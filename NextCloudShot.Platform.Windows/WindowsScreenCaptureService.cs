using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using NextCloudShot.Core.Contracts;
using NextCloudShot.Core.Models;

namespace NextCloudShot.Platform.Windows;

public sealed class WindowsScreenCaptureService : IScreenCaptureService
{
    public Task<ScreenshotImage> CaptureVirtualDesktopAsync(CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        Rectangle bounds = new(
            Win32.GetSystemMetrics(Win32.SM_XVIRTUALSCREEN),
            Win32.GetSystemMetrics(Win32.SM_YVIRTUALSCREEN),
            Win32.GetSystemMetrics(Win32.SM_CXVIRTUALSCREEN),
            Win32.GetSystemMetrics(Win32.SM_CYVIRTUALSCREEN));
        return Task.FromResult(Capture(bounds, GetForegroundWindowTitle()));
    }

    public Task<ScreenshotImage> CaptureActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        nint foreground = Win32.GetForegroundWindow();
        if (foreground == nint.Zero || !Win32.GetWindowRect(foreground, out Win32.Rect rect))
        {
            throw new InvalidOperationException("Unable to resolve the foreground window bounds.");
        }

        Rectangle bounds = new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        return Task.FromResult(Capture(bounds, GetWindowTitle(foreground)));
    }

    private static ScreenshotImage Capture(Rectangle bounds, string? windowTitle)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new InvalidOperationException("The screen capture bounds are empty.");
        }

        using Bitmap bitmap = new(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
        }

        using MemoryStream stream = new();
        bitmap.Save(stream, ImageFormat.Png);
        return new ScreenshotImage(stream.ToArray(), new PixelSize(bounds.Width, bounds.Height), new PixelPoint(bounds.X, bounds.Y), windowTitle);
    }

    private static string? GetForegroundWindowTitle() => GetWindowTitle(Win32.GetForegroundWindow());

    private static string? GetWindowTitle(nint window)
    {
        int length = Win32.GetWindowTextLengthW(window);
        if (length <= 0) return null;
        StringBuilder text = new(length + 1);
        return Win32.GetWindowTextW(window, text, text.Capacity) > 0 ? text.ToString() : null;
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Screen capture is implemented for Windows in the initial milestone.");
        }
    }
}
