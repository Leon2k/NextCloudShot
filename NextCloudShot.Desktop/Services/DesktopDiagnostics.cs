namespace NextCloudShot.Desktop.Services;

public static class DesktopDiagnostics
{
    private static readonly object SyncRoot = new();
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NextCloudShot",
        "nextcloudshot.log");

    public static void Write(string message)
    {
        lock (SyncRoot)
        {
            string? directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
    }
}
