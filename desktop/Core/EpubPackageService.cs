using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace KitaplikEditoru.Core;

public sealed class EpubPackageService
{
    private static readonly XNamespace XhtmlNamespace = "http://www.w3.org/1999/xhtml";
    private static readonly XNamespace OpfNamespace = "http://www.idpf.org/2007/opf";
    private static readonly XNamespace DcNamespace = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace DctermsNamespace = "http://purl.org/dc/terms/";
    private static readonly XNamespace EpubNamespace = "http://www.idpf.org/2007/ops";

    public async Task<EpubBook> CreateNewAsync(CancellationToken cancellationToken = default)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), "EpubAtolyesi", Guid.NewGuid().ToString("N"));
        var contentDirectory = Path.Combine(workingDirectory, "OEBPS");
        var textDirectory = Path.Combine(contentDirectory, "Text");
        Directory.CreateDirectory(Path.Combine(workingDirectory, "META-INF"));
        Directory.CreateDirectory(textDirectory);
        await File.WriteAllTextAsync(Path.Combine(workingDirectory, "mimetype"), "application/epub+zip", new UTF8Encoding(false), cancellationToken);

        var container = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement("container", new XAttribute("version", "1.0"),
                new XAttribute("xmlns", "urn:oasis:names:tc:opendocument:xmlns:container"),
                new XElement("rootfiles", new XElement("rootfile",
                    new XAttribute("full-path", "OEBPS/content.opf"),
                    new XAttribute("media-type", "application/oebps-package+xml")))));
        container.Save(Path.Combine(workingDirectory, "META-INF", "container.xml"));

        var identifier = $"urn:uuid:{Guid.NewGuid()}";
        var package = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(OpfNamespace + "package",
                new XAttribute("version", "3.0"),
                new XAttribute("unique-identifier", "book-id"),
                new XAttribute(XNamespace.Xmlns + "dc", DcNamespace),
                new XAttribute(XNamespace.Xmlns + "dcterms", DctermsNamespace),
                new XElement(OpfNamespace + "metadata",
                    new XElement(DcNamespace + "identifier", new XAttribute("id", "book-id"), identifier),
                    new XElement(DcNamespace + "title", "Yeni kitap"),
                    new XElement(DcNamespace + "language", "tr"),
                    new XElement(DctermsNamespace + "modified", DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"))),
                new XElement(OpfNamespace + "manifest",
                    new XElement(OpfNamespace + "item", new XAttribute("id", "nav"), new XAttribute("href", "nav.xhtml"),
                        new XAttribute("media-type", "application/xhtml+xml"), new XAttribute("properties", "nav")),
                    new XElement(OpfNamespace + "item", new XAttribute("id", "ncx"), new XAttribute("href", "toc.ncx"),
                        new XAttribute("media-type", "application/x-dtbncx+xml")),
                    new XElement(OpfNamespace + "item", new XAttribute("id", "section0001"), new XAttribute("href", "Text/Section0001.xhtml"),
                        new XAttribute("media-type", "application/xhtml+xml"))),
                new XElement(OpfNamespace + "spine", new XAttribute("toc", "ncx"), new XElement(OpfNamespace + "itemref", new XAttribute("idref", "section0001")))));
        package.Save(Path.Combine(contentDirectory, "content.opf"));

        var navigation = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(XhtmlNamespace + "html",
                new XElement(XhtmlNamespace + "head", new XElement(XhtmlNamespace + "title", "İçindekiler")),
                new XElement(XhtmlNamespace + "body",
                    new XElement(XhtmlNamespace + "nav", new XAttribute(XNamespace.Xmlns + "epub", EpubNamespace), new XAttribute(EpubNamespace + "type", "toc"),
                        new XElement(XhtmlNamespace + "h1", "İçindekiler"),
                        new XElement(XhtmlNamespace + "ol", new XElement(XhtmlNamespace + "li",
                            new XElement(XhtmlNamespace + "a", new XAttribute("href", "Text/Section0001.xhtml"), "Bölüm 1")))))));
        navigation.Save(Path.Combine(contentDirectory, "nav.xhtml"));

        var ncx = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(XNamespace.Get("http://www.daisy.org/z3986/2005/ncx/") + "ncx",
                new XAttribute("version", "2005-1"),
                new XElement(XNamespace.Get("http://www.daisy.org/z3986/2005/ncx/") + "head",
                    new XElement(XNamespace.Get("http://www.daisy.org/z3986/2005/ncx/") + "meta", new XAttribute("name", "dtb:uid"), new XAttribute("content", identifier))),
                new XElement(XNamespace.Get("http://www.daisy.org/z3986/2005/ncx/") + "docTitle",
                    new XElement(XNamespace.Get("http://www.daisy.org/z3986/2005/ncx/") + "text", "Yeni kitap")),
                new XElement(XNamespace.Get("http://www.daisy.org/z3986/2005/ncx/") + "navMap",
                    new XElement(XNamespace.Get("http://www.daisy.org/z3986/2005/ncx/") + "navPoint",
                        new XAttribute("id", "section0001"), new XAttribute("playOrder", "1"),
                        new XElement(XNamespace.Get("http://www.daisy.org/z3986/2005/ncx/") + "navLabel",
                            new XElement(XNamespace.Get("http://www.daisy.org/z3986/2005/ncx/") + "text", "Bölüm 1")),
                        new XElement(XNamespace.Get("http://www.daisy.org/z3986/2005/ncx/") + "content", new XAttribute("src", "Text/Section0001.xhtml"))))));
        ncx.Save(Path.Combine(contentDirectory, "toc.ncx"));

        var sectionPath = Path.Combine(textDirectory, "Section0001.xhtml");
        var sectionDocument = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(XhtmlNamespace + "html",
                new XElement(XhtmlNamespace + "head", new XElement(XhtmlNamespace + "title", "Bölüm 1")),
                new XElement(XhtmlNamespace + "body", new XElement(XhtmlNamespace + "h1", "Bölüm 1"), new XElement(XhtmlNamespace + "p"))));
        sectionDocument.Save(sectionPath);

        var book = new EpubBook
        {
            PackageFile = string.Empty,
            WorkingDirectory = workingDirectory,
            PackageRelativePath = "OEBPS/content.opf",
            Title = "Yeni kitap",
            IsPackageDirty = true
        };
        book.Resources.Add(new EpubResource("nav", "nav.xhtml", "application/xhtml+xml", Path.Combine(contentDirectory, "nav.xhtml"), IsNavigation: true));
        book.Resources.Add(new EpubResource("ncx", "toc.ncx", "application/x-dtbncx+xml", Path.Combine(contentDirectory, "toc.ncx"), IsNavigation: true));
        book.Resources.Add(new EpubResource("section0001", "Text/Section0001.xhtml", "application/xhtml+xml", sectionPath));
        book.Sections.Add(new EpubSection
        {
            Id = "section0001",
            RelativePath = "OEBPS/Text/Section0001.xhtml",
            FilePath = sectionPath,
            Title = "Bölüm 1",
            ContentXhtml = "<h1 xmlns=\"http://www.w3.org/1999/xhtml\">Bölüm 1</h1><p xmlns=\"http://www.w3.org/1999/xhtml\"></p>",
            WordCount = 2,
            WordCountKnown = true,
            IsLoaded = true,
            IsDirty = true
        });
        return book;
    }

    public async Task<EpubBook> OpenAsync(string epubPath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(epubPath);
        var workingDirectory = Path.Combine(Path.GetTempPath(), "EpubAtolyesi", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            using (var archive = ZipFile.OpenRead(fullPath))
            {
                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var destination = GetSafeDestination(workingDirectory, entry.FullName);
                    if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                    {
                        Directory.CreateDirectory(destination);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    await using var source = entry.Open();
                    await using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
                    await source.CopyToAsync(target, cancellationToken);
                }
            }

            var containerPath = Path.Combine(workingDirectory, "META-INF", "container.xml");
            var container = XDocument.Load(containerPath);
            var packagePath = container.Descendants().FirstOrDefault(element => element.Name.LocalName == "rootfile")?
                .Attribute("full-path")?.Value;
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                throw new InvalidDataException("EPUB içindeki container.xml dosyasında OPF paketi bulunamadı.");
            }

            var packageRelativePath = NormalizeArchivePath(packagePath);
            var packageFullPath = GetSafeDestination(workingDirectory, packageRelativePath);
            var packageDocument = XDocument.Load(packageFullPath, LoadOptions.PreserveWhitespace);
            var manifest = packageDocument.Descendants().FirstOrDefault(element => element.Name.LocalName == "manifest")
                ?? throw new InvalidDataException("EPUB OPF dosyasında manifest bölümü bulunamadı.");
            var packageDirectory = Path.GetDirectoryName(packageRelativePath)?.Replace('\\', '/') ?? string.Empty;
            var epub2CoverId = packageDocument.Descendants().FirstOrDefault(element => element.Name.LocalName == "meta" &&
                (string?)element.Attribute("name") == "cover")?.Attribute("content")?.Value;
            var resourcesById = new Dictionary<string, EpubResource>(StringComparer.Ordinal);

            foreach (var item in manifest.Elements().Where(element => element.Name.LocalName == "item"))
            {
                var id = (string?)item.Attribute("id");
                var href = (string?)item.Attribute("href");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(href))
                {
                    continue;
                }

                var resourcePath = NormalizeArchivePath(string.Join('/', new[] { packageDirectory, Uri.UnescapeDataString(href) }.Where(part => part.Length > 0)));
                var properties = ((string?)item.Attribute("properties"))?.Split(' ') ?? [];
                var mediaType = (string?)item.Attribute("media-type") ?? string.Empty;
                var isNavigation = properties.Contains("nav") || mediaType == "application/x-dtbncx+xml";
                var resource = new EpubResource(id, href, mediaType,
                    GetSafeDestination(workingDirectory, resourcePath), id == epub2CoverId || properties.Contains("cover-image"), isNavigation);
                resourcesById[id] = resource;
            }

            var book = new EpubBook
            {
                PackageFile = fullPath,
                WorkingDirectory = workingDirectory,
                PackageRelativePath = packageRelativePath,
                Title = packageDocument.Descendants().FirstOrDefault(element => element.Name.LocalName == "title")?.Value.Trim() ?? Path.GetFileNameWithoutExtension(fullPath)
            };
            book.Resources.AddRange(resourcesById.Values);

            var spine = packageDocument.Descendants().FirstOrDefault(element => element.Name.LocalName == "spine")
                ?? throw new InvalidDataException("EPUB OPF dosyasında spine bölümü bulunamadı.");
            var navItemId = manifest.Elements().FirstOrDefault(element => ((string?)element.Attribute("properties"))?.Split(' ').Contains("nav") == true)?
                .Attribute("id")?.Value;

            foreach (var itemRef in spine.Elements().Where(element => element.Name.LocalName == "itemref"))
            {
                var id = (string?)itemRef.Attribute("idref");
                if (id is null || id == navItemId || !resourcesById.TryGetValue(id, out var resource) ||
                    !IsXhtmlResource(resource) || !File.Exists(resource.FullPath))
                {
                    continue;
                }

                book.Sections.Add(new EpubSection
                {
                    Id = resource.Id,
                    RelativePath = GetRelativePath(workingDirectory, resource.FullPath),
                    FilePath = resource.FullPath,
                    Title = Path.GetFileNameWithoutExtension(resource.Href)
                });
            }

            if (book.Sections.Count == 0)
            {
                throw new InvalidDataException("EPUB spine içinde düzenlenebilir XHTML bölümü bulunamadı.");
            }

            return book;
        }
        catch
        {
            try
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
            catch
            {
            }

            throw;
        }
    }

    public async Task LoadSectionAsync(EpubSection section, CancellationToken cancellationToken = default)
    {
        if (section.IsLoaded)
        {
            return;
        }

        var data = await Task.Run(() => ReadSection(section.FilePath, section.RelativePath), cancellationToken);
        section.ContentXhtml = data.Content;
        section.Title = data.Title;
        section.WordCount = data.WordCount;
        section.WordCountKnown = true;
        section.IsLoaded = true;
    }

    public async Task<int> CountSectionWordsAsync(EpubSection section, CancellationToken cancellationToken = default)
    {
        if (section.WordCountKnown)
        {
            return section.WordCount;
        }

        return await Task.Run(() => ReadSection(section.FilePath, section.RelativePath).WordCount, cancellationToken);
    }

    private static (string Content, string Title, int WordCount) ReadSection(string path, string relativePath)
    {
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var body = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "body")
            ?? throw new InvalidDataException($"'{relativePath}' içinde body öğesi bulunamadı.");
            var title = document.Descendants().FirstOrDefault(element => element.Name.LocalName is "h1" or "h2" or "h3" or "h4" or "h5" or "h6")?.Value.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            title = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "title")?.Value.Trim();
        }
        if (string.IsNullOrWhiteSpace(title))
        {
            title = Path.GetFileNameWithoutExtension(path);
        }

        var content = string.Concat(body.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting)));
        var wordCount = body.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return (content, title, wordCount);
    }

    public async Task SaveAsync(EpubBook book, string targetPath, bool createBackup = false, CancellationToken cancellationToken = default)
    {
        foreach (var section in book.Sections.Where(section => section.IsDirty))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fragment = XElement.Parse($"<fragment xmlns=\"{XhtmlNamespace}\">{section.ContentXhtml}</fragment>", LoadOptions.PreserveWhitespace);
            var document = XDocument.Load(section.FilePath, LoadOptions.PreserveWhitespace);
            var body = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "body")
                ?? throw new InvalidDataException($"'{section.RelativePath}' içinde body öğesi bulunamadı.");
            body.ReplaceNodes(fragment.Nodes());

            await using var stream = new FileStream(section.FilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false });
            document.Save(writer);
        }

        var fullTargetPath = Path.GetFullPath(targetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullTargetPath)!);
        var temporaryArchive = fullTargetPath + ".tmp";
        try
        {
            await using (var output = new FileStream(temporaryArchive, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
                var mimetypePath = Path.Combine(book.WorkingDirectory, "mimetype");
                if (!File.Exists(mimetypePath))
                {
                    throw new InvalidDataException("EPUB içinde zorunlu mimetype dosyası bulunamadı.");
                }

                await WriteEntryAsync(archive, mimetypePath, "mimetype", CompressionLevel.NoCompression, cancellationToken);
                foreach (var file in Directory.EnumerateFiles(book.WorkingDirectory, "*", SearchOption.AllDirectories)
                    .Where(file => !string.Equals(file, mimetypePath, StringComparison.OrdinalIgnoreCase)))
                {
                    var relativePath = GetRelativePath(book.WorkingDirectory, file);
                    await WriteEntryAsync(archive, file, relativePath, CompressionLevel.Optimal, cancellationToken);
                }
            }

            if (createBackup && File.Exists(fullTargetPath))
            {
                File.Copy(fullTargetPath, fullTargetPath + ".bak", overwrite: true);
            }

            File.Move(temporaryArchive, fullTargetPath, overwrite: true);
            book.PackageFile = fullTargetPath;
            foreach (var section in book.Sections)
            {
                section.IsDirty = false;
            }
            book.IsPackageDirty = false;
        }
        finally
        {
            if (File.Exists(temporaryArchive))
            {
                File.Delete(temporaryArchive);
            }
        }
    }

    public async Task<string> AddImageAsync(EpubBook book, string imagePath, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(imagePath).ToLowerInvariant();
        var mediaType = extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".webp" => "image/webp",
            _ => throw new InvalidDataException("Desteklenen resim biçimleri PNG, JPEG, GIF, SVG ve WebP'dir.")
        };
        var packageDirectory = Path.GetDirectoryName(book.PackageRelativePath)?.Replace('\\', '/') ?? string.Empty;
        var imageName = $"image-{Guid.NewGuid():N}{extension}";
        var imageRelativePath = string.Join('/', new[] { packageDirectory, "Images", imageName }.Where(part => part.Length > 0));
        var destination = GetSafeDestination(book.WorkingDirectory, imageRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using (var source = File.OpenRead(imagePath))
        await using (var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await source.CopyToAsync(target, cancellationToken);
        }

        var packagePath = GetSafeDestination(book.WorkingDirectory, book.PackageRelativePath);
        var packageDocument = XDocument.Load(packagePath, LoadOptions.PreserveWhitespace);
        var manifest = packageDocument.Descendants().First(element => element.Name.LocalName == "manifest");
        var itemId = $"image-{Guid.NewGuid():N}";
        manifest.Add(new XElement(manifest.Name.Namespace + "item",
            new XAttribute("id", itemId),
            new XAttribute("href", string.Join('/', new[] { "Images", imageName })),
            new XAttribute("media-type", mediaType)));
        await using (var stream = new FileStream(packagePath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false }))
        {
            packageDocument.Save(writer);
        }

        book.Resources.Add(new EpubResource(itemId, $"Images/{imageName}", mediaType, destination));
        book.IsPackageDirty = true;
        return imageRelativePath;
    }

    public void ReorderSpine(EpubBook book, IReadOnlyList<string> orderedSectionIds)
    {
        var packagePath = GetSafeDestination(book.WorkingDirectory, book.PackageRelativePath);
        var packageDocument = XDocument.Load(packagePath, LoadOptions.PreserveWhitespace);
        var spine = packageDocument.Descendants().First(element => element.Name.LocalName == "spine");
        var itemRefs = spine.Elements().Where(element => element.Name.LocalName == "itemref").ToList();
        var sectionIds = book.Sections.Select(section => section.Id).ToHashSet(StringComparer.Ordinal);
        var sectionRefs = itemRefs.Where(element => sectionIds.Contains((string?)element.Attribute("idref") ?? string.Empty))
            .ToDictionary(element => (string)element.Attribute("idref")!, element => new XElement(element), StringComparer.Ordinal);
        var sectionSlots = itemRefs.Where(element => sectionIds.Contains((string?)element.Attribute("idref") ?? string.Empty)).ToList();
        for (var index = 0; index < Math.Min(sectionSlots.Count, orderedSectionIds.Count); index++)
        {
            if (!sectionRefs.TryGetValue(orderedSectionIds[index], out var itemRef))
            {
                continue;
            }

            sectionSlots[index].ReplaceWith(itemRef);
        }

        packageDocument.Save(packagePath, SaveOptions.DisableFormatting);
        book.IsPackageDirty = true;
    }

    public void RenameSection(EpubBook book, EpubSection section, string title)
    {
        XElement fragment;
        if (section.IsLoaded)
        {
            fragment = XElement.Parse($"<fragment xmlns=\"{XhtmlNamespace}\">{section.ContentXhtml}</fragment>", LoadOptions.PreserveWhitespace);
        }
        else
        {
            var document = XDocument.Load(section.FilePath, LoadOptions.PreserveWhitespace);
            var body = document.Descendants().First(element => element.Name.LocalName == "body");
            fragment = new XElement(XhtmlNamespace + "fragment", body.Nodes());
        }

        var heading = fragment.Descendants().FirstOrDefault(element => element.Name.LocalName is "h1" or "h2" or "h3" or "h4" or "h5" or "h6");
        if (heading is null)
        {
            fragment.AddFirst(new XElement(XhtmlNamespace + "h1", title));
        }
        else
        {
            heading.Value = title;
        }

        section.Title = title;
        section.ContentXhtml = string.Concat(fragment.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting)));
        section.WordCount = fragment.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        section.WordCountKnown = true;
        section.IsLoaded = true;
        section.IsDirty = true;
        UpdateNavigation(book, section, title, delete: false);
        book.IsPackageDirty = true;
    }

    public void DeleteSection(EpubBook book, EpubSection section)
    {
        if (book.Sections.Count <= 1)
        {
            throw new InvalidOperationException("Kitapta en az bir bölüm kalmalıdır.");
        }

        var packagePath = GetSafeDestination(book.WorkingDirectory, book.PackageRelativePath);
        var package = XDocument.Load(packagePath, LoadOptions.PreserveWhitespace);
        package.Descendants().First(element => element.Name.LocalName == "spine").Elements()
            .Where(element => element.Name.LocalName == "itemref" && (string?)element.Attribute("idref") == section.Id)
            .Remove();
        package.Descendants().First(element => element.Name.LocalName == "manifest").Elements()
            .Where(element => element.Name.LocalName == "item" && (string?)element.Attribute("id") == section.Id)
            .Remove();
        package.Save(packagePath, SaveOptions.DisableFormatting);

        UpdateNavigation(book, section, string.Empty, delete: true);
        if (File.Exists(section.FilePath))
        {
            File.Delete(section.FilePath);
        }
        book.Sections.Remove(section);
        book.Resources.RemoveAll(resource => resource.Id == section.Id);
        book.IsPackageDirty = true;
    }

    private static void UpdateNavigation(EpubBook book, EpubSection section, string title, bool delete)
    {
        foreach (var resource in book.Resources.Where(resource => resource.IsNavigation))
        {
            if (!File.Exists(resource.FullPath) || resource.Id == section.Id)
            {
                continue;
            }

            var document = XDocument.Load(resource.FullPath, LoadOptions.PreserveWhitespace);
            if (resource.MediaType == "application/xhtml+xml")
            {
                foreach (var anchor in document.Descendants().Where(element => element.Name.LocalName == "a"))
                {
                    var href = (string?)anchor.Attribute("href");
                    if (href is null || !ResolvesToSection(resource.FullPath, href, book.WorkingDirectory, section.RelativePath)) continue;
                    if (delete)
                    {
                        anchor.Ancestors().FirstOrDefault(element => element.Name.LocalName == "li")?.Remove();
                    }
                    else
                    {
                        anchor.Value = title;
                    }
                }
            }
            else
            {
                foreach (var content in document.Descendants().Where(element => element.Name.LocalName == "content"))
                {
                    var source = (string?)content.Attribute("src");
                    if (source is null || !ResolvesToSection(resource.FullPath, source, book.WorkingDirectory, section.RelativePath)) continue;
                    var navPoint = content.Ancestors().FirstOrDefault(element => element.Name.LocalName == "navPoint");
                    if (delete)
                    {
                        navPoint?.Remove();
                    }
                    else if (navPoint is not null)
                    {
                        var label = navPoint.Descendants().FirstOrDefault(element => element.Name.LocalName == "text");
                        if (label is not null) label.Value = title;
                    }
                }
                if (delete)
                {
                    var playOrder = 1;
                    foreach (var navPoint in document.Descendants().Where(element => element.Name.LocalName == "navPoint"))
                    {
                        navPoint.SetAttributeValue("playOrder", playOrder++.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                }
            }
            SaveXmlDocument(document, resource.FullPath);
        }
    }

    private static bool ResolvesToSection(string navigationPath, string href, string workingDirectory, string sectionRelativePath)
    {
        var path = Uri.UnescapeDataString(href.Split('#', '?')[0]);
        var candidate = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(navigationPath)!, path.Replace('/', Path.DirectorySeparatorChar)));
        return string.Equals(candidate, GetSafeDestination(workingDirectory, sectionRelativePath), StringComparison.OrdinalIgnoreCase);
    }

    private static void SaveXmlDocument(XDocument document, string path)
    {
        using var writer = XmlWriter.Create(path, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false });
        document.Save(writer);
    }

    private static async Task WriteEntryAsync(ZipArchive archive, string sourcePath, string entryName, CompressionLevel compressionLevel, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName.Replace('\\', '/'), compressionLevel);
        await using var input = File.OpenRead(sourcePath);
        await using var output = entry.Open();
        await input.CopyToAsync(output, cancellationToken);
    }

    private static string GetSafeDestination(string root, string archivePath)
    {
        var normalizedRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var destination = Path.GetFullPath(Path.Combine(root, NormalizeArchivePath(archivePath).Replace('/', Path.DirectorySeparatorChar)));
        if (!destination.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("EPUB arşivi geçersiz bir dosya yolu içeriyor.");
        }

        return destination;
    }

    private static string NormalizeArchivePath(string path) => path.Replace('\\', '/').TrimStart('/');

    private static string GetRelativePath(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');

    private static bool IsXhtmlResource(EpubResource resource) =>
        resource.MediaType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(resource.Href).Equals(".xhtml", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(resource.Href).Equals(".html", StringComparison.OrdinalIgnoreCase);
}