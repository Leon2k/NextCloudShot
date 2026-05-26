using NextCloudShot.Core.Models;

namespace NextCloudShot.Core.Contracts;

public interface IScreenCaptureService
{
    Task<ScreenshotImage> CaptureVirtualDesktopAsync(CancellationToken cancellationToken = default);
    Task<ScreenshotImage> CaptureActiveWindowAsync(CancellationToken cancellationToken = default);
}
