using NextCloudShot.Core.Models;

namespace NextCloudShot.Core.Contracts;

public interface ICloudShotStorageClient
{
    Task TestConnectionAsync(NextcloudConnectionSettings settings, CancellationToken cancellationToken = default);
    Task<UploadResult> UploadAsync(
        ScreenshotUpload upload,
        NextcloudConnectionSettings settings,
        CancellationToken cancellationToken = default);
}
