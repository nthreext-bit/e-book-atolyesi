using System.Text.Json;

namespace KitaplikEditoru.Infrastructure;

public sealed class AppSettings
{
    public bool CreateBackup { get; set; } = true;
    public bool DarkTheme { get; set; }
    public List<string> RecentFiles { get; set; } = [];
}

public sealed class AppSettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EpubAtolyesi", "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
            }
        }
        catch (Exception exception)
        {
            AppLogger.Write(exception, "Settings load failed");
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var temporaryPath = SettingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, SettingsPath, overwrite: true);
        }
        catch (Exception exception)
        {
            AppLogger.Write(exception, "Settings save failed");
        }
    }
}