namespace KitaplikEditoru.Core;

public sealed class EpubSection
{
    public required string Id { get; init; }
    public required string RelativePath { get; init; }
    public required string FilePath { get; init; }
    public required string Title { get; set; }
    public string ContentXhtml { get; set; } = string.Empty;
    public string? RawDocumentXhtml { get; set; }
    public int WordCount { get; set; }
    public bool WordCountKnown { get; set; }
    public bool IsLoaded { get; set; }
    public bool IsDirty { get; set; }
}