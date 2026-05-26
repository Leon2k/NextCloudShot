using NextCloudShot.Core.Models;

namespace NextCloudShot.Core.Contracts;

public interface IAnnotationRenderer
{
    byte[] RenderPng(ScreenshotDocument document);
}
