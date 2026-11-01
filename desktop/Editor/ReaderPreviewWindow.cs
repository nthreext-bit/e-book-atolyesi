using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Windows;
using System.Xml.Linq;
using KitaplikEditoru.Core;
using KitaplikEditoru.Infrastructure;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace KitaplikEditoru.Editor;

public sealed class ReaderPreviewWindow : Window
{
    private const string XhtmlNamespace = "http://www.w3.org/1999/xhtml";
    private const long MaximumEmbeddedImageBytes = 24 * 1024 * 1024;
    private const int MaximumPreviewCharacters = 48 * 1024 * 1024;
    private readonly WebView2 _webView = new();
    private readonly string _html;
    private string? _previewDirectory;

    public ReaderPreviewWindow(string title, string html)
    {
        Title = title;
        Width = 1000;
        Height = 820;
        MinWidth = 640;
        MinHeight = 480;
        Content = _webView;
        _html = html;
        Loaded += Window_Loaded;
        Closed += Window_Closed;
    }

    public static string BuildHtml(EpubBook book)
    {
        var sections = book.Sections;
        var sectionIds = sections.Select((section, index) => (section.RelativePath, Id: $"chapter-{index + 1}"))
            .ToDictionary(item => item.RelativePath, item => item.Id, StringComparer.OrdinalIgnoreCase);
        var imageCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        long embeddedImageBytes = 0;
        var language = string.IsNullOrWhiteSpace(book.Metadata.Language) ? "und" : book.Metadata.Language;
        var output = new StringBuilder("<!doctype html><html lang=\"")
            .Append(HtmlEncoder.Default.Encode(language))
            .AppendLine("\"><head><meta charset=\"utf-8\">")
            .Append("""
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; style-src 'unsafe-inline' data:; font-src data:">
            <style>
            body{max-width:760px;margin:32px auto;padding:0 24px;font:18px/1.75 Georgia,serif;color:#262626}
            nav{border-bottom:1px solid #bbb;padding-bottom:1em;margin-bottom:3em}nav li{margin:.35em 0}
            article{margin:0 auto 5em;overflow-wrap:anywhere}article+article{border-top:1px solid #bbb;padding-top:3em}
            img{max-width:100%;height:auto}table{border-collapse:collapse;max-width:100%}td,th{border:1px solid #888;padding:.35em .6em}
            blockquote{margin-left:1.5em;color:#555}
            </style>
            """);

        foreach (var resource in book.Resources.Where(resource =>
                     resource.MediaType.Equals("text/css", StringComparison.OrdinalIgnoreCase) &&
                     File.Exists(resource.FullPath)))
        {
            var file = new FileInfo(resource.FullPath);
            if (file.Length > 2 * 1024 * 1024)
                continue;
            var css = File.ReadAllText(resource.FullPath);
            css = Regex.Replace(css, @"@import\b[^;]*;?", string.Empty, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            css = Regex.Replace(css, @"url\s*\([^)]*\)", "none", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            css = Regex.Replace(css, @"expression\s*\([^)]*\)", "none", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            output.Append("<link rel=\"stylesheet\" href=\"data:text/css;base64,")
                .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(css)))
                .Append("\">");
            EnsurePreviewSize(output);
        }

        output.Append("</head><body><nav aria-label=\"Contents\"><h1>")
            .Append(HtmlEncoder.Default.Encode(book.Title))
            .Append("</h1><ol>");
        foreach (var section in sections.Select((section, index) => (section, index)))
        {
            output.Append("<li><a href=\"#chapter-").Append(section.index + 1).Append("\">")
                .Append(HtmlEncoder.Default.Encode(section.section.Title))
                .Append("</a></li>");
        }
        output.Append("</ol></nav>");

        foreach (var (section, index) in sections.Select((section, index) => (section, index)))
        {
            var fragment = XElement.Parse($"<fragment xmlns=\"{XhtmlNamespace}\">{section.ContentXhtml}</fragment>",
                LoadOptions.PreserveWhitespace);
            SanitizeFragment(fragment, book, section, sections, sectionIds, imageCache, ref embeddedImageBytes);
            output.Append("<article id=\"chapter-").Append(index + 1).Append("\"><h1>")
                .Append(HtmlEncoder.Default.Encode(section.Title))
                .Append("</h1>")
                .Append(string.Concat(fragment.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting))))
                .Append("</article>");
            EnsurePreviewSize(output);
        }

