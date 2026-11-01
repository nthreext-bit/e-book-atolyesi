using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using KitaplikEditoru.Localization;

namespace KitaplikEditoru.Core;

public sealed class EpubValidator
{
    private const long MaximumXmlEntryLength = 32 * 1024 * 1024;
    private const string EpubMimeType = "application/epub+zip";

    public async Task<IReadOnlyList<EpubValidationIssue>> ValidateAsync(
        string epubPath,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<EpubValidationIssue>();
        try
        {
            await using var file = new FileStream(epubPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            using var archive = new ZipArchive(file, ZipArchiveMode.Read);
            if (archive.Entries.Count == 0)
            {
                Add(issues, EpubIssueSeverity.Error, string.Empty, Strings.Get("Validation.EmptyArchive"));
                return issues;
            }

            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!entries.TryAdd(entry.FullName, entry))
                    Add(issues, EpubIssueSeverity.Error, entry.FullName, Strings.Get("Validation.DuplicatePath"));
            }

            var first = archive.Entries[0];
            if (first.FullName != "mimetype")
                Add(issues, EpubIssueSeverity.Error, "mimetype", Strings.Get("Validation.MimetypeFirst"));
            if (!entries.TryGetValue("mimetype", out var mimetype))
            {
                Add(issues, EpubIssueSeverity.Error, "mimetype", Strings.Get("Validation.MimetypeMissing"));
            }
            else
            {
                if (mimetype.CompressedLength != mimetype.Length)
                    Add(issues, EpubIssueSeverity.Error, "mimetype", Strings.Get("Validation.MimetypeCompressed"));
                var mediaType = mimetype.Length <= EpubMimeType.Length
                    ? await ReadTextAsync(mimetype, cancellationToken)
                    : string.Empty;
                if (!string.Equals(mediaType, EpubMimeType, StringComparison.Ordinal))
                    Add(issues, EpubIssueSeverity.Error, "mimetype", Strings.Get("Validation.MimetypeInvalid"));
            }

            if (!entries.TryGetValue("META-INF/container.xml", out var containerEntry))
            {
                Add(issues, EpubIssueSeverity.Error, "META-INF/container.xml", Strings.Get("Validation.ContainerMissing"));
                return issues;
            }

            var container = await ReadXmlAsync(containerEntry, cancellationToken);
            var packagePath = container?.Descendants().FirstOrDefault(element => element.Name.LocalName == "rootfile")
                ?.Attribute("full-path")?.Value;
            if (string.IsNullOrWhiteSpace(packagePath) || !TryResolveArchivePath(string.Empty, packagePath, out var packageEntryPath))
            {
                Add(issues, EpubIssueSeverity.Error, "META-INF/container.xml", Strings.Get("Validation.PackagePathInvalid"));
                return issues;
            }
            if (!entries.TryGetValue(packageEntryPath, out var packageEntry))
            {
                Add(issues, EpubIssueSeverity.Error, packageEntryPath, Strings.Get("Validation.PackageMissing"));
                return issues;
            }

            var package = await ReadXmlAsync(packageEntry, cancellationToken);
            var manifest = package?.Descendants().FirstOrDefault(element => element.Name.LocalName == "manifest");
            var spine = package?.Descendants().FirstOrDefault(element => element.Name.LocalName == "spine");
            if (manifest is null || spine is null)
            {
                Add(issues, EpubIssueSeverity.Error, packageEntryPath, Strings.Get("Validation.ManifestSpineMissing"));
                return issues;
            }

            var metadata = package!.Descendants().FirstOrDefault(element => element.Name.LocalName == "metadata");
            foreach (var required in new[] { "title", "identifier", "language" })
            {
                if (metadata?.Elements().Any(element => element.Name.LocalName == required && !string.IsNullOrWhiteSpace(element.Value)) != true)
                    Add(issues, EpubIssueSeverity.Warning, packageEntryPath,
                        Strings.Format("Validation.MetadataMissing", required));
            }

            var packageDirectory = Path.GetDirectoryName(packageEntryPath)?.Replace('\\', '/') ?? string.Empty;
            var manifestById = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in manifest.Elements().Where(element => element.Name.LocalName == "item"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var id = (string?)item.Attribute("id");
                var href = (string?)item.Attribute("href");
                var mediaType = (string?)item.Attribute("media-type");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(mediaType))
                {
                    Add(issues, EpubIssueSeverity.Error, packageEntryPath, Strings.Get("Validation.ManifestItemIncomplete"));
                    continue;
                }
                if (!manifestById.TryAdd(id, href))
                    Add(issues, EpubIssueSeverity.Error, packageEntryPath,
                        Strings.Format("Validation.ManifestIdDuplicate", id));

                if (!TryResolveArchivePath(packageDirectory, href, out var itemPath))
                {
                    Add(issues, EpubIssueSeverity.Error, href, Strings.Get("Validation.ManifestHrefInvalid"));
                    continue;
                }
                if (!entries.TryGetValue(itemPath, out var resourceEntry))
                {
                    Add(issues, EpubIssueSeverity.Error, itemPath, Strings.Get("Validation.ManifestResourceMissing"));
                    continue;
                }
                if (mediaType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
                {
                    var document = await ReadXmlAsync(resourceEntry, cancellationToken);
                    if (document?.Root?.Name.LocalName != "html")
                        Add(issues, EpubIssueSeverity.Error, itemPath, Strings.Get("Validation.XhtmlRootMissing"));
                    else if (!document.Descendants().Any(element => element.Name.LocalName == "body"))
                        Add(issues, EpubIssueSeverity.Error, itemPath, Strings.Get("Validation.XhtmlBodyMissing"));
                    if (document is not null)
                        ValidateDocumentReferences(document, itemPath, entries, issues);
                }
            }

            foreach (var itemRef in spine.Elements().Where(element => element.Name.LocalName == "itemref"))
            {
                var idref = (string?)itemRef.Attribute("idref");
                if (string.IsNullOrWhiteSpace(idref) || !manifestById.ContainsKey(idref))
                    Add(issues, EpubIssueSeverity.Error, packageEntryPath,
                        Strings.Format("Validation.SpineReferenceMissing", idref ?? "(empty)"));
            }

            var packageVersion = (string?)package.Root?.Attribute("version");
            if (packageVersion == "3.0" &&
                !manifest.Elements().Any(element => ((string?)element.Attribute("properties"))?
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("nav", StringComparer.Ordinal) == true))
                Add(issues, EpubIssueSeverity.Warning, packageEntryPath, Strings.Get("Validation.NavigationMissing"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or XmlException or ArgumentException)
        {
            Add(issues, EpubIssueSeverity.Error, epubPath,
                Strings.Format("Validation.PackageReadError", exception.Message));
        }

        if (issues.Count == 0)
            Add(issues, EpubIssueSeverity.Information, string.Empty, Strings.Get("Validation.Clean"));
        return issues;
    }

    private static async Task<XDocument?> ReadXmlAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        if (entry.Length > MaximumXmlEntryLength)
            throw new InvalidDataException(Strings.Format("Validation.XmlTooLarge", entry.FullName));
        await using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumXmlEntryLength
        });
        return await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
    }

    private static async Task<string> ReadTextAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using var stream = entry.Open();
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        return (await reader.ReadToEndAsync(cancellationToken)).Trim();
    }

    private static bool TryResolveArchivePath(string baseDirectory, string href, out string path)
    {
        path = string.Empty;
        if (Uri.TryCreate(href, UriKind.Absolute, out _) || href.StartsWith("/", StringComparison.Ordinal) ||
            href.Contains('\\'))
            return false;

        var pathPart = Uri.UnescapeDataString(href.Split('#', '?')[0]);
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(baseDirectory))
            parts.AddRange(baseDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries));
        foreach (var segment in pathPart.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
                continue;
            if (segment == "..")
            {
                if (parts.Count == 0)
                    return false;
                parts.RemoveAt(parts.Count - 1);
                continue;
            }
            if (segment.Contains(':') || segment.Contains('\0'))
                return false;
            parts.Add(segment);
        }
        if (parts.Count == 0)
            return false;
        path = string.Join('/', parts);
        return true;
    }

    private static void ValidateDocumentReferences(
        XDocument document,
        string documentPath,
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        ICollection<EpubValidationIssue> issues)
    {
        var directory = Path.GetDirectoryName(documentPath)?.Replace('\\', '/') ?? string.Empty;
        foreach (var attribute in document.Descendants().Attributes()
                     .Where(attribute => attribute.Name.LocalName is "href" or "src"))
        {
            var href = attribute.Value.Trim();
            if (href.Length == 0)
                continue;
            var normalized = Regex.Replace(href, @"[\u0000-\u0020]+", string.Empty, RegexOptions.CultureInvariant);
            if (normalized.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                Add(issues, EpubIssueSeverity.Error, documentPath,
                    Strings.Format("Validation.ResourceReferenceUnsafe", href));
                continue;
            }
            if (normalized.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                Uri.TryCreate(href, UriKind.Absolute, out _))
                continue;

            var resourceHref = href.Split('#', '?')[0];
            var candidatePath = resourceHref.Length == 0
                ? documentPath
                : TryResolveArchivePath(directory, resourceHref, out var resolvedPath) ? resolvedPath : null;
            if (candidatePath is null)
            {
                Add(issues, EpubIssueSeverity.Error, documentPath,
                    Strings.Format("Validation.ResourceReferenceUnsafe", href));
            }
            else if (!entries.ContainsKey(candidatePath))
            {
                Add(issues, EpubIssueSeverity.Error, documentPath,
                    Strings.Format("Validation.ResourceReferenceMissing", href));
            }
        }
    }

    private static void Add(
        ICollection<EpubValidationIssue> issues,
        EpubIssueSeverity severity,
        string path,
        string message) => issues.Add(new EpubValidationIssue(severity, path, message));
}
