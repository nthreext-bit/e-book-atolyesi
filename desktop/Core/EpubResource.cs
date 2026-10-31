namespace KitaplikEditoru.Core;

public sealed record EpubResource(
	string Id,
	string Href,
	string MediaType,
	string FullPath,
	bool IsCover = false,
	bool IsNavigation = false);