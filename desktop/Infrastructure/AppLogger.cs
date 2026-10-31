using System.Text;

namespace KitaplikEditoru.Infrastructure;

public static class AppLogger
{
    private static readonly object SyncRoot = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EpubAtolyesi", "logs");

    public static void Write(Exception exception, string context)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var logPath = Path.Combine(LogDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
            var entry = new StringBuilder()
                .AppendLine($"[{DateTime.Now:O}] {context}")
                .AppendLine(exception.ToString())
                .AppendLine();
            lock (SyncRoot)
            {
                File.AppendAllText(logPath, entry.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
        }
    }
}