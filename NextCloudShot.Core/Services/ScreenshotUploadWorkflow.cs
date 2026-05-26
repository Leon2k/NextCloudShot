using NextCloudShot.Core.Contracts;
using NextCloudShot.Core.Models;

namespace NextCloudShot.Core.Services;

public sealed class ScreenshotUploadWorkflow(
    IAnnotationRenderer renderer,
    INextCloudShotStorageClient storageClient,
    IClipboardService clipboard)
{
    public async Task<UploadResult> UploadAndCopyLinkAsync(
        ScreenshotDocument document,
        NextcloudConnectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        byte[] rendered = renderer.RenderPng(document);
        string filename = $"Screenshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
        ScreenshotUpload upload = new(filename, rendered, DateTimeOffset.UtcNow);

        UploadResult result = await storageClient.UploadAsync(upload, settings, cancellationToken);
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
}
