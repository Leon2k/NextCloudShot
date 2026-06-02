using NextCloudShot.Core.Contracts;
using NextCloudShot.Core.Models;
using SkiaSharp;

namespace NextCloudShot.Desktop.Services;

public sealed class SkiaAnnotationRenderer : IAnnotationRenderer
{
    public byte[] Render(ScreenshotDocument document, ScreenshotFileFormat format)
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
        using SKData encoded = image.Encode(
            format == ScreenshotFileFormat.Jpeg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png,
            format == ScreenshotFileFormat.Jpeg ? 92 : 100);
        return encoded.ToArray();
    }

    private static void DrawAnnotation(SKCanvas canvas, SKBitmap bitmap, Annotation annotation, int offsetX, int offsetY)
    {
        switch (annotation)
        {
            case RectangleAnnotation rectangle:
                using (SKPaint paint = StrokePaint(rectangle.Color, rectangle.Thickness))
                    DrawShape(canvas, Offset(rectangle.Bounds, offsetX, offsetY), paint, rectangle.Style);
                break;
            case ArrowAnnotation arrow:
                using (SKPaint paint = StrokePaint(arrow.Color, arrow.Thickness))
                    DrawArrow(canvas, Offset(arrow.From, offsetX, offsetY), Offset(arrow.To, offsetX, offsetY), arrow.Control is null ? null : Offset(arrow.Control.Value, offsetX, offsetY), paint, arrow.Style);
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

    private static void DrawShape(SKCanvas canvas, SKRect bounds, SKPaint paint, ShapeStyle style)
    {
        switch (style)
        {
            case ShapeStyle.Ellipse:
                canvas.DrawOval(bounds, paint);
                break;
            case ShapeStyle.Cloud:
                paint.PathEffect = SKPathEffect.CreateDash([1, Math.Max(3, paint.StrokeWidth)], 0);
                canvas.DrawRect(bounds, paint);
                break;
            case ShapeStyle.Line:
                canvas.DrawLine(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom, paint);
                break;
            default:
                canvas.DrawRect(bounds, paint);
                break;
        }
    }

    private static void DrawArrow(SKCanvas canvas, SKPoint from, SKPoint to, SKPoint? control, SKPaint paint, ArrowStyle style)
    {
        if (style == ArrowStyle.Triangle)
        {
            DrawTriangleArrow(canvas, from, to, paint.Color, paint.StrokeWidth);
            return;
        }

        if (style == ArrowStyle.Dotted)
        {
            paint.PathEffect = SKPathEffect.CreateDash([1, Math.Max(7, paint.StrokeWidth * 2)], 0);
            paint.StrokeWidth = Math.Max(4, paint.StrokeWidth);
            SKPoint curveControl = control ?? Midpoint(from, to);
            using SKPath curve = new();
            curve.MoveTo(from);
            curve.QuadTo(curveControl, to);
            canvas.DrawPath(curve, paint);
            DrawArrowHead(canvas, curveControl, to, paint);
            return;
        }

        canvas.DrawLine(from, to, paint);
        DrawArrowHead(canvas, from, to, paint);
    }

    private static void DrawArrowHead(SKCanvas canvas, SKPoint from, SKPoint to, SKPaint paint)
    {
        double angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
        float length = Math.Max(22, paint.StrokeWidth * 4.5f);
        SKPoint left = new(to.X - length * (float)Math.Cos(angle - Math.PI / 6), to.Y - length * (float)Math.Sin(angle - Math.PI / 6));
        SKPoint right = new(to.X - length * (float)Math.Cos(angle + Math.PI / 6), to.Y - length * (float)Math.Sin(angle + Math.PI / 6));
        canvas.DrawLine(to, left, paint);
        canvas.DrawLine(to, right, paint);
    }

    private static void DrawTriangleArrow(SKCanvas canvas, SKPoint from, SKPoint to, SKColor color, float thickness)
    {
        double angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
        float headLength = Math.Max(22, thickness * 4);
        float headWidth = Math.Max(16, thickness * 3);
        float shaftWidth = Math.Max(4, thickness);
        SKPoint headBase = new(to.X - headLength * (float)Math.Cos(angle), to.Y - headLength * (float)Math.Sin(angle));
        SKPoint normal = new(-(float)Math.Sin(angle), (float)Math.Cos(angle));
        using SKPath path = new();
        path.MoveTo(from.X + normal.X * shaftWidth / 2, from.Y + normal.Y * shaftWidth / 2);
        path.LineTo(headBase.X + normal.X * shaftWidth / 2, headBase.Y + normal.Y * shaftWidth / 2);
        path.LineTo(headBase.X + normal.X * headWidth / 2, headBase.Y + normal.Y * headWidth / 2);
        path.LineTo(to);
        path.LineTo(headBase.X - normal.X * headWidth / 2, headBase.Y - normal.Y * headWidth / 2);
        path.LineTo(headBase.X - normal.X * shaftWidth / 2, headBase.Y - normal.Y * shaftWidth / 2);
        path.LineTo(from.X - normal.X * shaftWidth / 2, from.Y - normal.Y * shaftWidth / 2);
        path.Close();
        using SKPaint fill = new() { Color = color, Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawPath(path, fill);
    }

    private static SKRect Offset(PixelRect rect, int x, int y) => new((float)(rect.X - x), (float)(rect.Y - y), (float)(rect.Right - x), (float)(rect.Bottom - y));
    private static SKPoint Offset(PixelPoint point, int x, int y) => new((float)(point.X - x), (float)(point.Y - y));
    private static SKColor Parse(string color) => SKColor.Parse(color);
    private static SKPoint Midpoint(SKPoint a, SKPoint b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

    private static SKRectI ToClampedRect(PixelRect rect, int width, int height) => new(
        Math.Clamp((int)Math.Floor(rect.X), 0, width),
        Math.Clamp((int)Math.Floor(rect.Y), 0, height),
        Math.Clamp((int)Math.Ceiling(rect.Right), 0, width),
        Math.Clamp((int)Math.Ceiling(rect.Bottom), 0, height));
}
