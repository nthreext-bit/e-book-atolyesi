using System.IO.Compression;
using System.Text;
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
            var addedImage = Assert.Single(reopened.Resources.Where(resource => resource.MediaType == "image/png" && resource.Href.Contains("image-", StringComparison.Ordinal)));
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