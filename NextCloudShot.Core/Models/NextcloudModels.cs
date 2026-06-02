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

public sealed record ScreenshotUpload(
    string FileName,
    byte[] PngBytes,
    DateTimeOffset CapturedAtUtc);

public sealed record UploadResult(
    string RemotePath,
    Uri? PublicUrl,
    bool LinkCreated);
