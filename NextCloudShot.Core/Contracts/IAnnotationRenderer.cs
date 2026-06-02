using NextCloudShot.Core.Models;

namespace NextCloudShot.Core.Contracts;

public interface IAnnotationRenderer
{
    byte[] Render(ScreenshotDocument document, ScreenshotFileFormat format);
}
