using NextCloudShot.Core.Contracts;
using NextCloudShot.Core.Models;
using SkiaSharp;

namespace NextCloudShot.Desktop.Services;

public sealed class SkiaAnnotationRenderer : IAnnotationRenderer
{
    public byte[] RenderPng(ScreenshotDocument document)
    {
        using SKBitmap source = SKBitmap.Decode(document.Source.PngBytes)
            ?? throw new InvalidOperationException("Unable to decode source screenshot PNG.");
        SKRectI crop = ToClampedRect(document.Crop.Normalize(), source.Width, source.Height);
        if (crop.Width <= 0 || crop.Height <= 0)
        {
            throw new InvalidOperationException("Crop area is empty.");
        }

        using SKBitmap output = new(crop.Width, crop.Height, true);
        using SKCanvas canvas = new(output);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(source, new SKRect(crop.Left, crop.Top, crop.Right, crop.Bottom), new SKRect(0, 0, crop.Width, crop.Height));

        foreach (Annotation annotation in document.Annotations)
        {
            DrawAnnotation(canvas, output, annotation, crop.Left, crop.Top);
        }

        using SKImage image = SKImage.FromBitmap(output);
        using SKData encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private static void DrawAnnotation(SKCanvas canvas, SKBitmap bitmap, Annotation annotation, int offsetX, int offsetY)
    {
        switch (annotation)
        {
            case RectangleAnnotation rectangle:
                using (SKPaint paint = StrokePaint(rectangle.Color, rectangle.Thickness))
                    canvas.DrawRect(Offset(rectangle.Bounds, offsetX, offsetY), paint);
                break;
            case ArrowAnnotation arrow:
                using (SKPaint paint = StrokePaint(arrow.Color, arrow.Thickness))
                    DrawArrow(canvas, Offset(arrow.From, offsetX, offsetY), Offset(arrow.To, offsetX, offsetY), paint);
                break;
            case PenAnnotation pen:
                using (SKPaint paint = StrokePaint(pen.Color, pen.Thickness))
                using (SKPath path = new())
                {
                    if (pen.Points.Count == 0) break;
                    SKPoint first = Offset(pen.Points[0], offsetX, offsetY);
                    path.MoveTo(first);
                    foreach (PixelPoint point in pen.Points.Skip(1)) path.LineTo(Offset(point, offsetX, offsetY));
                    canvas.DrawPath(path, paint);
                }
                break;
            case TextAnnotation text:
                using (SKPaint paint = new() { Color = Parse(text.Color), TextSize = (float)text.FontSize, IsAntialias = true })
                {
                    SKPoint point = Offset(text.Position, offsetX, offsetY);
                    canvas.DrawText(text.Text, point.X, point.Y + (float)text.FontSize, paint);
                }
                break;
            case PixelateAnnotation pixelate:
                ApplyPixelation(canvas, bitmap, Offset(pixelate.Bounds, offsetX, offsetY), pixelate.BlockSize);
                break;
        }
    }

    private static void ApplyPixelation(SKCanvas canvas, SKBitmap bitmap, SKRect region, int blockSize)
    {
        SKRectI clipped = new(
            Math.Clamp((int)region.Left, 0, bitmap.Width),
            Math.Clamp((int)region.Top, 0, bitmap.Height),
            Math.Clamp((int)region.Right, 0, bitmap.Width),
            Math.Clamp((int)region.Bottom, 0, bitmap.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;

        using SKBitmap fragment = new(clipped.Width, clipped.Height);
        if (!bitmap.ExtractSubset(fragment, clipped)) return;
        int smallWidth = Math.Max(1, clipped.Width / Math.Max(2, blockSize));
        int smallHeight = Math.Max(1, clipped.Height / Math.Max(2, blockSize));
        using SKBitmap? small = fragment.Resize(new SKImageInfo(smallWidth, smallHeight), SKFilterQuality.None);
        if (small is null) return;
        using SKPaint paint = new() { FilterQuality = SKFilterQuality.None, IsAntialias = false };
        canvas.DrawBitmap(small, new SKRect(clipped.Left, clipped.Top, clipped.Right, clipped.Bottom), paint);
    }

    private static SKPaint StrokePaint(string color, double thickness) => new()
    {
        Color = Parse(color),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = (float)thickness,
        IsAntialias = true,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round
    };

    private static void DrawArrow(SKCanvas canvas, SKPoint from, SKPoint to, SKPaint paint)
    {
        canvas.DrawLine(from, to, paint);
        double angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
        const float length = 18;
        SKPoint left = new(to.X - length * (float)Math.Cos(angle - Math.PI / 6), to.Y - length * (float)Math.Sin(angle - Math.PI / 6));
        SKPoint right = new(to.X - length * (float)Math.Cos(angle + Math.PI / 6), to.Y - length * (float)Math.Sin(angle + Math.PI / 6));
        canvas.DrawLine(to, left, paint);
        canvas.DrawLine(to, right, paint);
    }

    private static SKRect Offset(PixelRect rect, int x, int y) => new((float)(rect.X - x), (float)(rect.Y - y), (float)(rect.Right - x), (float)(rect.Bottom - y));
    private static SKPoint Offset(PixelPoint point, int x, int y) => new((float)(point.X - x), (float)(point.Y - y));
    private static SKColor Parse(string color) => SKColor.Parse(color);

    private static SKRectI ToClampedRect(PixelRect rect, int width, int height) => new(
        Math.Clamp((int)Math.Floor(rect.X), 0, width),
        Math.Clamp((int)Math.Floor(rect.Y), 0, height),
        Math.Clamp((int)Math.Ceiling(rect.Right), 0, width),
        Math.Clamp((int)Math.Ceiling(rect.Bottom), 0, height));
}
