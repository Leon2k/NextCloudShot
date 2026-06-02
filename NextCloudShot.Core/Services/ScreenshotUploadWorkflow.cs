using NextCloudShot.Core.Contracts;
using NextCloudShot.Core.Models;

namespace NextCloudShot.Core.Services;

public sealed class ScreenshotUploadWorkflow(
    IAnnotationRenderer renderer,
    INextCloudShotStorageClient storageClient,
    IClipboardService clipboard)
{
    public byte[] Render(ScreenshotDocument document, ScreenshotFileFormat format) => renderer.Render(document, format);

    public Task CopyImageAsync(ScreenshotDocument document) =>
        clipboard.SetImagePngAsync(Render(document, ScreenshotFileFormat.Png));

    public Task<UploadResult> SaveToNextcloudAsync(
        ScreenshotDocument document,
        NextcloudConnectionSettings settings,
        ScreenshotOutputSettings outputSettings,
        CancellationToken cancellationToken = default) =>
        UploadAsync(document, settings with { CreatePublicLink = false }, outputSettings, cancellationToken);

    public async Task<UploadResult> UploadAndCopyLinkAsync(
        ScreenshotDocument document,
        NextcloudConnectionSettings settings,
        ScreenshotOutputSettings outputSettings,
        CancellationToken cancellationToken = default)
    {
        byte[] rendered = Render(document, outputSettings.Format);
        UploadResult result = await UploadAsync(document, settings, outputSettings, cancellationToken, rendered);
        if (result.PublicUrl is not null)
        {
            await clipboard.SetTextAsync(result.PublicUrl.ToString());
        }
        else
        {
            await clipboard.SetImagePngAsync(rendered);
        }

        return result;
    }

    private async Task<UploadResult> UploadAsync(
        ScreenshotDocument document,
        NextcloudConnectionSettings settings,
        ScreenshotOutputSettings outputSettings,
        CancellationToken cancellationToken,
        byte[]? rendered = null)
    {
        rendered ??= Render(document, outputSettings.Format);
        string extension = outputSettings.Format == ScreenshotFileFormat.Jpeg ? "jpg" : "png";
        string contentType = outputSettings.Format == ScreenshotFileFormat.Jpeg ? "image/jpeg" : "image/png";
        string filename = BuildFileName(outputSettings.FileNamePattern, document.Source.WindowTitle, extension);
        ScreenshotUpload upload = new(filename, rendered, contentType, DateTimeOffset.UtcNow);
        return await storageClient.UploadAsync(upload, settings, cancellationToken);
    }

    private static string BuildFileName(string pattern, string? windowTitle, string extension)
    {
        DateTime now = DateTime.Now;
        string stem = pattern switch
        {
            "Дата + время" => now.ToString("yyyy-MM-dd_HH-mm-ss"),
            "Дата + время + Название окна" => $"{now:yyyy-MM-dd_HH-mm-ss}_{windowTitle ?? "Скриншот"}",
            _ => pattern
                .Replace("{date}", now.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
                .Replace("{time}", now.ToString("HH-mm-ss"), StringComparison.OrdinalIgnoreCase)
                .Replace("{window}", windowTitle ?? "Скриншот", StringComparison.OrdinalIgnoreCase)
        };
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            stem = stem.Replace(invalid, '_');
        }

        return $"{stem}.{extension}";
    }
}
