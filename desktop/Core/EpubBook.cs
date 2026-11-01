namespace KitaplikEditoru.Core;

public sealed class EpubBook : IDisposable
{
    public required string PackageFile { get; set; }
    public required string WorkingDirectory { get; init; }
    public required string PackageRelativePath { get; init; }
    public required string Title { get; set; }
    public List<EpubSection> Sections { get; } = [];
    public List<EpubResource> Resources { get; } = [];
    public List<EpubTocEntry> TableOfContents { get; } = [];
    public EpubMetadata Metadata { get; set; } = new();
    public bool IsPackageDirty { get; set; }
    public bool IsDirty => IsPackageDirty || Sections.Any(section => section.IsDirty);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(WorkingDirectory))
            {
                Directory.Delete(WorkingDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}