namespace KitaplikEditoru.Core;

public sealed record EpubResource(
	string Id,
	string Href,
	string MediaType,
	string FullPath,
	bool IsCover = false,
	bool IsNavigation = false)
{
	public bool IsUnused { get; init; }
	public string DisplayName => IsUnused ? $"{Href} [kullanılmıyor]" : Href;
}