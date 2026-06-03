namespace NextCloudShot.Core.Models;

public sealed record NextcloudConnectionSettings(
    Uri ServerUri,
    string Username,
    string AppPassword,
    string UploadFolder,
    bool CreatePublicLink,
    int? ShareExpiryDays = null);

public static class NextcloudDefaults
{
    public const string UploadFolder = "/Скриншоты";
    public const string EnglishUploadFolder = "/Screenshots";

    public static string GetUploadFolder(string? language) =>
        language?.StartsWith("ru", StringComparison.OrdinalIgnoreCase) == true
            ? UploadFolder
            : EnglishUploadFolder;

    public static bool IsDefaultUploadFolder(string folder) =>
        string.Equals(folder, UploadFolder, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(folder, EnglishUploadFolder, StringComparison.OrdinalIgnoreCase);
}

public sealed record NextcloudConnectionInfo(string? Language);

public enum ScreenshotFileFormat
{
    Png,
    Jpeg
}

public sealed record ScreenshotOutputSettings(
    string FileNamePattern,
    ScreenshotFileFormat Format)
{
    public static ScreenshotOutputSettings Default { get; } = new("Дата + время", ScreenshotFileFormat.Png);
}

public sealed record ScreenshotUpload(
    string FileName,
    byte[] Bytes,
    string ContentType,
    DateTimeOffset CapturedAtUtc);

public sealed record UploadResult(
    string RemotePath,
    Uri? PublicUrl,
    bool LinkCreated);

public sealed record LocalScreenshotResult(
    string LocalPath,
    string RemotePath,
    string FileName);
