using NextCloudShot.Core.Models;

namespace NextCloudShot.Core.Contracts;

public interface ILocalScreenshotStore
{
    Task<LocalScreenshotResult> SaveAsync(
        ScreenshotUpload upload,
        NextcloudConnectionSettings settings,
        CancellationToken cancellationToken = default);
}
