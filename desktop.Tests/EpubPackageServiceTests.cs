using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using KitaplikEditoru.Core;
using Xunit;

namespace EpubStudio.Tests;

public sealed class EpubPackageServiceTests
{
    [Theory]
    [InlineData("2.0")]
    [InlineData("3.0")]
    public async Task UnchangedRoundTripPreservesContentAndUnknownFiles(string version)
    {
        var fixturePath = await CreateFixtureAsync(version);
        var outputPath = Path.Combine(Path.GetTempPath(), $"roundtrip-{Guid.NewGuid():N}.epub");
        var service = new EpubPackageService();

        try
        {
            using var book = await service.OpenAsync(fixturePath);
            Assert.Single(book.Sections);
            Assert.False(book.Sections[0].IsLoaded);
            await service.LoadSectionAsync(book.Sections[0]);
            Assert.Contains("Original paragraph", book.Sections[0].ContentXhtml);
            Assert.False(book.IsDirty);

            await service.SaveAsync(book, outputPath);

            using var source = ZipFile.OpenRead(fixturePath);
            using var output = ZipFile.OpenRead(outputPath);
            Assert.Equal("mimetype", output.Entries[0].FullName);
            Assert.Equal(output.Entries[0].Length, output.Entries[0].CompressedLength);
            Assert.Equal(await ReadEntryAsync(source, "OEBPS/Text/chapter.xhtml"), await ReadEntryAsync(output, "OEBPS/Text/chapter.xhtml"));
            Assert.Equal(await ReadEntryAsync(source, "OEBPS/content.opf"), await ReadEntryAsync(output, "OEBPS/content.opf"));
            Assert.Equal(await ReadEntryAsync(source, "META-INF/rights.xml"), await ReadEntryAsync(output, "META-INF/rights.xml"));
            Assert.Equal(await ReadEntryAsync(source, "OEBPS/Images/sample.png"), await ReadEntryAsync(output, "OEBPS/Images/sample.png"));
        }
        finally
        {
            File.Delete(fixturePath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task EditedXhtmlCanBeSavedAndReopened()
    {
        var fixturePath = await CreateFixtureAsync("3.0");
        var outputPath = Path.Combine(Path.GetTempPath(), $"edited-{Guid.NewGuid():N}.epub");
        var service = new EpubPackageService();

        try
        {
            using (var book = await service.OpenAsync(fixturePath))
            {
                await service.LoadSectionAsync(book.Sections[0]);
                book.Sections[0].ContentXhtml += "<p>Edited paragraph</p><table><tbody><tr><td>Cell</td></tr></tbody></table><aside xmlns:epub=\"http://www.idpf.org/2007/ops\" epub:type=\"footnote\" id=\"note-1\">Footnote</aside>";
                book.Sections[0].IsDirty = true;
                await service.SaveAsync(book, outputPath);
            }

            using var reopened = await service.OpenAsync(outputPath);
            await service.LoadSectionAsync(reopened.Sections[0]);
            Assert.Contains("Edited paragraph", reopened.Sections[0].ContentXhtml);
            Assert.Contains("<table", reopened.Sections[0].ContentXhtml);
            Assert.Contains("epub:type=\"footnote\"", reopened.Sections[0].ContentXhtml);
            Assert.Contains(reopened.Resources, resource => resource.Href == "Images/sample.png");
        }
        finally
        {
            File.Delete(fixturePath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task NewBookCreatesReadableEpub3WithNavAndNcx()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"new-book-{Guid.NewGuid():N}.epub");
        var service = new EpubPackageService();

        try
        {
            using (var book = await service.CreateNewAsync())
            {
                await service.SaveAsync(book, outputPath);
            }

            using var archive = ZipFile.OpenRead(outputPath);
            Assert.Equal("mimetype", archive.Entries[0].FullName);
            Assert.NotNull(archive.GetEntry("OEBPS/nav.xhtml"));
            Assert.NotNull(archive.GetEntry("OEBPS/toc.ncx"));
            using var reopened = await service.OpenAsync(outputPath);
            Assert.Equal("Yeni kitap", reopened.Title);
            Assert.Single(reopened.Sections);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task SaveOptionCreatesBackupOfExistingTarget()
    {
        var fixturePath = await CreateFixtureAsync("3.0");
        var outputPath = Path.Combine(Path.GetTempPath(), $"backup-{Guid.NewGuid():N}.epub");
        var service = new EpubPackageService();
        await File.WriteAllTextAsync(outputPath, "previous target");

        try
        {
            using var book = await service.OpenAsync(fixturePath);
            await service.SaveAsync(book, outputPath, createBackup: true);
            Assert.Equal("previous target", await File.ReadAllTextAsync(outputPath + ".bak"));
            using var savedArchive = ZipFile.OpenRead(outputPath);
            Assert.True(savedArchive.Entries.Count > 0);
        }
        finally
        {
            File.Delete(fixturePath);
            File.Delete(outputPath);
            File.Delete(outputPath + ".bak");
        }
    }

    [Fact]
    public async Task RenameUpdatesXhtmlAndNavigationWithoutDroppingCustomOpfData()
    {
        var fixturePath = await CreateFixtureAsync("3.0");
        var outputPath = Path.Combine(Path.GetTempPath(), $"renamed-{Guid.NewGuid():N}.epub");
        var service = new EpubPackageService();

        try
        {
            using (var book = await service.OpenAsync(fixturePath))
            {
                await service.LoadSectionAsync(book.Sections[0]);
                book.Sections[0].ContentXhtml += "<p>Unsaved note</p>";
                book.Sections[0].IsDirty = true;
                service.RenameSection(book, book.Sections[0], "Yeni Başlık");
                await service.SaveAsync(book, outputPath);
            }

            using var reopened = await service.OpenAsync(outputPath);
            await service.LoadSectionAsync(reopened.Sections[0]);
            Assert.Equal("Yeni Başlık", reopened.Sections[0].Title);
            Assert.Contains("Unsaved note", reopened.Sections[0].ContentXhtml);
            using var archive = ZipFile.OpenRead(outputPath);
            Assert.Contains("Yeni Başlık", Encoding.UTF8.GetString(await ReadEntryAsync(archive, "OEBPS/nav.xhtml")));
            var package = Encoding.UTF8.GetString(await ReadEntryAsync(archive, "OEBPS/content.opf"));
            Assert.Contains("urn:vendor:metadata", package);
            Assert.Contains("keep-me", package);
        }
        finally
        {
            File.Delete(fixturePath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task ReorderAndDeleteUpdateSpineAndNavigation()
    {
        var fixturePath = await CreateFixtureAsync("3.0", includeSecondSection: true);
        var reorderedPath = Path.Combine(Path.GetTempPath(), $"reordered-{Guid.NewGuid():N}.epub");
        var deletedPath = Path.Combine(Path.GetTempPath(), $"deleted-{Guid.NewGuid():N}.epub");
        var service = new EpubPackageService();

        try
        {
            using (var book = await service.OpenAsync(fixturePath))
            {
                Assert.Equal(2, book.Sections.Count);
                var reverseOrder = book.Sections.AsEnumerable().Reverse().Select(section => section.Id).ToArray();
                service.ReorderSpine(book, reverseOrder);
                await service.SaveAsync(book, reorderedPath);
            }

            using (var reordered = await service.OpenAsync(reorderedPath))
            {
                Assert.Equal("second", reordered.Sections[0].Id);
                service.DeleteSection(reordered, reordered.Sections[1]);
                await service.SaveAsync(reordered, deletedPath);
            }

            using var finalBook = await service.OpenAsync(deletedPath);
            Assert.Single(finalBook.Sections);
            Assert.Equal("second", finalBook.Sections[0].Id);
            using var finalArchive = ZipFile.OpenRead(deletedPath);
            Assert.DoesNotContain("chapter.xhtml", Encoding.UTF8.GetString(await ReadEntryAsync(finalArchive, "OEBPS/nav.xhtml")));
        }
        finally
        {
            File.Delete(fixturePath);
            File.Delete(reorderedPath);
            File.Delete(deletedPath);
        }
    }

    [Fact]
    public async Task StyleChangesPreserveUnknownCssAndAddStylesheetLinks()
    {
        var fixturePath = await CreateFixtureAsync("3.0", includeSecondSection: true);
        var outputPath = Path.Combine(Path.GetTempPath(), $"styled-{Guid.NewGuid():N}.epub");
        var service = new EpubPackageService();

        try
        {
            using var book = await service.OpenAsync(fixturePath);
            var style = new EpubStyle { Name = "Alıntı", ClassName = "alinti" };
            style.Properties["font-style"] = "italic";
            style.Properties["margin-top"] = "1em";
            service.SaveStyle(book, style);

            var css = await File.ReadAllTextAsync(book.Resources.Single(resource => resource.MediaType == "text/css").FullPath);
            Assert.Contains("body { font-family: serif; }", css);
            Assert.Contains("font-style: italic", css);
            Assert.Contains(".alinti", css);
            Assert.Contains(service.ReadStyles(book), item => item.ClassName == "alinti");
            foreach (var section in book.Sections)
            {
                var xhtml = await File.ReadAllTextAsync(section.FilePath);
                Assert.Contains("book.css", xhtml);
            }
            await service.SaveAsync(book, outputPath);
            using var reopened = await service.OpenAsync(outputPath);
            await service.LoadSectionAsync(reopened.Sections[1]);
            Assert.Contains("Second section", reopened.Sections[1].ContentXhtml);
        }
        finally
        {
            File.Delete(fixturePath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task MetadataChangesPreserveUnknownOpfElementsAndSupportCreatorRoles()
    {
        var fixturePath = await CreateFixtureAsync("3.0");
        var outputPath = Path.Combine(Path.GetTempPath(), $"metadata-{Guid.NewGuid():N}.epub");
        var service = new EpubPackageService();

        try
        {
            using (var book = await service.OpenAsync(fixturePath))
            {
                var metadata = service.ReadMetadata(book);
                metadata.Title = "Yeni başlık";
                metadata.Creators = "aut|Ayşe Yazar\ntrl|Mehmet Çevirmen";
                metadata.Series = "Örnek Seri";
                metadata.SeriesIndex = "2";
                service.SaveMetadata(book, metadata);
                await service.SaveAsync(book, outputPath);
            }

            using var reopened = await service.OpenAsync(outputPath);
            var savedMetadata = service.ReadMetadata(reopened);
            Assert.Equal("Yeni başlık", savedMetadata.Title);
            Assert.Contains("aut|Ayşe Yazar", savedMetadata.Creators);
            Assert.Contains("trl|Mehmet Çevirmen", savedMetadata.Creators);
            Assert.Equal("Örnek Seri", savedMetadata.Series);
            Assert.Equal("2", savedMetadata.SeriesIndex);
            using var archive = ZipFile.OpenRead(outputPath);
            var opf = Encoding.UTF8.GetString(await ReadEntryAsync(archive, "OEBPS/content.opf"));
            Assert.Contains("urn:vendor:metadata", opf);
            Assert.Contains("keep-me", opf);
        }
        finally
        {
            File.Delete(fixturePath);
            File.Delete(outputPath);
        }
    }

    [Theory]
    [InlineData("2.0")]
    [InlineData("3.0")]
    public async Task CoverImageUpdatesTheVersionSpecificCoverMetadata(string version)
    {
        var fixturePath = await CreateFixtureAsync(version);
        var service = new EpubPackageService();

        try
        {
            using var book = await service.OpenAsync(fixturePath);
            var image = book.Resources.Single(resource => resource.MediaType == "image/png");
            service.SetCoverImage(book, image);

            var package = XDocument.Load(Path.Combine(book.WorkingDirectory, book.PackageRelativePath));
            if (version == "2.0")
            {
                Assert.Contains(package.Descendants().Where(element => element.Name.LocalName == "meta"),
                    element => (string?)element.Attribute("name") == "cover" &&
                               (string?)element.Attribute("content") == image.Id);
            }
            else
            {
                var item = package.Descendants().Single(element => (string?)element.Attribute("id") == image.Id);
                Assert.Contains("cover-image", ((string?)item.Attribute("properties"))?.Split(' ') ?? []);
            }
            Assert.True(book.Resources.Single(resource => resource.Id == image.Id).IsCover);
        }
        finally
        {
            File.Delete(fixturePath);
        }
    }

    [Fact]
    public async Task GeneratedTocUpdatesBothNavigationFormats()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"generated-toc-{Guid.NewGuid():N}.epub");
        var service = new EpubPackageService();

        try
        {
            using (var book = await service.CreateNewAsync())
            {
                book.Sections[0].ContentXhtml = "<h1>Başlangıç</h1><h2>İlk konu</h2>";
                book.Sections[0].IsLoaded = true;
                book.Sections[0].IsDirty = true;
                var entries = service.GenerateTableOfContents(book);
                Assert.Equal(2, entries.Count);
                Assert.Equal(2, entries[1].Level);
                entries[1].Title = "Düzenlenmiş konu";
                service.SaveTableOfContents(book);
                await service.SaveAsync(book, outputPath);
            }

            using var archive = ZipFile.OpenRead(outputPath);
            var nav = Encoding.UTF8.GetString(await ReadEntryAsync(archive, "OEBPS/nav.xhtml"));
            var ncx = Encoding.UTF8.GetString(await ReadEntryAsync(archive, "OEBPS/toc.ncx"));
            Assert.Contains("Düzenlenmiş konu", nav);
            Assert.Contains("Düzenlenmiş konu", ncx);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task FindReplaceTouchesTextNodesOnlyAndSplitMergeKeepsSpineConsistent()
    {
        var fixturePath = await CreateFixtureAsync("3.0", includeSecondSection: true);
        var outputPath = Path.Combine(Path.GetTempPath(), $"split-merge-{Guid.NewGuid():N}.epub");
        var service = new EpubPackageService();

        try
        {
            using (var book = await service.OpenAsync(fixturePath))
            {
                var replacements = service.ReplaceText(book, "Original", "Revised", true, false, false);
                Assert.Equal(1, replacements);
                var first = book.Sections[0];
                await service.LoadSectionAsync(book.Sections[1]);
                var split = service.SplitSection(book, first,
                    "<h1>Part One</h1><p>First half</p>", "<h1>Part Two</h1><p>Second half</p>");
                Assert.Equal(3, book.Sections.Count);
                service.MergeSections(book, first, split);
                Assert.Equal(2, book.Sections.Count);
                Assert.Contains("First half", first.ContentXhtml);
                Assert.Contains("Second half", first.ContentXhtml);
                await service.SaveAsync(book, outputPath);
            }

            using var reopened = await service.OpenAsync(outputPath);
            Assert.Equal(2, reopened.Sections.Count);
            await service.LoadSectionAsync(reopened.Sections[0]);
            Assert.Contains("First half", reopened.Sections[0].ContentXhtml);
            Assert.Contains("Second half", reopened.Sections[0].ContentXhtml);
            using var archive = ZipFile.OpenRead(outputPath);
            var package = XDocument.Load(archive.GetEntry("OEBPS/content.opf")!.Open());
            var spineIds = package.Descendants().Where(element => element.Name.LocalName == "itemref")
                .Select(element => (string?)element.Attribute("idref")).ToArray();
            Assert.Equal(reopened.Sections.Select(section => section.Id), spineIds);
        }
        finally
        {
            File.Delete(fixturePath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task AddImageCopiesAssetAndAddsManifestEntry()
    {
        var fixturePath = await CreateFixtureAsync("3.0");
        var imagePath = Path.Combine(Path.GetTempPath(), $"image-{Guid.NewGuid():N}.png");
        var outputPath = Path.Combine(Path.GetTempPath(), $"image-added-{Guid.NewGuid():N}.epub");
        await File.WriteAllBytesAsync(imagePath, [137, 80, 78, 71, 13, 10, 26, 10]);
        var service = new EpubPackageService();

        try
        {
            using (var book = await service.OpenAsync(fixturePath))
            {
                await service.LoadSectionAsync(book.Sections[0]);
                var packagePath = await service.AddImageAsync(book, imagePath);
                var relativeImagePath = Path.GetRelativePath(
                    Path.GetDirectoryName(book.Sections[0].RelativePath)!, packagePath).Replace('\\', '/');
                book.Sections[0].ContentXhtml += $"<img src=\"{relativeImagePath}\" alt=\"Added image\" />";
                book.Sections[0].IsDirty = true;
                await service.SaveAsync(book, outputPath);
            }

            using var reopened = await service.OpenAsync(outputPath);
            var addedImage = Assert.Single(reopened.Resources, resource =>
                resource.MediaType == "image/png" && resource.Href.Contains("image-", StringComparison.Ordinal));
            Assert.True(File.Exists(addedImage.FullPath));
            await service.LoadSectionAsync(reopened.Sections[0]);
            Assert.Contains(addedImage.Href.Split('/').Last(), reopened.Sections[0].ContentXhtml);
        }
        finally
        {
            File.Delete(fixturePath);
            File.Delete(imagePath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task ValidatorAcceptsWellFormedEpubAndReportsBrokenArchives()
    {
        var fixturePath = await CreateFixtureAsync("3.0");
        var invalidPath = Path.Combine(Path.GetTempPath(), $"invalid-{Guid.NewGuid():N}.epub");
        await File.WriteAllTextAsync(invalidPath, "not a zip archive");
        var validator = new EpubValidator();

        try
        {
            var validIssues = await validator.ValidateAsync(fixturePath);
            Assert.DoesNotContain(validIssues, issue => issue.Severity == EpubIssueSeverity.Error);

            var invalidIssues = await validator.ValidateAsync(invalidPath);
            Assert.Contains(invalidIssues, issue => issue.Severity == EpubIssueSeverity.Error);
        }
        finally
        {
            File.Delete(fixturePath);
            File.Delete(invalidPath);
        }
    }

    [Fact]
    public async Task ValidatorReportsMissingLocalContentResources()
    {
        var epubPath = Path.Combine(Path.GetTempPath(), $"broken-reference-{Guid.NewGuid():N}.epub");
        var service = new EpubPackageService();

        try
        {
            using var book = await service.CreateNewAsync();
            book.Sections[0].ContentXhtml += "<img src=\"../Images/missing.png\" />";
            book.Sections[0].IsLoaded = true;
            book.Sections[0].IsDirty = true;
            await service.CreateRecoverySnapshotAsync(book, epubPath);

            var issues = await new EpubValidator().ValidateAsync(epubPath);

            Assert.Contains(issues, issue =>
                issue.Severity == EpubIssueSeverity.Error &&
                issue.Message.Contains("missing", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(epubPath);
        }
    }

    [Fact]
    public async Task TextImportAndExportPreservesParagraphsAndSanitizesXhtml()
    {
        var fixturePath = await CreateFixtureAsync("3.0");
        var textPath = Path.Combine(Path.GetTempPath(), $"import-{Guid.NewGuid():N}.txt");
        var xhtmlPath = Path.Combine(Path.GetTempPath(), $"import-{Guid.NewGuid():N}.xhtml");
        var exportPath = Path.Combine(Path.GetTempPath(), $"export-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(textPath, "First paragraph\n\nSecond paragraph");
        await File.WriteAllTextAsync(xhtmlPath,
            "<html xmlns=\"http://www.w3.org/1999/xhtml\"><body><p onclick=\"alert(1)\">Safe <em>formatted</em> text</p><p>Second paragraph</p><script>unsafe()</script></body></html>");
        var service = new EpubPackageService();

        try
        {
            using var book = await service.OpenAsync(fixturePath);
            var section = book.Sections[0];
            await service.ImportSectionContentAsync(section, textPath);
            Assert.Contains("First paragraph", section.ContentXhtml);
            Assert.Contains("Second paragraph", section.ContentXhtml);
            Assert.Contains("<br", section.ContentXhtml);
            Assert.True(section.IsDirty);

            await service.ImportSectionContentAsync(section, xhtmlPath);
            Assert.Contains("Safe", section.ContentXhtml);
            Assert.DoesNotContain("onclick", section.ContentXhtml);
            Assert.DoesNotContain("<script", section.ContentXhtml);
            await service.ExportSectionAsync(section, exportPath);
            Assert.Equal($"Safe formatted text{Environment.NewLine}Second paragraph", await File.ReadAllTextAsync(exportPath));
        }
        finally
        {
            File.Delete(fixturePath);
            File.Delete(textPath);
            File.Delete(xhtmlPath);
            File.Delete(exportPath);
        }
    }

    [Fact]
    public async Task RecoverySnapshotKeepsSourceDirtyStateAndCanBeReopened()
    {
        var snapshotPath = Path.Combine(Path.GetTempPath(), $"recovery-{Guid.NewGuid():N}.epub");
        var service = new EpubPackageService();

        try
        {
            using var book = await service.CreateNewAsync();
            var sourcePath = book.PackageFile;
            book.Sections[0].ContentXhtml = "<h1>Recovered heading</h1><p>Recovered text</p>";
            book.Sections[0].IsLoaded = true;
            book.Sections[0].IsDirty = true;

            await service.CreateRecoverySnapshotAsync(book, snapshotPath);

            Assert.True(book.IsDirty);
            Assert.Equal(sourcePath, book.PackageFile);
            using var recovered = await service.OpenAsync(snapshotPath);
            await service.LoadSectionAsync(recovered.Sections[0]);
            Assert.Contains("Recovered heading", recovered.Sections[0].ContentXhtml);
            Assert.Contains("Recovered text", recovered.Sections[0].ContentXhtml);
        }
        finally
        {
            File.Delete(snapshotPath);
        }
    }

    private static async Task<string> CreateFixtureAsync(string version, bool includeSecondSection = false)
    {
        var path = Path.Combine(Path.GetTempPath(), $"fixture-{Guid.NewGuid():N}.epub");
        var packageDocument = version == "2.0"
            ? """
              <?xml version="1.0" encoding="UTF-8"?>
                            <package xmlns="http://www.idpf.org/2007/opf" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:vendor="urn:vendor:metadata" version="2.0" unique-identifier="book-id">
                                <metadata><dc:identifier id="book-id">urn:uuid:test-book</dc:identifier><dc:title>Fixture Book</dc:title><dc:language>tr</dc:language><vendor:extra>keep-me</vendor:extra></metadata>
                <manifest><item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml"/><item id="chapter" href="Text/chapter.xhtml" media-type="application/xhtml+xml"/><item id="image" href="Images/sample.png" media-type="image/png"/><item id="css" href="Styles/book.css" media-type="text/css"/></manifest>
                <spine toc="ncx"><itemref idref="chapter"/></spine>
              </package>
              """
            : """
              <?xml version="1.0" encoding="UTF-8"?>
                            <package xmlns="http://www.idpf.org/2007/opf" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:vendor="urn:vendor:metadata" version="3.0" unique-identifier="book-id">
                                <metadata><dc:identifier id="book-id">urn:uuid:test-book</dc:identifier><dc:title>Fixture Book</dc:title><dc:language>tr</dc:language><vendor:extra>keep-me</vendor:extra></metadata>
                <manifest><item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/><item id="chapter" href="Text/chapter.xhtml" media-type="application/xhtml+xml"/><item id="image" href="Images/sample.png" media-type="image/png"/><item id="css" href="Styles/book.css" media-type="text/css"/></manifest>
                <spine><itemref idref="chapter"/></spine>
              </package>
              """;
        var entries = new Dictionary<string, byte[]>
        {
            ["META-INF/container.xml"] = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><container xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\" version=\"1.0\"><rootfiles><rootfile full-path=\"OEBPS/content.opf\" media-type=\"application/oebps-package+xml\"/></rootfiles></container>"),
            ["META-INF/rights.xml"] = Encoding.UTF8.GetBytes("<rights>Keep this unknown file</rights>"),
            ["OEBPS/content.opf"] = Encoding.UTF8.GetBytes(packageDocument),
            ["OEBPS/Text/chapter.xhtml"] = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>Chapter</title><link rel=\"stylesheet\" href=\"../Styles/book.css\" /></head><body><h1>Chapter</h1><p>Original paragraph</p><img src=\"../Images/sample.png\" alt=\"sample\" /></body></html>"),
            ["OEBPS/Images/sample.png"] = [137, 80, 78, 71, 13, 10, 26, 10],
            ["OEBPS/Styles/book.css"] = Encoding.UTF8.GetBytes("body { font-family: serif; }")
        };
        if (version == "2.0")
        {
            entries["OEBPS/toc.ncx"] = Encoding.UTF8.GetBytes("<ncx xmlns=\"http://www.daisy.org/z3986/2005/ncx/\" version=\"2005-1\"><navMap/></ncx>");
        }
        else
        {
            entries["OEBPS/nav.xhtml"] = Encoding.UTF8.GetBytes("<html xmlns=\"http://www.w3.org/1999/xhtml\"><body><nav><ol><li><a href=\"Text/chapter.xhtml\">Chapter</a></li></ol></nav></body></html>");
        }

        if (includeSecondSection)
        {
            packageDocument = packageDocument
                .Replace("</manifest>", "<item id=\"second\" href=\"Text/second.xhtml\" media-type=\"application/xhtml+xml\"/></manifest>", StringComparison.Ordinal)
                .Replace("</spine>", "<itemref idref=\"second\"/></spine>", StringComparison.Ordinal);
            entries["OEBPS/content.opf"] = Encoding.UTF8.GetBytes(packageDocument);
            entries["OEBPS/Text/second.xhtml"] = Encoding.UTF8.GetBytes("<html xmlns=\"http://www.w3.org/1999/xhtml\"><body><h1>Second</h1><p>Second section</p></body></html>");
            entries["OEBPS/nav.xhtml"] = Encoding.UTF8.GetBytes("<html xmlns=\"http://www.w3.org/1999/xhtml\"><body><nav><ol><li><a href=\"Text/chapter.xhtml\">Chapter</a></li><li><a href=\"Text/second.xhtml\">Second</a></li></ol></nav></body></html>");
        }

        await using var file = File.Create(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);
        var mimetype = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
        await using (var stream = mimetype.Open())
        await using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            await writer.WriteAsync("application/epub+zip");
        }
        foreach (var entry in entries)
        {
            var zipEntry = archive.CreateEntry(entry.Key, CompressionLevel.Optimal);
            await using var stream = zipEntry.Open();
            await stream.WriteAsync(entry.Value);
        }

        return path;
    }

    private static async Task<byte[]> ReadEntryAsync(ZipArchive archive, string name)
    {
        await using var stream = archive.GetEntry(name)!.Open();
        using var output = new MemoryStream();
        await stream.CopyToAsync(output);
        return output.ToArray();
    }
}