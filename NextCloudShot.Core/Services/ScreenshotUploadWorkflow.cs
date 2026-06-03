using NextCloudShot.Core.Contracts;
using NextCloudShot.Core.Models;

namespace NextCloudShot.Core.Services;

public sealed class ScreenshotUploadWorkflow(
    IAnnotationRenderer renderer,
    INextCloudShotStorageClient storageClient,
    IClipboardService clipboard,
    ILocalScreenshotStore localStore,
    IScreenshotFilePresenter filePresenter)
{
    public byte[] Render(ScreenshotDocument document, ScreenshotFileFormat format) => renderer.Render(document, format);

    public async Task<LocalScreenshotResult> CopyImageAsync(
        ScreenshotDocument document,
        NextcloudConnectionSettings settings,
        ScreenshotOutputSettings outputSettings,
        CancellationToken cancellationToken = default)
    {
        LocalScreenshotResult saved = await SaveLocalAsync(document, settings, outputSettings, cancellationToken);
        await clipboard.SetImagePngAsync(Render(document, ScreenshotFileFormat.Png));
        return saved;
    }

    public async Task<LocalScreenshotResult> SaveToNextcloudAsync(
        ScreenshotDocument document,
        NextcloudConnectionSettings settings,
        ScreenshotOutputSettings outputSettings,
        bool showInFolder = false,
        CancellationToken cancellationToken = default)
    {
        LocalScreenshotResult saved = await SaveLocalAsync(document, settings, outputSettings, cancellationToken);
        if (showInFolder)
        {
            await filePresenter.ShowInFolderAsync(saved, cancellationToken);
        }

        return saved;
    }

    public async Task<UploadResult> UploadAndCopyLinkAsync(
        ScreenshotDocument document,
        NextcloudConnectionSettings settings,
        ScreenshotOutputSettings outputSettings,
        CancellationToken cancellationToken = default)
    {
        byte[] rendered = Render(document, outputSettings.Format);
        ScreenshotUpload upload = CreateUpload(document, outputSettings, rendered);
        await localStore.SaveAsync(upload, settings, cancellationToken);
        UploadResult result = await storageClient.UploadAsync(upload, settings, cancellationToken);
        if (result.PublicUrl is not null)
        {
            await clipboard.SetTextAsync(result.PublicUrl.ToString());
        }
        else
        {
            await clipboard.SetImagePngAsync(Render(document, ScreenshotFileFormat.Png));
        }

        return result;
    }

    private async Task<LocalScreenshotResult> SaveLocalAsync(
        ScreenshotDocument document,
        NextcloudConnectionSettings settings,
        ScreenshotOutputSettings outputSettings,
        CancellationToken cancellationToken,
        byte[]? rendered = null)
    {
        rendered ??= Render(document, outputSettings.Format);
        ScreenshotUpload upload = CreateUpload(document, outputSettings, rendered);
        return await localStore.SaveAsync(upload, settings, cancellationToken);
    }

    private static ScreenshotUpload CreateUpload(
        ScreenshotDocument document,
        ScreenshotOutputSettings outputSettings,
        byte[] rendered)
    {
        string extension = outputSettings.Format == ScreenshotFileFormat.Jpeg ? "jpg" : "png";
        string contentType = outputSettings.Format == ScreenshotFileFormat.Jpeg ? "image/jpeg" : "image/png";
        string filename = BuildFileName(outputSettings.FileNamePattern, document.Source.WindowTitle, extension);
        return new ScreenshotUpload(filename, rendered, contentType, DateTimeOffset.UtcNow);
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
