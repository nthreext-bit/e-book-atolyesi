namespace KitaplikEditoru.Core;

public sealed class EpubMetadata
{
    public string Title { get; set; } = string.Empty;
    public string Creators { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Subjects { get; set; } = string.Empty;
    public string Rights { get; set; } = string.Empty;
    public string Series { get; set; } = string.Empty;
    public string SeriesIndex { get; set; } = string.Empty;
}

public sealed class EpubTocEntry
{
    public required string SectionId { get; init; }
    public required string Href { get; set; }
    public required string Title { get; set; }
    public int Level { get; set; }
}
