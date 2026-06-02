namespace NextCloudShot.Core.Models;

public enum CaptureMode
{
    Region,
    ActiveWindow,
    FullScreen
}

public enum CaptureAction
{
    Region,
    RegionAndShare,
    FullScreen,
    ActiveWindow
}

public sealed record GlobalHotkeySettings(
    bool Enabled,
    string Region,
    string RegionAndShare,
    string FullScreen,
    string ActiveWindow)
{
    public static GlobalHotkeySettings Default { get; } = new(
        true,
        "Ctrl+Shift+1",
        "Ctrl+Shift+2",
        "Ctrl+Shift+3 или PrtScr",
        "Ctrl+Shift+4 или Alt+PrtScr");
}

public sealed record ScreenshotImage(byte[] PngBytes, PixelSize PixelSize, PixelPoint DesktopOrigin, string? WindowTitle = null)
{
    public static ScreenshotImage Empty { get; } = new([], new PixelSize(0, 0), new PixelPoint(0, 0));
}

public sealed record CaptureRequest(CaptureMode Mode);

public sealed class HotkeyPressedEventArgs(CaptureAction action) : EventArgs
{
    public CaptureAction Action { get; } = action;
}
