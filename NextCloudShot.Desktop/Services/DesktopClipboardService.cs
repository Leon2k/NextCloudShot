using Avalonia;
using Avalonia.Input;
using Avalonia.Input.Platform;
using NextCloudShot.Core.Contracts;

namespace NextCloudShot.Desktop.Services;

public sealed class DesktopClipboardService(Func<IClipboard?> clipboardFactory) : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        IClipboard? clipboard = clipboardFactory();
        if (clipboard is not null) await clipboard.SetTextAsync(text);
    }

    public async Task SetImagePngAsync(byte[] pngBytes)
    {
        IClipboard? clipboard = clipboardFactory();
        if (clipboard is null) return;
        DataObject data = new();
        data.Set("image/png", pngBytes);
        await clipboard.SetDataObjectAsync(data);
    }
}
