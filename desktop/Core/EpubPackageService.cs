using System.IO.Compression;
using System.Text.RegularExpressions;
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
    private static readonly XNamespace ContainerNamespace = "urn:oasis:names:tc:opendocument:xmlns:container";

    public async Task<EpubBook> CreateNewAsync(CancellationToken cancellationToken = default)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), "EpubAtolyesi", Guid.NewGuid().ToString("N"));
        var contentDirectory = Path.Combine(workingDirectory, "OEBPS");
        var textDirectory = Path.Combine(contentDirectory, "Text");
        Directory.CreateDirectory(Path.Combine(workingDirectory, "META-INF"));
        Directory.CreateDirectory(textDirectory);
        await File.WriteAllTextAsync(Path.Combine(workingDirectory, "mimetype"), "application/epub+zip", new UTF8Encoding(false), cancellationToken);

        var container = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(ContainerNamespace + "container", new XAttribute("version", "1.0"),
                new XElement(ContainerNamespace + "rootfiles", new XElement(ContainerNamespace + "rootfile",
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

    private static bool ResolvesToFile(string sourcePath, string href, string expectedPath)
    {
        var path = Uri.UnescapeDataString(href.Split('#', '?')[0]);
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsAbsoluteUri)
        {
            return false;
        }
        var candidate = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, path.Replace('/', Path.DirectorySeparatorChar)));
        return string.Equals(candidate, expectedPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveLocalReference(string sourcePath, string href, out string targetPath)
    {
        targetPath = string.Empty;
        var path = Uri.UnescapeDataString(href.Split('#', '?')[0]);
        if (path.Length == 0 || Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsAbsoluteUri)
            return false;
        targetPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, path.Replace('/', Path.DirectorySeparatorChar)));
        return true;
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
                var looksLikeCover = mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
                    (id.Contains("cover", StringComparison.OrdinalIgnoreCase) ||
                     Path.GetFileNameWithoutExtension(href).Contains("cover", StringComparison.OrdinalIgnoreCase));
                var resource = new EpubResource(id, href, mediaType,
                    GetSafeDestination(workingDirectory, resourcePath),
                    id == epub2CoverId || properties.Contains("cover-image") || looksLikeCover,
                    isNavigation);
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

    public async Task<string> ReadRawSectionSourceAsync(EpubSection section, CancellationToken cancellationToken = default)
    {
        var file = new FileInfo(section.FilePath);
        if (file.Length > 32 * 1024 * 1024)
        {
            throw new InvalidDataException($"'{section.RelativePath}' exceeds the 32 MB source-view limit.");
        }

        return await File.ReadAllTextAsync(section.FilePath, cancellationToken);
    }

    public async Task ImportSectionContentAsync(
        EpubSection section,
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        if (new FileInfo(sourcePath).Length > 32 * 1024 * 1024)
            throw new InvalidDataException("Imported section content exceeds the 32 MB size limit.");
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        var source = await File.ReadAllTextAsync(sourcePath, cancellationToken);
        string content;
        if (extension == ".txt")
        {
            var paragraphs = source.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n')
                .Select(line => string.IsNullOrWhiteSpace(line)
                    ? new XElement(XhtmlNamespace + "p", new XElement(XhtmlNamespace + "br"))
                    : new XElement(XhtmlNamespace + "p", line));
            content = string.Concat(paragraphs.Select(paragraph => paragraph.ToString(SaveOptions.DisableFormatting)));
        }
        else if (extension is ".xhtml" or ".xml")
        {
            using var textReader = new StringReader(source);
            using var reader = XmlReader.Create(textReader, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 32 * 1024 * 1024
            });
            var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            var body = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "body")
                ?? throw new InvalidDataException("Imported XHTML must contain a body element.");
            SanitizeImportedContent(body);
            content = string.Concat(body.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting)));
        }
        else
        {
            throw new InvalidDataException("Only plain text (.txt) and well-formed XHTML (.xhtml) files can be imported.");
        }

        var fragment = XElement.Parse($"<fragment xmlns=\"{XhtmlNamespace}\">{content}</fragment>", LoadOptions.PreserveWhitespace);
        section.ContentXhtml = string.Concat(fragment.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting)));
        section.IsLoaded = true;
        section.IsDirty = true;
        section.WordCount = fragment.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        section.WordCountKnown = true;
    }

    public async Task ExportSectionAsync(
        EpubSection section,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(destinationPath).ToLowerInvariant();
        var fragment = XElement.Parse($"<fragment xmlns=\"{XhtmlNamespace}\">{section.ContentXhtml}</fragment>", LoadOptions.PreserveWhitespace);
        string content;
        if (extension == ".txt")
        {
            content = FlattenPlainText(fragment).Trim();
        }
        else if (extension is ".xhtml" or ".xml")
        {
            var document = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(XhtmlNamespace + "html",
                    new XElement(XhtmlNamespace + "head", new XElement(XhtmlNamespace + "title", section.Title)),
                    new XElement(XhtmlNamespace + "body", fragment.Nodes())));
            content = document.ToString(SaveOptions.DisableFormatting);
        }
        else
        {
            throw new InvalidDataException("Only plain text (.txt) and XHTML (.xhtml) files can be exported.");
        }

        await File.WriteAllTextAsync(destinationPath, content, new UTF8Encoding(false), cancellationToken);
    }

    private static string FlattenPlainText(XNode node)
    {
        if (node is XText text)
            return text.Value;
        if (node is not XElement element)
            return string.Empty;
        if (element.Name.LocalName == "br")
            return Environment.NewLine;

        var content = string.Concat(element.Nodes().Select(FlattenPlainText));
        return element.Name.LocalName is "p" or "div" or "li" or "blockquote" or "pre" or
            "h1" or "h2" or "h3" or "h4" or "h5" or "h6"
            ? content.TrimEnd() + Environment.NewLine
            : content;
    }

    private static void SanitizeImportedContent(XElement body)
    {
        body.Descendants().Where(element =>
                element.Name.LocalName is "script" or "iframe" or "object" or "embed" or "form" or "base" or "style" or "link" or "meta")
            .Remove();
        foreach (var element in body.DescendantsAndSelf())
        {
            foreach (var attribute in element.Attributes().ToArray())
            {
                var name = attribute.Name.LocalName;
                var value = attribute.Value.TrimStart();
                if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("srcdoc", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("style", StringComparison.OrdinalIgnoreCase) ||
                    ((name.Equals("href", StringComparison.OrdinalIgnoreCase) ||
                      name.Equals("src", StringComparison.OrdinalIgnoreCase) ||
                      name.Equals("xlink:href", StringComparison.OrdinalIgnoreCase)) &&
                     HasUnsafeProtocol(value)))
                    attribute.Remove();
            }
        }
    }

    private static bool HasUnsafeProtocol(string value)
    {
        var normalized = Regex.Replace(value, @"[\u0000-\u0020]+", string.Empty, RegexOptions.CultureInvariant);
        return normalized.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("//", StringComparison.Ordinal);
    }

    public async Task<int> CountSectionWordsAsync(EpubSection section, CancellationToken cancellationToken = default)
    {
        if (section.WordCountKnown)
        {
            return section.WordCount;
        }

        return await Task.Run(() => ReadSection(section.FilePath, section.RelativePath).WordCount, cancellationToken);
    }

    public List<EpubStyle> ReadStyles(EpubBook book)
    {
        var styles = new Dictionary<string, EpubStyle>(StringComparer.OrdinalIgnoreCase);
        foreach (var resource in book.Resources.Where(resource =>
                     resource.MediaType.Equals("text/css", StringComparison.OrdinalIgnoreCase) && File.Exists(resource.FullPath)))
        {
            var css = File.ReadAllText(resource.FullPath);
            foreach (Match rule in Regex.Matches(css, @"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.Singleline))
            {
                foreach (var selector in rule.Groups["selector"].Value.Split(','))
                {
                    var match = Regex.Match(selector.Trim(), @"^(?<tag>p|span)?\.(?<name>[A-Za-z_][\w-]*)$");
                    if (!match.Success)
                    {
                        continue;
                    }

                    var className = match.Groups["name"].Value;
                    var style = new EpubStyle
                    {
                        Name = className.Replace('-', ' '),
                        ClassName = className,
                        IsCharacterStyle = match.Groups["tag"].Value == "span",
                        SelectorPrefix = match.Groups["tag"].Value
                    };
                    foreach (var declaration in rule.Groups["body"].Value.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var separator = declaration.IndexOf(':');
                        if (separator <= 0)
                        {
                            continue;
                        }
                        var property = declaration[..separator].Trim();
                        var value = declaration[(separator + 1)..].Trim();
                        if (property.Length > 0 && value.Length > 0)
                        {
                            style.Properties[property] = value;
                        }
                    }
                    styles[style.Selector] = style;
                }
            }
        }

        return styles.Values.OrderBy(style => style.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public void SaveStyle(EpubBook book, EpubStyle style)
    {
        if (!Regex.IsMatch(style.ClassName, @"^[A-Za-z_][\w-]*$"))
        {
            throw new InvalidDataException("Stil adı İngilizce harf, rakam, alt çizgi veya tire içermelidir.");
        }

        var allowedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "font-family", "font-size", "color", "font-weight", "font-style", "text-align",
            "text-indent", "line-height", "margin-top", "margin-bottom"
        };
        var declarations = style.Properties
            .Where(item => allowedProperties.Contains(item.Key) && !item.Value.Contains('{') &&
                           !item.Value.Contains('}') && !item.Value.Contains(';'))
            .ToArray();
        if (declarations.Length == 0)
        {
            throw new InvalidDataException("Stil için en az bir geçerli CSS özelliği belirtin.");
        }

        var stylesheet = GetOrCreateStylesheet(book);
        var css = File.ReadAllText(stylesheet.FullPath);
        var pattern = $@"(?<prefix>(?:^|}})\s*){Regex.Escape(style.Selector)}\s*\{{[^{{}}]*\}}";
        var replacement = $"{style.Selector} {{ {string.Join("; ", declarations.Select(item => $"{item.Key}: {item.Value}"))}; }}";

        var match = Regex.Match(css, pattern, RegexOptions.Multiline);
        css = match.Success
            ? css.Remove(match.Index + match.Groups["prefix"].Length, match.Length - match.Groups["prefix"].Length)
                .Insert(match.Index + match.Groups["prefix"].Length, replacement)
            : $"{css.TrimEnd()}{Environment.NewLine}{replacement}{Environment.NewLine}";
        File.WriteAllText(stylesheet.FullPath, css, new UTF8Encoding(false));
        EnsureSectionStylesheetLinks(book, stylesheet);
        book.IsPackageDirty = true;
    }

    public void DeleteStyle(EpubBook book, EpubStyle style)
    {
        var updates = new List<(EpubSection Section, XDocument? Document, XElement Body, bool Changed)>();
        foreach (var section in book.Sections)
        {
            XDocument? document = null;
            XElement body;
            if (section.IsLoaded)
            {
                body = XElement.Parse($"<fragment xmlns=\"{XhtmlNamespace}\">{section.ContentXhtml}</fragment>", LoadOptions.PreserveWhitespace);
            }
            else
            {
                document = XDocument.Load(section.FilePath, LoadOptions.PreserveWhitespace);
                body = document.Descendants().First(element => element.Name.LocalName == "body");
            }

            var changed = false;
            foreach (var element in body.DescendantsAndSelf())
            {
                var classes = ((string?)element.Attribute("class"))?
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
                if (classes is null || !classes.Remove(style.ClassName)) continue;
                if (classes.Count == 0) element.Attribute("class")?.Remove();
                else element.SetAttributeValue("class", string.Join(' ', classes));
                changed = true;
            }
            updates.Add((section, document, body, changed));
        }

        foreach (var update in updates.Where(update => update.Changed))
        {
            if (update.Section.IsLoaded)
            {
                update.Section.ContentXhtml = string.Concat(update.Body.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting)));
                update.Section.IsDirty = true;
            }
            else if (update.Document is not null)
            {
                SaveXmlDocument(update.Document, update.Section.FilePath);
            }
            book.IsPackageDirty = true;
        }

        foreach (var resource in book.Resources.Where(resource =>
                     resource.MediaType.Equals("text/css", StringComparison.OrdinalIgnoreCase) && File.Exists(resource.FullPath)))
        {
            var css = File.ReadAllText(resource.FullPath);
            var pattern = $@"(?m)^[ \t]*{Regex.Escape(style.Selector)}\s*\{{[^{{}}]*\}}\s*";
            var updated = Regex.Replace(css, pattern, string.Empty);
            if (!string.Equals(css, updated, StringComparison.Ordinal))
            {
                File.WriteAllText(resource.FullPath, updated, new UTF8Encoding(false));
                book.IsPackageDirty = true;
            }
        }
    }

    public void SetCoverImage(EpubBook book, EpubResource selected)
    {
        var package = XDocument.Load(GetSafeDestination(book.WorkingDirectory, book.PackageRelativePath), LoadOptions.PreserveWhitespace);
        var root = package.Root ?? throw new InvalidDataException("OPF package root bulunamadı.");
        var manifest = root.Elements().First(element => element.Name.LocalName == "manifest");
        _ = manifest.Elements().FirstOrDefault(element => (string?)element.Attribute("id") == selected.Id)
            ?? throw new InvalidDataException("Kapak resmi EPUB manifest'inde bulunamadı.");
        var isEpub2 = ((string?)root.Attribute("version"))?.StartsWith("2", StringComparison.Ordinal) == true;
        if (isEpub2)
        {
            var metadata = root.Elements().First(element => element.Name.LocalName == "metadata");
            var coverMeta = metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "meta" &&
                (string?)element.Attribute("name") == "cover");
            if (coverMeta is null)
                metadata.Add(new XElement(metadata.Name.Namespace + "meta",
                    new XAttribute("name", "cover"), new XAttribute("content", selected.Id)));
            else
                coverMeta.SetAttributeValue("content", selected.Id);
        }
        else
        {
            foreach (var manifestItem in manifest.Elements().Where(element => element.Name.LocalName == "item"))
            {
                var properties = ((string?)manifestItem.Attribute("properties") ?? string.Empty)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(property => property != "cover-image")
                    .ToList();
                if ((string?)manifestItem.Attribute("id") == selected.Id) properties.Add("cover-image");
                if (properties.Count == 0) manifestItem.Attribute("properties")?.Remove();
                else manifestItem.SetAttributeValue("properties", string.Join(' ', properties));
            }
        }

        SaveXmlDocument(package, GetSafeDestination(book.WorkingDirectory, book.PackageRelativePath));
        for (var index = 0; index < book.Resources.Count; index++)
        {
            book.Resources[index] = book.Resources[index] with { IsCover = book.Resources[index].Id == selected.Id };
        }
        book.IsPackageDirty = true;
    }

    public void DeleteUnusedResource(EpubBook book, EpubResource resource)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(resource);
        var currentResource = book.Resources.FirstOrDefault(candidate => candidate.Id == resource.Id);
        if (resource.IsNavigation || resource.IsCover || IsXhtmlResource(resource) ||
            currentResource is null ||
            !FindUnusedResourceIds(book).Contains(resource.Id))
        {
            throw new InvalidOperationException("Only a currently unused, non-navigation EPUB resource can be deleted.");
        }

        var workingDirectory = Path.GetFullPath(book.WorkingDirectory);
        var workingDirectoryPrefix = Path.EndsInDirectorySeparator(workingDirectory)
            ? workingDirectory
            : workingDirectory + Path.DirectorySeparatorChar;
        var resourcePath = Path.GetFullPath(currentResource.FullPath);
        if (!resourcePath.StartsWith(workingDirectoryPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("EPUB resource path is outside the working directory.");
        }

        var packagePath = GetSafeDestination(book.WorkingDirectory, book.PackageRelativePath);
        var package = XDocument.Load(packagePath, LoadOptions.PreserveWhitespace);
        var manifest = package.Descendants().FirstOrDefault(element => element.Name.LocalName == "manifest")
            ?? throw new InvalidDataException("EPUB OPF manifest bulunamadı.");
        var item = manifest.Elements().FirstOrDefault(element => (string?)element.Attribute("id") == currentResource.Id)
            ?? throw new InvalidDataException("EPUB kaynağı manifest içinde bulunamadı.");
        item.Remove();
        SaveXmlDocument(package, packagePath);
        book.Resources.RemoveAll(candidate => candidate.Id == currentResource.Id);
        book.IsPackageDirty = true;
        if (File.Exists(resourcePath))
        {
            File.Delete(resourcePath);
        }
    }

    public HashSet<string> FindUnusedResourceIds(EpubBook book)
    {
        var referencedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in book.Sections)
            referencedPaths.Add(Path.GetFullPath(section.FilePath));

        foreach (var source in book.Resources.Where(resource =>
                     (IsXhtmlResource(resource) || resource.MediaType.Equals("text/css", StringComparison.OrdinalIgnoreCase)) &&
                     File.Exists(resource.FullPath)))
        {
            if (IsXhtmlResource(source))
            {
                var openSection = book.Sections.FirstOrDefault(section =>
                    string.Equals(section.FilePath, source.FullPath, StringComparison.OrdinalIgnoreCase));
                var document = XDocument.Load(source.FullPath, LoadOptions.PreserveWhitespace);
                if (openSection is { IsLoaded: true })
                {
                    var body = document.Descendants().First(element => element.Name.LocalName == "body");
                    var fragment = XElement.Parse($"<fragment xmlns=\"{XhtmlNamespace}\">{openSection.ContentXhtml}</fragment>",
                        LoadOptions.PreserveWhitespace);
                    body.ReplaceNodes(fragment.Nodes());
                }
                foreach (var attribute in document.Descendants().Attributes().Where(attribute =>
                             attribute.Name.LocalName is "href" or "src" or "srcset" or "data" or "poster" or "xlink:href"))
                {
                    var references = attribute.Name.LocalName == "srcset"
                        ? attribute.Value.Split(',').Select(candidate => candidate.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty)
                        : [attribute.Value];
                    foreach (var reference in references)
                    {
                        if (TryResolveLocalReference(source.FullPath, reference, out var target))
                            referencedPaths.Add(target);
                    }
                }
            }
            else
            {
                var css = File.ReadAllText(source.FullPath);
                foreach (Match match in Regex.Matches(css,
                             @"url\(\s*['""]?(?<path>[^)'""]+)['""]?\s*\)|@import\s+(?:url\(\s*)?['""](?<import>[^'""]+)['""]",
                             RegexOptions.IgnoreCase))
                {
                    var reference = match.Groups["path"].Success
                        ? match.Groups["path"].Value
                        : match.Groups["import"].Value;
                    if (TryResolveLocalReference(source.FullPath, reference, out var target))
                        referencedPaths.Add(target);
                }
            }
        }

        return book.Resources.Where(resource => !resource.IsNavigation && !resource.IsCover &&
                !referencedPaths.Contains(Path.GetFullPath(resource.FullPath)))
            .Select(resource => resource.Id)
            .ToHashSet(StringComparer.Ordinal);
    }

    public EpubMetadata ReadMetadata(EpubBook book)
    {
        var package = LoadPackage(book);
        var metadata = package.Descendants().First(element => element.Name.LocalName == "metadata");
        string Value(string name) => metadata.Elements().FirstOrDefault(element => element.Name.LocalName == name)?.Value.Trim() ?? string.Empty;
        var rolesByCreatorId = metadata.Elements().Where(element => element.Name.LocalName == "meta" &&
                ((string?)element.Attribute("property")) == "role")
            .Where(element => element.Value is "aut" or "trl" or "edt")
            .GroupBy(element => ((string?)element.Attribute("refines") ?? string.Empty).TrimStart('#'), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Value.Trim(), StringComparer.Ordinal);
        var creators = metadata.Elements().Where(element => element.Name.LocalName == "creator")
            .Select(element => $"{element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "role")?.Value ?? rolesByCreatorId.GetValueOrDefault((string?)element.Attribute("id") ?? string.Empty, "aut")}|{element.Value.Trim()}")
            .Where(value => !value.EndsWith('|'))
            .ToList();
        var seriesMeta = metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "meta" &&
            (string?)element.Attribute("property") == "belongs-to-collection" &&
            (string?)element.Attribute("id") is string collectionId &&
            metadata.Elements().Any(refinement =>
                (string?)refinement.Attribute("refines") == $"#{collectionId}" &&
                (string?)refinement.Attribute("property") == "collection-type" &&
                refinement.Value.Trim() == "series"))
            ?? metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "meta" &&
                (string?)element.Attribute("property") == "belongs-to-collection");
        var legacySeries = metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "meta" &&
            (string?)element.Attribute("name") == "calibre:series");
        var seriesId = (string?)seriesMeta?.Attribute("id");
        var seriesIndex = metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "meta" &&
            (string?)element.Attribute("property") == "group-position" &&
            (string?)element.Attribute("refines") == $"#{seriesId}")?.Value.Trim()
            ?? metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "meta" &&
                (string?)element.Attribute("name") == "calibre:series_index")?.Attribute("content")?.Value
            ?? string.Empty;

        return new EpubMetadata
        {
            Title = Value("title"),
            Creators = string.Join(Environment.NewLine, creators),
            Publisher = Value("publisher"),
            Language = Value("language"),
            Description = Value("description"),
            Identifier = Value("identifier"),
            Date = Value("date"),
            Subjects = string.Join("; ", metadata.Elements().Where(element => element.Name.LocalName == "subject").Select(element => element.Value.Trim())),
            Rights = Value("rights"),
            Series = seriesMeta?.Value.Trim() ?? legacySeries?.Attribute("content")?.Value ?? string.Empty,
            SeriesIndex = seriesIndex
        };
    }

    public void SaveMetadata(EpubBook book, EpubMetadata values)
    {
        var package = LoadPackage(book);
        var metadata = package.Descendants().First(element => element.Name.LocalName == "metadata");
        var isEpub2 = ((string?)package.Attribute("version"))?.StartsWith("2", StringComparison.Ordinal) == true;
        var dcNamespace = metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "title")?.Name.Namespace
            ?? DcNamespace;
        SetDcValue(metadata, dcNamespace, "title", values.Title, required: true);
        SetDcValue(metadata, dcNamespace, "publisher", values.Publisher);
        SetDcValue(metadata, dcNamespace, "language", values.Language);
        SetDcValue(metadata, dcNamespace, "description", values.Description);
        SetDcValue(metadata, dcNamespace, "date", values.Date);
        SetDcValue(metadata, dcNamespace, "rights", values.Rights);

        var creators = values.Creators.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('|', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2 && parts[1].Length > 0 &&
                            (parts[0] is "aut" or "trl" or "edt"))
            .ToArray();
        foreach (var oldCreator in metadata.Elements().Where(element => element.Name.LocalName == "creator").ToArray())
        {
            var id = (string?)oldCreator.Attribute("id");
            oldCreator.Remove();
            if (id is not null)
            {
                metadata.Elements().Where(element => element.Name.LocalName == "meta" &&
                    (string?)element.Attribute("refines") == $"#{id}" &&
                    (string?)element.Attribute("property") == "role").Remove();
            }
        }
        foreach (var creatorParts in creators)
        {
            var role = creatorParts[0];
            var name = creatorParts[1];
            var creator = new XElement(dcNamespace + "creator", name);
            if (isEpub2)
            {
                creator.SetAttributeValue(OpfNamespace + "role", role);
            }
            else
            {
                var id = $"creator-{Guid.NewGuid():N}";
                creator.SetAttributeValue("id", id);
                metadata.Add(creator);
                metadata.Add(new XElement(metadata.Name.Namespace + "meta",
                    new XAttribute("refines", $"#{id}"), new XAttribute("property", "role"), role));
                continue;
            }
            metadata.Add(creator);
        }

        var uniqueIdentifierId = (string?)package.Attribute("unique-identifier");
        var identifierElement = metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "identifier" &&
            (string?)element.Attribute("id") == uniqueIdentifierId)
            ?? metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "identifier");
        if (identifierElement is null)
        {
            if (!string.IsNullOrWhiteSpace(values.Identifier))
                metadata.Add(new XElement(dcNamespace + "identifier", values.Identifier.Trim()));
        }
        else if (!string.IsNullOrWhiteSpace(values.Identifier))
        {
            identifierElement.Value = values.Identifier.Trim();
        }
        var existingSubjects = metadata.Elements().Where(element => element.Name.LocalName == "subject").ToArray();
        foreach (var subject in existingSubjects) subject.Remove();
        foreach (var subject in values.Subjects.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            metadata.Add(new XElement(dcNamespace + "subject", subject));
        }

        var packageRoot = package;
        var oldSeriesIds = metadata.Elements().Where(element =>
                element.Name.LocalName == "meta" &&
                (string?)element.Attribute("property") == "belongs-to-collection" &&
                (string?)element.Attribute("id") is string collectionId &&
                metadata.Elements().Any(refinement =>
                    (string?)refinement.Attribute("refines") == $"#{collectionId}" &&
                    (string?)refinement.Attribute("property") == "collection-type" &&
                    refinement.Value.Trim() == "series"))
            .Select(element => (string?)element.Attribute("id"))
            .Where(id => id is not null)
            .ToHashSet(StringComparer.Ordinal);
        if (oldSeriesIds.Count == 0 && !string.IsNullOrWhiteSpace(ReadMetadata(book).Series))
        {
            var legacyCollectionId = metadata.Elements().FirstOrDefault(element =>
                element.Name.LocalName == "meta" && (string?)element.Attribute("property") == "belongs-to-collection")?
                .Attribute("id")?.Value;
            if (legacyCollectionId is not null) oldSeriesIds.Add(legacyCollectionId);
        }
        var oldSeries = metadata.Elements().Where(element =>
        {
            var name = (string?)element.Attribute("name");
            var property = (string?)element.Attribute("property");
            var refines = ((string?)element.Attribute("refines"))?.TrimStart('#');
            return name is "calibre:series" or "calibre:series_index" ||
                   property == "belongs-to-collection" && oldSeriesIds.Contains((string?)element.Attribute("id") ?? string.Empty) ||
                   property is "group-position" or "collection-type" && refines is not null && oldSeriesIds.Contains(refines);
        }).ToArray();
        foreach (var item in oldSeries) item.Remove();
        if (!string.IsNullOrWhiteSpace(values.Series))
        {
            if (isEpub2)
            {
                metadata.Add(new XElement(metadata.Name.Namespace + "meta", new XAttribute("name", "calibre:series"), new XAttribute("content", values.Series.Trim())));
                if (!string.IsNullOrWhiteSpace(values.SeriesIndex))
                    metadata.Add(new XElement(metadata.Name.Namespace + "meta", new XAttribute("name", "calibre:series_index"), new XAttribute("content", values.SeriesIndex.Trim())));
            }
            else
            {
                var id = $"series-{Guid.NewGuid():N}";
                metadata.Add(new XElement(metadata.Name.Namespace + "meta",
                    new XAttribute("id", id), new XAttribute("property", "belongs-to-collection"), values.Series.Trim()));
                metadata.Add(new XElement(metadata.Name.Namespace + "meta",
                    new XAttribute("refines", $"#{id}"), new XAttribute("property", "collection-type"), "series"));
                if (!string.IsNullOrWhiteSpace(values.SeriesIndex))
                    metadata.Add(new XElement(metadata.Name.Namespace + "meta",
                        new XAttribute("refines", $"#{id}"), new XAttribute("property", "group-position"), values.SeriesIndex.Trim()));
            }
        }

        SavePackage(book, package);
        book.Title = values.Title.Trim();
        book.Metadata = values;
        book.IsPackageDirty = true;
    }

    public List<EpubTocEntry> GenerateTableOfContents(EpubBook book)
    {
        var entries = new List<EpubTocEntry>();
        foreach (var section in book.Sections)
        {
            XDocument? document = null;
            XElement body;
            if (section.IsLoaded)
            {
                body = XElement.Parse($"<fragment xmlns=\"{XhtmlNamespace}\">{section.ContentXhtml}</fragment>", LoadOptions.PreserveWhitespace);
            }
            else
            {
                document = XDocument.Load(section.FilePath, LoadOptions.PreserveWhitespace);
                body = document.Descendants().First(element => element.Name.LocalName == "body");
            }
            var changed = false;
            foreach (var heading in body.Descendants().Where(element =>
                         element.Name.LocalName is "h1" or "h2" or "h3" or "h4" or "h5" or "h6"))
            {
                var title = heading.Value.Trim();
                if (title.Length == 0)
                {
                    continue;
                }

                var id = (string?)heading.Attribute("id");
                if (string.IsNullOrWhiteSpace(id))
                {
                    id = $"toc-{Guid.NewGuid():N}";
                    heading.SetAttributeValue("id", id);
                    changed = true;
                }
                entries.Add(new EpubTocEntry
                {
                    SectionId = section.Id,
                    Href = $"{Path.GetFileName(section.RelativePath)}#{id}",
                    Title = title,
                    Level = int.Parse(heading.Name.LocalName[1..], System.Globalization.CultureInfo.InvariantCulture)
                });
            }

            if (changed)
            {
                if (section.IsLoaded)
                {
                    section.ContentXhtml = string.Concat(body.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting)));
                    section.IsDirty = true;
                }
                else if (document is not null)
                {
                    SaveXmlDocument(document, section.FilePath);
                }
            }

            if (entries.All(entry => entry.SectionId != section.Id))
            {
                entries.Add(new EpubTocEntry
                {
                    SectionId = section.Id,
                    Href = Path.GetFileName(section.RelativePath),
                    Title = section.Title,
                    Level = 1
                });
            }
        }

        book.TableOfContents.Clear();
        book.TableOfContents.AddRange(entries);
        SaveTableOfContents(book);
        return entries;
    }

    public void SaveTableOfContents(EpubBook book)
    {
        var nav = book.Resources.FirstOrDefault(resource => resource.MediaType == "application/xhtml+xml" &&
            resource.IsNavigation && File.Exists(resource.FullPath));
        if (nav is not null)
        {
            var document = XDocument.Load(nav.FullPath, LoadOptions.PreserveWhitespace);
            var toc = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "nav" &&
                ((string?)element.Attribute(EpubNamespace + "type") == "toc" ||
                 (string?)element.Attribute("type") == "toc"));
            if (toc is not null)
            {
                var existingList = toc.Elements().FirstOrDefault(element => element.Name.LocalName == "ol");
                var xhtml = toc.Name.Namespace;
                var list = new XElement(xhtml + "ol");
                var tree = BuildTocTree(book.TableOfContents);
                foreach (var node in tree)
                {
                    list.Add(CreateXhtmlTocItem(node, book, nav.FullPath, xhtml));
                }
                if (existingList is null)
                {
                    toc.Add(list);
                }
                else
                {
                    existingList.ReplaceWith(list);
                }
                SaveXmlDocument(document, nav.FullPath);
            }
        }

        var ncx = book.Resources.FirstOrDefault(resource => resource.MediaType == "application/x-dtbncx+xml" &&
            File.Exists(resource.FullPath));
        if (ncx is not null)
        {
            var document = XDocument.Load(ncx.FullPath, LoadOptions.PreserveWhitespace);
            var navMap = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "navMap");
            if (navMap is not null)
            {
                var ns = navMap.Name.Namespace;
                var order = 0;
                var navPoints = new List<XElement>();
                foreach (var node in BuildTocTree(book.TableOfContents))
                {
                    navPoints.Add(CreateNcxTocItem(node, book, ncx.FullPath, ns, ref order));
                }
                navMap.ReplaceNodes(navPoints);
                SaveXmlDocument(document, ncx.FullPath);
            }
        }

        book.IsPackageDirty = true;
    }

    public int ReplaceText(
        EpubBook book,
        string search,
        string replacement,
        bool matchCase,
        bool wholeWord,
        bool regularExpression,
        EpubSection? onlySection = null)
    {
        var pattern = regularExpression ? search : Regex.Escape(search);
        if (wholeWord)
        {
            pattern = $@"(?<![\p{{L}}\p{{N}}_])(?:{pattern})(?![\p{{L}}\p{{N}}_])";
        }
        var options = RegexOptions.CultureInvariant | (matchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
        var expression = new Regex(pattern, options, TimeSpan.FromSeconds(2));
        var replacements = 0;
        var sections = onlySection is null ? book.Sections : [onlySection];
        foreach (var section in sections)
        {
            var sectionReplacements = 0;
            XDocument document;
            XElement body;
            if (section.IsLoaded)
            {
                var fragment = XElement.Parse($"<fragment xmlns=\"{XhtmlNamespace}\">{section.ContentXhtml}</fragment>", LoadOptions.PreserveWhitespace);
                document = new XDocument(fragment);
                body = fragment;
            }
            else
            {
                document = XDocument.Load(section.FilePath, LoadOptions.PreserveWhitespace);
                body = document.Descendants().First(element => element.Name.LocalName == "body");
            }

            foreach (var text in body.DescendantNodes().OfType<XText>())
            {
                var matches = expression.Matches(text.Value);
                if (matches.Count == 0) continue;
                sectionReplacements += matches.Count;
                text.Value = expression.Replace(text.Value, replacement);
            }
            if (sectionReplacements == 0) continue;

            replacements += sectionReplacements;
            if (!section.IsLoaded)
            {
                SaveXmlDocument(document, section.FilePath);
            }
            section.ContentXhtml = string.Concat(body.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting)));
            section.IsLoaded = true;
            section.IsDirty = true;
            section.WordCount = body.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            section.WordCountKnown = true;
        }
        return replacements;
    }

    public List<EpubSearchResult> SearchText(
        EpubBook book,
        string search,
        bool matchCase,
        bool wholeWord,
        bool regularExpression,
        EpubSection? onlySection = null)
    {
        var pattern = regularExpression ? search : Regex.Escape(search);
        if (wholeWord)
            pattern = $@"(?<![\p{{L}}\p{{N}}_])(?:{pattern})(?![\p{{L}}\p{{N}}_])";
        var options = RegexOptions.CultureInvariant | (matchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
        var expression = new Regex(pattern, options, TimeSpan.FromSeconds(2));
        var results = new List<EpubSearchResult>();
        IEnumerable<EpubSection> sections = onlySection is null ? book.Sections : [onlySection];
        foreach (var section in sections)
        {
            var root = section.IsLoaded
                ? XElement.Parse($"<fragment xmlns=\"{XhtmlNamespace}\">{section.ContentXhtml}</fragment>", LoadOptions.PreserveWhitespace)
                : XDocument.Load(section.FilePath, LoadOptions.PreserveWhitespace)
                    .Descendants().First(element => element.Name.LocalName == "body");
            foreach (var text in root.DescendantNodes().OfType<XText>())
            {
                foreach (Match match in expression.Matches(text.Value))
                {
                    var start = Math.Max(0, match.Index - 45);
                    var length = Math.Min(text.Value.Length - start, match.Length + 90);
                    results.Add(new EpubSearchResult
                    {
                        SectionId = section.Id,
                        SectionTitle = section.Title,
                        Snippet = text.Value.Substring(start, length).Trim(),
                        Match = match.Value
                    });
                    if (results.Count == 1000) return results;
                }
            }
        }
        return results;
    }

    public EpubSection SplitSection(EpubBook book, EpubSection section, string firstContent, string secondContent)
    {
        var firstFragment = XElement.Parse($"<fragment xmlns=\"{XhtmlNamespace}\">{firstContent}</fragment>", LoadOptions.PreserveWhitespace);
        var secondFragment = XElement.Parse($"<fragment xmlns=\"{XhtmlNamespace}\">{secondContent}</fragment>", LoadOptions.PreserveWhitespace);
        var sourceDocument = XDocument.Load(section.FilePath, LoadOptions.PreserveWhitespace);
        var sourceBody = sourceDocument.Descendants().First(element => element.Name.LocalName == "body")
            ?? throw new InvalidDataException("Bölümün body öğesi bulunamadı.");
        sourceBody.ReplaceNodes(firstFragment.Nodes());
        SaveXmlDocument(sourceDocument, section.FilePath);
        section.ContentXhtml = string.Concat(sourceBody.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting)));
        section.WordCount = sourceBody.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        section.WordCountKnown = true;
        section.IsLoaded = true;
        section.IsDirty = true;

        var secondDocument = XDocument.Load(section.FilePath, LoadOptions.PreserveWhitespace);
        var secondBody = secondDocument.Descendants().First(element => element.Name.LocalName == "body");
        secondBody.ReplaceNodes(secondFragment.Nodes());
        var directory = Path.GetDirectoryName(section.RelativePath)!;
        var fileName = $"Section-{Guid.NewGuid():N}.xhtml";
        var relativePath = Path.Combine(directory, fileName).Replace('\\', '/');
        var fullPath = GetSafeDestination(book.WorkingDirectory, relativePath);
        SaveXmlDocument(secondDocument, fullPath);
        var id = $"section-{Guid.NewGuid():N}";
        var title = secondBody.Descendants().FirstOrDefault(element => element.Name.LocalName is "h1" or "h2" or "h3" or "h4" or "h5" or "h6")?.Value.Trim();
        if (string.IsNullOrWhiteSpace(title)) title = $"{section.Title} (devamı)";
        var newSection = new EpubSection
        {
            Id = id,
            RelativePath = relativePath,
            FilePath = fullPath,
            Title = title,
            ContentXhtml = string.Concat(secondBody.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting))),
            WordCount = secondBody.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length,
            WordCountKnown = true,
            IsLoaded = true,
            IsDirty = true
        };
        var packageDirectory = Path.GetDirectoryName(book.PackageRelativePath)?.Replace('\\', '/') ?? string.Empty;
        var href = Path.GetRelativePath(packageDirectory.Length == 0 ? "." : packageDirectory, relativePath).Replace('\\', '/');
        var package = XDocument.Load(GetSafeDestination(book.WorkingDirectory, book.PackageRelativePath), LoadOptions.PreserveWhitespace);
        var manifest = package.Descendants().First(element => element.Name.LocalName == "manifest");
        manifest.Add(new XElement(manifest.Name.Namespace + "item",
            new XAttribute("id", id), new XAttribute("href", href), new XAttribute("media-type", "application/xhtml+xml")));
        var spine = package.Descendants().First(element => element.Name.LocalName == "spine");
        var currentRef = spine.Elements().FirstOrDefault(element => element.Name.LocalName == "itemref" &&
            (string?)element.Attribute("idref") == section.Id);
        if (currentRef is null) throw new InvalidDataException("Bölüm OPF spine içinde bulunamadı.");
        currentRef.AddAfterSelf(new XElement(currentRef.Name,
            new XAttribute("idref", id)));
        SaveXmlDocument(package, GetSafeDestination(book.WorkingDirectory, book.PackageRelativePath));
        var resource = new EpubResource(id, href, "application/xhtml+xml", fullPath);
        book.Resources.Add(resource);
        var sectionIndex = book.Sections.IndexOf(section);
        book.Sections.Insert(sectionIndex + 1, newSection);
        book.IsPackageDirty = true;
        GenerateTableOfContents(book);
        return newSection;
    }

    public void MergeSections(EpubBook book, EpubSection first, EpubSection second)
    {
        var firstFragment = XElement.Parse($"<fragment xmlns=\"{XhtmlNamespace}\">{first.ContentXhtml}</fragment>", LoadOptions.PreserveWhitespace);
        var secondFragment = XElement.Parse($"<fragment xmlns=\"{XhtmlNamespace}\">{second.ContentXhtml}</fragment>", LoadOptions.PreserveWhitespace);
        firstFragment.Add(secondFragment.Nodes());
        first.ContentXhtml = string.Concat(firstFragment.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting)));
        first.WordCount = firstFragment.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        first.WordCountKnown = true;
        first.IsLoaded = true;
        first.IsDirty = true;
        DeleteSection(book, second);
        GenerateTableOfContents(book);
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

    public Task SaveAsync(
        EpubBook book,
        string targetPath,
        bool createBackup = false,
        CancellationToken cancellationToken = default) =>
        SaveCoreAsync(book, targetPath, createBackup, cancellationToken, updateBookState: true);

    public Task CreateRecoverySnapshotAsync(
        EpubBook book,
        string targetPath,
        CancellationToken cancellationToken = default) =>
        SaveCoreAsync(book, targetPath, createBackup: false, cancellationToken, updateBookState: false);

    private async Task SaveCoreAsync(
        EpubBook book,
        string targetPath,
        bool createBackup,
        CancellationToken cancellationToken,
        bool updateBookState)
    {
        foreach (var section in book.Sections.Where(section => section.IsDirty))
        {
            cancellationToken.ThrowIfCancellationRequested();
            XDocument document;
            if (section.RawDocumentXhtml is not null)
            {
                if (updateBookState)
                {
                    document = XDocument.Parse(section.RawDocumentXhtml, LoadOptions.PreserveWhitespace);
                }
                else
                {
                    await File.WriteAllTextAsync(
                        section.FilePath, section.RawDocumentXhtml, new UTF8Encoding(false), cancellationToken);
                    continue;
                }
            }
            else
            {
                document = XDocument.Load(section.FilePath, LoadOptions.PreserveWhitespace);
            }

            var body = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "body")
                ?? throw new InvalidDataException($"'{section.RelativePath}' içinde body öğesi bulunamadı.");
            if (section.IsLoaded)
            {
                var fragment = XElement.Parse(
                    $"<fragment xmlns=\"{XhtmlNamespace}\">{section.ContentXhtml}</fragment>",
                    LoadOptions.PreserveWhitespace);
                body.ReplaceNodes(fragment.Nodes());
            }

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
            if (updateBookState)
            {
                book.PackageFile = fullTargetPath;
                foreach (var section in book.Sections)
                {
                    section.IsDirty = false;
                    section.RawDocumentXhtml = null;
                }
                book.IsPackageDirty = false;
            }
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
                foreach (var anchor in document.Descendants().Where(element => element.Name.LocalName == "a").ToArray())
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
                foreach (var content in document.Descendants().Where(element => element.Name.LocalName == "content").ToArray())
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

    private sealed class TocNode(EpubTocEntry entry)
    {
        public EpubTocEntry Entry { get; } = entry;
        public List<TocNode> Children { get; } = [];
    }

    private static List<TocNode> BuildTocTree(IReadOnlyList<EpubTocEntry> entries)
    {
        var roots = new List<TocNode>();
        var stack = new List<TocNode?> { null };
        var previousLevel = 1;
        foreach (var entry in entries)
        {
            var level = roots.Count == 0 && stack.Count == 1
                ? 1
                : Math.Min(Math.Clamp(entry.Level, 1, 6), previousLevel + 1);
            while (stack.Count > level)
            {
                stack.RemoveAt(stack.Count - 1);
            }

            var node = new TocNode(entry);
            if (stack.Count == 1)
            {
                roots.Add(node);
            }
            else
            {
                stack[^1]!.Children.Add(node);
            }
            stack.Add(node);
            previousLevel = level;
        }
        return roots;
    }

    private static XElement CreateXhtmlTocItem(TocNode node, EpubBook book, string navigationPath, XNamespace xhtml)
    {
        var section = book.Sections.FirstOrDefault(item => item.Id == node.Entry.SectionId);
        var fragmentIndex = node.Entry.Href.IndexOf('#');
        var href = section is null ? node.Entry.Href :
            Path.GetRelativePath(Path.GetDirectoryName(navigationPath)!, section.FilePath).Replace('\\', '/') +
            (fragmentIndex >= 0 ? node.Entry.Href[fragmentIndex..] : string.Empty);
        var item = new XElement(xhtml + "li", new XElement(xhtml + "a",
            new XAttribute("href", href), node.Entry.Title));
        if (node.Children.Count > 0)
        {
            item.Add(new XElement(xhtml + "ol", node.Children.Select(child => CreateXhtmlTocItem(child, book, navigationPath, xhtml))));
        }
        return item;
    }

    private static XElement CreateNcxTocItem(TocNode node, EpubBook book, string ncxPath, XNamespace ncx, ref int order)
    {
        var section = book.Sections.FirstOrDefault(item => item.Id == node.Entry.SectionId);
        var fragmentIndex = node.Entry.Href.IndexOf('#');
        var href = section is null ? node.Entry.Href :
            Path.GetRelativePath(Path.GetDirectoryName(ncxPath)!, section.FilePath).Replace('\\', '/') +
            (fragmentIndex >= 0 ? node.Entry.Href[fragmentIndex..] : string.Empty);
        var point = new XElement(ncx + "navPoint",
            new XAttribute("id", $"navPoint-{++order}"),
            new XAttribute("playOrder", order.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new XElement(ncx + "navLabel", new XElement(ncx + "text", node.Entry.Title)),
            new XElement(ncx + "content", new XAttribute("src", href)));
        foreach (var child in node.Children)
        {
            point.Add(CreateNcxTocItem(child, book, ncxPath, ncx, ref order));
        }
        return point;
    }

    private static XElement LoadPackage(EpubBook book)
    {
        var package = XDocument.Load(GetSafeDestination(book.WorkingDirectory, book.PackageRelativePath), LoadOptions.PreserveWhitespace);
        return package.Root ?? throw new InvalidDataException("OPF package root bulunamadı.");
    }

    private static void SavePackage(EpubBook book, XElement packageRoot)
    {
        var path = GetSafeDestination(book.WorkingDirectory, book.PackageRelativePath);
        SaveXmlDocument(new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), packageRoot), path);
    }

    private static void SetDcValue(XElement metadata, XNamespace dcNamespace, string localName, string value, bool required = false)
    {
        var existing = metadata.Elements().FirstOrDefault(element => element.Name.LocalName == localName);
        if (string.IsNullOrWhiteSpace(value) && !required)
        {
            existing?.Remove();
            return;
        }

        if (existing is null)
        {
            metadata.Add(new XElement(dcNamespace + localName, value.Trim()));
        }
        else
        {
            existing.Value = value.Trim();
        }
    }

    private static EpubResource GetOrCreateStylesheet(EpubBook book)
    {
        var existing = book.Resources.FirstOrDefault(resource =>
            resource.MediaType.Equals("text/css", StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var packageDirectory = Path.GetDirectoryName(book.PackageRelativePath)?.Replace('\\', '/') ?? string.Empty;
        var href = "Styles/book.css";
        var relativePath = string.Join('/', new[] { packageDirectory, href }.Where(part => part.Length > 0));
        var path = GetSafeDestination(book.WorkingDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty, new UTF8Encoding(false));
        var package = LoadPackage(book);
        var manifest = package.Descendants().First(element => element.Name.LocalName == "manifest");
        var id = $"style-{Guid.NewGuid():N}";
        manifest.Add(new XElement(manifest.Name.Namespace + "item",
            new XAttribute("id", id), new XAttribute("href", href), new XAttribute("media-type", "text/css")));
        SavePackage(book, package);
        var resource = new EpubResource(id, href, "text/css", path);
        book.Resources.Add(resource);
        return resource;
    }

    private static void EnsureSectionStylesheetLinks(EpubBook book, EpubResource stylesheet)
    {
        foreach (var section in book.Sections)
        {
            var document = XDocument.Load(section.FilePath, LoadOptions.PreserveWhitespace);
            var head = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "head");
            if (head is null)
            {
                var html = document.Root ?? throw new InvalidDataException($"'{section.RelativePath}' XHTML root bulunamadı.");
                head = new XElement(html.Name.Namespace + "head");
                html.AddFirst(head);
            }
            if (head.Elements().Any(element => element.Name.LocalName == "link" &&
                    (string?)element.Attribute("href") is string href && ResolvesToFile(section.FilePath, href, stylesheet.FullPath)))
                continue;

            var hrefFromSection = Path.GetRelativePath(Path.GetDirectoryName(section.FilePath)!, stylesheet.FullPath).Replace('\\', '/');
            head.Add(new XElement(head.Name.Namespace + "link",
                new XAttribute("rel", "stylesheet"), new XAttribute("type", "text/css"), new XAttribute("href", hrefFromSection)));
            SaveXmlDocument(document, section.FilePath);
            if (section.IsLoaded)
            {
                var body = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "body");
                section.ContentXhtml = body is null ? section.ContentXhtml :
                    string.Concat(body.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting)));
            }
        }
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