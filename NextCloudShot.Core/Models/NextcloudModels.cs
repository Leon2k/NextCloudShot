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
    public const string UploadFolder = "/Screenshots";
}

public sealed record ScreenshotUpload(
    string FileName,
    byte[] PngBytes,
    DateTimeOffset CapturedAtUtc);

public sealed record UploadResult(
    string RemotePath,
    Uri? PublicUrl,
    bool LinkCreated);
