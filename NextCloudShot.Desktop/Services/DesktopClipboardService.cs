using Avalonia;
using Avalonia.Input;
using Avalonia.Input.Platform;
using NextCloudShot.Core.Contracts;
using SkiaSharp;
using System.Runtime.InteropServices;

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
        if (OperatingSystem.IsWindows() && await TrySetWindowsDibAsync(pngBytes))
        {
            return;
        }

        IClipboard? clipboard = clipboardFactory();
        if (clipboard is null) return;
        DataObject data = new();
        data.Set("image/png", pngBytes);
        await clipboard.SetDataObjectAsync(data);
    }

    private static async Task<bool> TrySetWindowsDibAsync(byte[] pngBytes)
    {
        byte[]? dib = CreateDib(pngBytes);
        if (dib is null) return false;

        IntPtr handle = GlobalAlloc(GlobalMoveable, (UIntPtr)dib.Length);
        if (handle == IntPtr.Zero) return false;

        bool transferred = false;
        try
        {
            IntPtr memory = GlobalLock(handle);
            if (memory == IntPtr.Zero) return false;
            try
            {
                Marshal.Copy(dib, 0, memory, dib.Length);
            }
            finally
            {
                GlobalUnlock(handle);
            }

            for (int attempt = 0; attempt < 8; attempt++)
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        EmptyClipboard();
                        transferred = SetClipboardData(ClipboardFormatDib, handle) != IntPtr.Zero;
                        return transferred;
                    }
                    finally
                    {
                        CloseClipboard();
                    }
                }

                await Task.Delay(40);
            }

            return false;
        }
        finally
        {
            if (!transferred)
            {
                GlobalFree(handle);
            }
        }
    }

    private static byte[]? CreateDib(byte[] pngBytes)
    {
        using SKBitmap? bitmap = SKBitmap.Decode(pngBytes);
        if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0) return null;

        const int headerSize = 40;
        int stride = bitmap.Width * 4;
        byte[] dib = new byte[headerSize + stride * bitmap.Height];
        WriteInt32(dib, 0, headerSize);
        WriteInt32(dib, 4, bitmap.Width);
        WriteInt32(dib, 8, bitmap.Height);
        WriteInt16(dib, 12, 1);
        WriteInt16(dib, 14, 32);
        WriteInt32(dib, 16, 0);
        WriteInt32(dib, 20, stride * bitmap.Height);

        for (int y = 0; y < bitmap.Height; y++)
        {
            int targetY = bitmap.Height - 1 - y;
            for (int x = 0; x < bitmap.Width; x++)
            {
                SKColor color = bitmap.GetPixel(x, y);
                int offset = headerSize + targetY * stride + x * 4;
                dib[offset] = color.Blue;
                dib[offset + 1] = color.Green;
                dib[offset + 2] = color.Red;
                dib[offset + 3] = 255;
            }
        }

        return dib;
    }

    private static void WriteInt32(byte[] target, int offset, int value) =>
        BitConverter.GetBytes(value).CopyTo(target, offset);

    private static void WriteInt16(byte[] target, int offset, short value) =>
        BitConverter.GetBytes(value).CopyTo(target, offset);

    private const uint GlobalMoveable = 0x0002;
    private const uint ClipboardFormatDib = 8;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);
}
