using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using NextCloudShot.Core.Models;
using NextCloudShot.Desktop.ViewModels;
using CorePoint = NextCloudShot.Core.Models.PixelPoint;
using CoreRect = NextCloudShot.Core.Models.PixelRect;

namespace NextCloudShot.Desktop.Controls;

public sealed class AnnotationCanvasControl : Control
{
    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<AnnotationCanvasControl, double>(nameof(Zoom), 1);

    static AnnotationCanvasControl() => AffectsMeasure<AnnotationCanvasControl>(ZoomProperty);

    private ScreenshotEditorViewModel? _editor;
    private Bitmap? _bitmap;
    private Point? _gestureStart;
    private Point _gestureCurrent;
    private readonly List<CorePoint> _penPoints = [];

    public AnnotationCanvasControl()
    {
        DataContextChanged += (_, _) => AttachViewModel(DataContext as ScreenshotEditorViewModel);
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
    }

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return _editor is null
            ? new Size(640, 360)
            : new Size(_editor.Document.Source.PixelSize.Width * Zoom, _editor.Document.Source.PixelSize.Height * Zoom);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_editor is null || _bitmap is null) return;

        double width = _editor.Document.Source.PixelSize.Width;
        double height = _editor.Document.Source.PixelSize.Height;
        using IDisposable transform = context.PushTransform(Matrix.CreateScale(Zoom, Zoom));
        context.DrawImage(_bitmap, new Rect(0, 0, width, height));

        foreach (Annotation annotation in _editor.Document.Annotations)
        {
            DrawAnnotation(context, annotation);
        }

        if (_gestureStart is Point start)
        {
            DrawPreview(context, start, _gestureCurrent);
        }

        CoreRect crop = _editor.Document.Crop.Normalize();
        if (crop.Width < width || crop.Height < height)
        {
            Pen cropPen = new(Brushes.DeepSkyBlue, 2, dashStyle: DashStyle.Dash);
            context.DrawRectangle(null, cropPen, ToRect(crop));
        }
    }

    private void AttachViewModel(ScreenshotEditorViewModel? editor)
    {
        if (_editor is not null) _editor.Changed -= OnEditorChanged;
        _bitmap?.Dispose();
        _bitmap = null;
        _editor = editor;
        if (_editor is not null)
        {
            using MemoryStream stream = new(_editor.Document.Source.PngBytes);
            _bitmap = new Bitmap(stream);
            _editor.Changed += OnEditorChanged;
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnEditorChanged(object? sender, EventArgs args) => InvalidateVisual();

    private void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (_editor is null || !args.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Point point = ToDocumentPoint(args.GetPosition(this));
        if (_editor.Tool == AnnotationTool.Text)
        {
            _editor.CommitText(ToCore(point));
            return;
        }

        _gestureStart = point;
        _gestureCurrent = point;
        _penPoints.Clear();
        if (_editor.Tool == AnnotationTool.Pen) _penPoints.Add(ToCore(point));
        args.Pointer.Capture(this);
        InvalidateVisual();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs args)
    {
        if (_editor is null || _gestureStart is null || args.Pointer.Captured != this) return;
        _gestureCurrent = ToDocumentPoint(args.GetPosition(this));
        if (_editor.Tool == AnnotationTool.Pen) _penPoints.Add(ToCore(_gestureCurrent));
        InvalidateVisual();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (_editor is null || _gestureStart is not Point start) return;
        _gestureCurrent = ToDocumentPoint(args.GetPosition(this));
        args.Pointer.Capture(null);
        CoreRect bounds = NewRect(start, _gestureCurrent).Normalize();
        switch (_editor.Tool)
        {
            case AnnotationTool.Crop: _editor.CommitCrop(bounds); break;
            case AnnotationTool.Rectangle: _editor.CommitRectangle(bounds); break;
            case AnnotationTool.Arrow: _editor.CommitArrow(ToCore(start), ToCore(_gestureCurrent)); break;
            case AnnotationTool.Pen when _penPoints.Count > 1: _editor.CommitPen(_penPoints.ToArray()); break;
            case AnnotationTool.Pixelate: _editor.CommitPixelation(bounds); break;
        }
        _gestureStart = null;
        _penPoints.Clear();
        InvalidateVisual();
    }

    private void DrawPreview(DrawingContext context, Point start, Point current)
    {
        if (_editor is null) return;
        Pen previewPen = new(Brushes.DeepSkyBlue, 2, dashStyle: DashStyle.Dash);
        switch (_editor.Tool)
        {
            case AnnotationTool.Arrow:
                DrawArrow(context, ToCore(start), ToCore(current), previewPen);
                break;
            case AnnotationTool.Pen:
                DrawPolyline(context, _penPoints, previewPen);
                break;
            default:
                context.DrawRectangle(null, previewPen, ToRect(NewRect(start, current).Normalize()));
                break;
        }
    }

    private static void DrawAnnotation(DrawingContext context, Annotation annotation)
    {
        switch (annotation)
        {
            case RectangleAnnotation rectangle:
                context.DrawRectangle(null, Pen(rectangle.Color, rectangle.Thickness), ToRect(rectangle.Bounds));
                break;
            case ArrowAnnotation arrow:
                DrawArrow(context, arrow.From, arrow.To, Pen(arrow.Color, arrow.Thickness));
                break;
            case PenAnnotation pen:
                DrawPolyline(context, pen.Points, Pen(pen.Color, pen.Thickness));
                break;
            case PixelateAnnotation pixelate:
                context.DrawRectangle(new SolidColorBrush(Color.Parse("#667B8794")), Pen("#C7D2DF", 1), ToRect(pixelate.Bounds));
                break;
            case TextAnnotation text:
                TextLayout layout = new(text.Text, new Typeface("Inter"), text.FontSize, Brush.Parse(text.Color));
                layout.Draw(context, new Point(text.Position.X, text.Position.Y));
                break;
        }
    }

    private static void DrawPolyline(DrawingContext context, IReadOnlyList<CorePoint> points, Pen pen)
    {
        for (int i = 1; i < points.Count; i++)
        {
            context.DrawLine(pen, new Point(points[i - 1].X, points[i - 1].Y), new Point(points[i].X, points[i].Y));
        }
    }

    private static void DrawArrow(DrawingContext context, CorePoint from, CorePoint to, Pen pen)
    {
        context.DrawLine(pen, new Point(from.X, from.Y), new Point(to.X, to.Y));
        double angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
        const double length = 18;
        Point left = new(to.X - length * Math.Cos(angle - Math.PI / 6), to.Y - length * Math.Sin(angle - Math.PI / 6));
        Point right = new(to.X - length * Math.Cos(angle + Math.PI / 6), to.Y - length * Math.Sin(angle + Math.PI / 6));
        context.DrawLine(pen, new Point(to.X, to.Y), left);
        context.DrawLine(pen, new Point(to.X, to.Y), right);
    }

    private static Pen Pen(string color, double thickness) => new(Brush.Parse(color), thickness);
    private Point ToDocumentPoint(Point point) => new(point.X / Zoom, point.Y / Zoom);
    private static CorePoint ToCore(Point point) => new(point.X, point.Y);
    private static CoreRect NewRect(Point a, Point b) => new(a.X, a.Y, b.X - a.X, b.Y - a.Y);
    private static Rect ToRect(CoreRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
