using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KitaplikEditoru.Core;

namespace KitaplikEditoru.Infrastructure;

public sealed record EpubRecoverySnapshot(
    string Id,
    string EpubPath,
    string? SourcePath,
    string BookTitle,
    DateTimeOffset UpdatedAt);

public sealed class EpubRecoveryService(EpubPackageService packageService)
{
    private static readonly string RecoveryDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EpubAtolyesi", "Recovery");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<EpubRecoverySnapshot> SaveAsync(
        EpubBook book,
        string? sourcePath,
        EpubRecoverySnapshot? existing = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(RecoveryDirectory);
        var id = existing?.Id ?? GetRecoveryId(sourcePath);
        var epubPath = Path.Combine(RecoveryDirectory, $"{id}.epub");
        var temporaryEpubPath = epubPath + ".tmp";
        try
        {
            await packageService.CreateRecoverySnapshotAsync(book, temporaryEpubPath, cancellationToken);
            File.Move(temporaryEpubPath, epubPath, overwrite: true);
            var snapshot = new EpubRecoverySnapshot(id, epubPath, sourcePath, book.Title, DateTimeOffset.UtcNow);
            var metadataPath = Path.Combine(RecoveryDirectory, $"{id}.json");
            var temporaryMetadataPath = metadataPath + ".tmp";
            await File.WriteAllTextAsync(temporaryMetadataPath, JsonSerializer.Serialize(snapshot, JsonOptions), cancellationToken);
            File.Move(temporaryMetadataPath, metadataPath, overwrite: true);
            return snapshot;
        }
        finally
        {
            if (File.Exists(temporaryEpubPath))
                File.Delete(temporaryEpubPath);
        }
    }

    public IReadOnlyList<EpubRecoverySnapshot> FindAll()
    {
        if (!Directory.Exists(RecoveryDirectory))
            return [];

        var snapshots = new List<EpubRecoverySnapshot>();
        foreach (var metadataPath in Directory.EnumerateFiles(RecoveryDirectory, "*.json"))
        {
            try
            {
                var snapshot = JsonSerializer.Deserialize<EpubRecoverySnapshot>(File.ReadAllText(metadataPath));
                if (snapshot is not null && IsValidSnapshot(snapshot))
                    snapshots.Add(snapshot);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
            {
                AppLogger.Write(exception, $"Recovery metadata could not be read: {metadataPath}");
            }
        }

        return snapshots.OrderByDescending(snapshot => snapshot.UpdatedAt).ToArray();
    }

    public void Delete(EpubRecoverySnapshot snapshot)
    {
        var metadataPath = Path.Combine(RecoveryDirectory, $"{snapshot.Id}.json");
        if (File.Exists(snapshot.EpubPath))
            File.Delete(snapshot.EpubPath);
        if (File.Exists(metadataPath))
            File.Delete(metadataPath);
    }

    private static string GetRecoveryId(string? sourcePath)
    {
        var key = string.IsNullOrWhiteSpace(sourcePath)
            ? Guid.NewGuid().ToString("N")
            : Path.GetFullPath(sourcePath).ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
    }

    private static bool IsValidSnapshot(EpubRecoverySnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Id) || string.IsNullOrWhiteSpace(snapshot.EpubPath) ||
            snapshot.Id.Length != 64 || snapshot.Id.Any(character => !Uri.IsHexDigit(character)))
            return false;
        var expectedPath = Path.GetFullPath(Path.Combine(RecoveryDirectory, $"{snapshot.Id}.epub"));
        if (!Path.GetFullPath(snapshot.EpubPath).Equals(expectedPath, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(expectedPath))
            return false;
        if (string.IsNullOrWhiteSpace(snapshot.SourcePath))
            return true;
        return Path.IsPathFullyQualified(snapshot.SourcePath) &&
               Path.GetExtension(snapshot.SourcePath).Equals(".epub", StringComparison.OrdinalIgnoreCase);
    }
}
