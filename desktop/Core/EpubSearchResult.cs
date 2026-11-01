namespace KitaplikEditoru.Core;

public sealed class EpubSearchResult
{
    public required string SectionId { get; init; }
    public required string SectionTitle { get; init; }
    public required string Snippet { get; init; }
    public required string Match { get; init; }
}
