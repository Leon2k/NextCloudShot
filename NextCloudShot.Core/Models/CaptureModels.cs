namespace NextCloudShot.Core.Models;

public enum CaptureMode
{
    Region,
    ActiveWindow,
    FullScreen
}

public sealed record ScreenshotImage(byte[] PngBytes, PixelSize PixelSize, PixelPoint DesktopOrigin)
{
    public static ScreenshotImage Empty { get; } = new([], new PixelSize(0, 0), new PixelPoint(0, 0));
}

public sealed record CaptureRequest(CaptureMode Mode);

public sealed class HotkeyPressedEventArgs(CaptureMode mode) : EventArgs
{
    public CaptureMode Mode { get; } = mode;
}