        return output.Append("</body></html>").ToString();
    }

    private static void SanitizeFragment(
        XElement fragment,
        EpubBook book,
        EpubSection section,
        IReadOnlyList<EpubSection> sections,
        IReadOnlyDictionary<string, string> sectionIds,
        IDictionary<string, string> imageCache,
        ref long embeddedImageBytes)
    {
        fragment.Descendants().Where(element =>
                element.Name.LocalName is "script" or "iframe" or "object" or "embed" or "form" or "base" or "link" or "meta" or "style" or "audio" or "video")
            .Remove();

        foreach (var element in fragment.DescendantsAndSelf())
        {
            foreach (var attribute in element.Attributes().ToArray())
            {
                var name = attribute.Name.LocalName;
                var value = attribute.Value.Trim();
                if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("style", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("srcdoc", StringComparison.OrdinalIgnoreCase))
                {
                    attribute.Remove();
                    continue;
                }

                if (name.Equals("src", StringComparison.OrdinalIgnoreCase))
                {
                    var dataUri = TryReadImageDataUri(book, section, value, imageCache, ref embeddedImageBytes);
                    if (dataUri is null) attribute.Remove();
                    else attribute.Value = dataUri;
                }
                else if (name.Equals("href", StringComparison.OrdinalIgnoreCase))
                {
                    if (value.StartsWith('#'))
                        continue;
                    if (TryResolveSectionHref(section, value, sections, sectionIds, out var internalHref))
                        attribute.Value = internalHref;
                    else
                        attribute.Remove();
                }
                else if (name.Equals("srcset", StringComparison.OrdinalIgnoreCase) ||
                         name.Equals("xlink:href", StringComparison.OrdinalIgnoreCase))
                {
                    attribute.Remove();
                }
            }
        }
    }

    private static string? TryReadImageDataUri(
        EpubBook book,
        EpubSection section,
        string source,
        IDictionary<string, string> imageCache,
        ref long embeddedImageBytes)
    {
        if (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            Uri.TryCreate(source, UriKind.Absolute, out _))
            return null;

        try
        {
            var relativePath = Path.GetFullPath(Path.Combine(
                book.WorkingDirectory,
                Path.GetDirectoryName(section.RelativePath) ?? string.Empty,
                Uri.UnescapeDataString(source.Split('#', '?')[0]).Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithinDirectory(book.WorkingDirectory, relativePath))
                return null;
            if (imageCache.TryGetValue(relativePath, out var cachedImage))
                return cachedImage;
            var resource = book.Resources.FirstOrDefault(item =>
                Path.GetFullPath(item.FullPath).Equals(relativePath, StringComparison.OrdinalIgnoreCase));
            if (resource is null || !resource.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                resource.MediaType.Equals("image/svg+xml", StringComparison.OrdinalIgnoreCase))
                return null;

            var info = new FileInfo(resource.FullPath);
            if (!info.Exists || info.Length > 8 * 1024 * 1024 ||
                embeddedImageBytes + info.Length > MaximumEmbeddedImageBytes)
                return null;
            var dataUri = $"data:{resource.MediaType};base64,{Convert.ToBase64String(File.ReadAllBytes(resource.FullPath))}";
            imageCache[relativePath] = dataUri;
            embeddedImageBytes += info.Length;
            return dataUri;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or UriFormatException)
        {
            return null;
        }
    }

    private static void EnsurePreviewSize(StringBuilder output)
    {
        if (output.Length > MaximumPreviewCharacters)
            throw new InvalidDataException("The generated reader preview exceeds the 48 MB size limit.");
    }

    private static bool TryResolveSectionHref(
        EpubSection source,
        string href,
        IReadOnlyList<EpubSection> sections,
        IReadOnlyDictionary<string, string> sectionIds,
        out string result)
    {
        result = string.Empty;
        if (Uri.TryCreate(href, UriKind.Absolute, out _))
            return false;
        try
        {
            var filePart = Uri.UnescapeDataString(href.Split('#', '?')[0]);
            var targetPath = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(source.FilePath)!,
                filePart.Replace('/', Path.DirectorySeparatorChar)));
            var target = sections.FirstOrDefault(section =>
                Path.GetFullPath(section.FilePath).Equals(targetPath, StringComparison.OrdinalIgnoreCase));
            if (target is null || !sectionIds.TryGetValue(target.RelativePath, out var targetId))
                return false;
            var fragment = href.Contains('#') ? href[href.IndexOf('#')..] : string.Empty;
            result = $"#{targetId}{fragment}";
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or UriFormatException)
        {
            return false;
        }
    }

    private static bool IsWithinDirectory(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var environment = await WebView2EnvironmentService.CreateAsync();
            await _webView.EnsureCoreWebView2Async(environment);
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.IsScriptEnabled = false;
            _previewDirectory = Path.Combine(Path.GetTempPath(), "EpubAtolyesi", "Previews", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_previewDirectory);
            var previewPath = Path.Combine(_previewDirectory, "index.html");
            await File.WriteAllTextAsync(previewPath, _html, new UTF8Encoding(false));
            _webView.CoreWebView2.Navigate(new Uri(previewPath).AbsoluteUri);
        }
        catch (Exception exception)
        {
            AppLogger.Write(exception, "Reader preview failed to initialize");
            MessageBox.Show(this, exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        if (_previewDirectory is null || !Directory.Exists(_previewDirectory))
            return;
        try
        {
            Directory.Delete(_previewDirectory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppLogger.Write(exception, "Reader preview temporary files could not be removed");
        }
    }
}
