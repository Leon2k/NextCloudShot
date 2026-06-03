using NextCloudShot.Core.Models;

namespace NextCloudShot.Core.Contracts;

public interface IScreenshotFilePresenter
{
    Task ShowInFolderAsync(LocalScreenshotResult result, CancellationToken cancellationToken = default);
}
