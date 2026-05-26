namespace NextCloudShot.Core.Contracts;

public interface IClipboardService
{
    Task SetTextAsync(string text);
    Task SetImagePngAsync(byte[] pngBytes);
}
