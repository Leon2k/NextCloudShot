namespace NextCloudShot.Core.Models;

public enum AnnotationTool
{
    Crop,
    Rectangle,
    Arrow,
    Pen,
    Text,
    Pixelate
}

public abstract record Annotation(Guid Id);

public sealed record RectangleAnnotation(Guid Id, PixelRect Bounds, string Color, double Thickness) : Annotation(Id);

public sealed record ArrowAnnotation(Guid Id, PixelPoint From, PixelPoint To, string Color, double Thickness) : Annotation(Id);

public sealed record PenAnnotation(Guid Id, IReadOnlyList<PixelPoint> Points, string Color, double Thickness) : Annotation(Id);

public sealed record TextAnnotation(Guid Id, PixelPoint Position, string Text, string Color, double FontSize) : Annotation(Id);

public sealed record PixelateAnnotation(Guid Id, PixelRect Bounds, int BlockSize) : Annotation(Id);

public sealed class ScreenshotDocument
{
    private readonly List<Annotation> _annotations = [];

    public ScreenshotDocument(ScreenshotImage source)
    {
        Source = source;
        Crop = new PixelRect(0, 0, source.PixelSize.Width, source.PixelSize.Height);
    }

    public ScreenshotImage Source { get; }
    public PixelRect Crop { get; set; }
    public IReadOnlyList<Annotation> Annotations => _annotations;

    public void Add(Annotation annotation) => _annotations.Add(annotation);
    public bool Remove(Guid id) => _annotations.RemoveAll(a => a.Id == id) > 0;
    public void ClearAnnotations() => _annotations.Clear();
}
