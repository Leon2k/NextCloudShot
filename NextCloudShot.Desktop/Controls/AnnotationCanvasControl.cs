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
    private EditHandle _editHandle;
    private Annotation? _editingOriginal;
    private CoreRect? _editingCropOriginal;

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
            : new Size(_editor.Document.Crop.Normalize().Width * Zoom, _editor.Document.Crop.Normalize().Height * Zoom);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_editor is null || _bitmap is null) return;

        CoreRect crop = _editor.Document.Crop.Normalize();
        using IDisposable transform = context.PushTransform(Matrix.CreateScale(Zoom, Zoom));
        context.DrawImage(_bitmap, ToRect(crop), new Rect(0, 0, crop.Width, crop.Height));
        using IDisposable offset = context.PushTransform(Matrix.CreateTranslation(-crop.X, -crop.Y));

        foreach (Annotation annotation in _editor.Document.Annotations)
        {
            DrawAnnotation(context, annotation);
        }

        if (_gestureStart is Point start)
        {
            DrawPreview(context, start, _gestureCurrent);
        }

        if (_editor.GetSelectedAnnotation() is Annotation selected)
        {
            DrawSelectionHandles(context, selected);
        }

        if (_editor.Tool == AnnotationTool.Crop && _editor.PendingCrop is CoreRect pendingCrop)
        {
            DrawCropOverlay(context, crop, pendingCrop.Normalize());
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

    private void OnEditorChanged(object? sender, EventArgs args)
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (_editor is null || !args.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Point point = ToDocumentPoint(args.GetPosition(this));
        if (_editor.Tool == AnnotationTool.Crop && TryBeginCropEdit(point))
        {
            args.Pointer.Capture(this);
            return;
        }
        if (TryBeginEdit(point))
        {
            args.Pointer.Capture(this);
            return;
        }

        if (_editor.Tool == AnnotationTool.Text)
        {
            _editor.BeginDrawing();
            _editor.CommitText(ToCore(point));
            return;
        }

        _editor.BeginDrawing();
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
        if (_editHandle != EditHandle.None)
        {
            if (_editingCropOriginal is not null) UpdateEditedCrop(_gestureCurrent);
            else UpdateEditedAnnotation(_gestureCurrent);
            return;
        }
        if (_editor.Tool == AnnotationTool.Pen) _penPoints.Add(ToCore(_gestureCurrent));
        InvalidateVisual();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (_editor is null || _gestureStart is not Point start) return;
        _gestureCurrent = ToDocumentPoint(args.GetPosition(this));
        args.Pointer.Capture(null);
        if (_editHandle != EditHandle.None)
        {
            if (_editingCropOriginal is not null) UpdateEditedCrop(_gestureCurrent);
            else
            {
                UpdateEditedAnnotation(_gestureCurrent);
                _editor.FinishAnnotationEdit();
            }
            _editHandle = EditHandle.None;
            _editingOriginal = null;
            _editingCropOriginal = null;
            _gestureStart = null;
            return;
        }

        CoreRect bounds = NewRect(start, _gestureCurrent).Normalize();
        switch (_editor.Tool)
        {
            case AnnotationTool.Crop: _editor.SetPendingCrop(bounds); break;
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
                DrawArrow(context, ToCore(start), ToCore(current), Midpoint(ToCore(start), ToCore(current)), previewPen, _editor.ArrowStyle);
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
                DrawShape(context, rectangle);
                break;
            case ArrowAnnotation arrow:
                DrawArrow(context, arrow.From, arrow.To, arrow.Control, Pen(arrow.Color, arrow.Thickness), arrow.Style);
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

    private static void DrawShape(DrawingContext context, RectangleAnnotation shape)
    {
        Pen pen = Pen(shape.Color, shape.Thickness);
        Rect bounds = ToRect(shape.Bounds);
        switch (shape.Style)
        {
            case ShapeStyle.Ellipse:
                context.DrawEllipse(null, pen, bounds);
                break;
            case ShapeStyle.Cloud:
                context.DrawRectangle(null, new Pen(pen.Brush, pen.Thickness, new DashStyle([1, 1], 0), PenLineCap.Round, PenLineJoin.Round), bounds);
                break;
            case ShapeStyle.Line:
                context.DrawLine(pen, bounds.TopLeft, bounds.BottomRight);
                break;
            default:
                context.DrawRectangle(null, pen, bounds);
                break;
        }
    }

    private static void DrawArrow(DrawingContext context, CorePoint from, CorePoint to, CorePoint? control, Pen pen, ArrowStyle style)
    {
        if (style == ArrowStyle.Triangle)
        {
            DrawTriangleArrow(context, from, to, pen.Brush, pen.Thickness);
            return;
        }

        if (style == ArrowStyle.Dotted)
        {
            pen = new Pen(pen.Brush, Math.Max(4, pen.Thickness), new DashStyle([0, 2], 0), PenLineCap.Round, PenLineJoin.Round);
            CorePoint curveControl = control ?? Midpoint(from, to);
            StreamGeometry curve = new();
            using (StreamGeometryContext path = curve.Open())
            {
                path.BeginFigure(new Point(from.X, from.Y), false);
                path.QuadraticBezierTo(new Point(curveControl.X, curveControl.Y), new Point(to.X, to.Y));
            }
            context.DrawGeometry(null, pen, curve);
            DrawArrowHead(context, curveControl, to, pen);
            return;
        }

        context.DrawLine(pen, new Point(from.X, from.Y), new Point(to.X, to.Y));
        DrawArrowHead(context, from, to, pen);
    }

    private static void DrawArrowHead(DrawingContext context, CorePoint from, CorePoint to, Pen pen)
    {
        double angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
        double length = Math.Max(22, pen.Thickness * 4.5);
        Point left = new(to.X - length * Math.Cos(angle - Math.PI / 6), to.Y - length * Math.Sin(angle - Math.PI / 6));
        Point right = new(to.X - length * Math.Cos(angle + Math.PI / 6), to.Y - length * Math.Sin(angle + Math.PI / 6));
        context.DrawLine(pen, new Point(to.X, to.Y), left);
        context.DrawLine(pen, new Point(to.X, to.Y), right);
    }

    private static void DrawTriangleArrow(DrawingContext context, CorePoint from, CorePoint to, IBrush? brush, double thickness)
    {
        double angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
        double headLength = Math.Max(22, thickness * 4);
        double headWidth = Math.Max(16, thickness * 3);
        double shaftWidth = Math.Max(4, thickness);
        Point headBase = new(to.X - headLength * Math.Cos(angle), to.Y - headLength * Math.Sin(angle));
        Vector normal = new(-Math.Sin(angle), Math.Cos(angle));
        StreamGeometry geometry = new();
        using (StreamGeometryContext path = geometry.Open())
        {
            path.BeginFigure(new Point(from.X + normal.X * shaftWidth / 2, from.Y + normal.Y * shaftWidth / 2), true);
            path.LineTo(headBase + normal * (shaftWidth / 2));
            path.LineTo(headBase + normal * (headWidth / 2));
            path.LineTo(new Point(to.X, to.Y));
            path.LineTo(headBase - normal * (headWidth / 2));
            path.LineTo(headBase - normal * (shaftWidth / 2));
            path.LineTo(new Point(from.X - normal.X * shaftWidth / 2, from.Y - normal.Y * shaftWidth / 2));
            path.EndFigure(true);
        }
        context.DrawGeometry(brush, null, geometry);
    }

    private bool TryBeginEdit(Point point)
    {
        if (_editor?.GetSelectedAnnotation() is not Annotation annotation) return false;
        double radius = 12 / Zoom;
        EditHandle handle = annotation switch
        {
            ArrowAnnotation arrow when Near(point, arrow.From, radius) => EditHandle.Start,
            ArrowAnnotation arrow when Near(point, arrow.To, radius) => EditHandle.End,
            ArrowAnnotation arrow when arrow.Style == ArrowStyle.Dotted && Near(point, arrow.Control ?? Midpoint(arrow.From, arrow.To), radius) => EditHandle.Control,
            RectangleAnnotation rectangle when Near(point, new CorePoint(rectangle.Bounds.X, rectangle.Bounds.Y), radius) => EditHandle.TopLeft,
            RectangleAnnotation rectangle when Near(point, new CorePoint(rectangle.Bounds.X + rectangle.Bounds.Width / 2, rectangle.Bounds.Y), radius) => EditHandle.Top,
            RectangleAnnotation rectangle when Near(point, new CorePoint(rectangle.Bounds.Right, rectangle.Bounds.Y), radius) => EditHandle.TopRight,
            RectangleAnnotation rectangle when Near(point, new CorePoint(rectangle.Bounds.X, rectangle.Bounds.Y + rectangle.Bounds.Height / 2), radius) => EditHandle.Left,
            RectangleAnnotation rectangle when Near(point, new CorePoint(rectangle.Bounds.Right, rectangle.Bounds.Y + rectangle.Bounds.Height / 2), radius) => EditHandle.Right,
            RectangleAnnotation rectangle when Near(point, new CorePoint(rectangle.Bounds.X, rectangle.Bounds.Bottom), radius) => EditHandle.BottomLeft,
            RectangleAnnotation rectangle when Near(point, new CorePoint(rectangle.Bounds.X + rectangle.Bounds.Width / 2, rectangle.Bounds.Bottom), radius) => EditHandle.Bottom,
            RectangleAnnotation rectangle when Near(point, new CorePoint(rectangle.Bounds.Right, rectangle.Bounds.Bottom), radius) => EditHandle.BottomRight,
            PixelateAnnotation pixelate when FindRectangleHandle(point, pixelate.Bounds, radius) is EditHandle pixelateHandle => pixelateHandle,
            _ => EditHandle.None
        };
        if (handle == EditHandle.None) return false;

        _editor.BeginAnnotationEdit();
        _editingOriginal = annotation;
        _editHandle = handle;
        _gestureStart = point;
        _gestureCurrent = point;
        return true;
    }

    private void UpdateEditedAnnotation(Point current)
    {
        if (_editor is null || _editingOriginal is null || _gestureStart is not Point start) return;
        double dx = current.X - start.X;
        double dy = current.Y - start.Y;
        Annotation updated = _editingOriginal switch
        {
            ArrowAnnotation arrow when _editHandle == EditHandle.Start => arrow with { From = ToCore(current) },
            ArrowAnnotation arrow when _editHandle == EditHandle.End => arrow with { To = ToCore(current) },
            ArrowAnnotation arrow when _editHandle == EditHandle.Control => arrow with { Control = ToCore(current) },
            RectangleAnnotation rectangle => rectangle with { Bounds = ResizeRectangle(rectangle.Bounds, _editHandle, current) },
            PixelateAnnotation pixelate => pixelate with { Bounds = ResizeRectangle(pixelate.Bounds, _editHandle, current) },
            _ => _editingOriginal
        };
        _editor.UpdateAnnotation(updated);
    }

    private static CoreRect ResizeRectangle(CoreRect bounds, EditHandle handle, Point current) => handle switch
    {
        EditHandle.TopLeft => new CoreRect(current.X, current.Y, bounds.Right - current.X, bounds.Bottom - current.Y).Normalize(),
        EditHandle.Top => new CoreRect(bounds.X, current.Y, bounds.Width, bounds.Bottom - current.Y).Normalize(),
        EditHandle.TopRight => new CoreRect(bounds.X, current.Y, current.X - bounds.X, bounds.Bottom - current.Y).Normalize(),
        EditHandle.Left => new CoreRect(current.X, bounds.Y, bounds.Right - current.X, bounds.Height).Normalize(),
        EditHandle.Right => new CoreRect(bounds.X, bounds.Y, current.X - bounds.X, bounds.Height).Normalize(),
        EditHandle.BottomLeft => new CoreRect(current.X, bounds.Y, bounds.Right - current.X, current.Y - bounds.Y).Normalize(),
        EditHandle.Bottom => new CoreRect(bounds.X, bounds.Y, bounds.Width, current.Y - bounds.Y).Normalize(),
        EditHandle.BottomRight => new CoreRect(bounds.X, bounds.Y, current.X - bounds.X, current.Y - bounds.Y).Normalize(),
        _ => bounds
    };

    private static void DrawSelectionHandles(DrawingContext context, Annotation annotation)
    {
        IEnumerable<CorePoint> handles = annotation switch
        {
            ArrowAnnotation arrow when arrow.Style == ArrowStyle.Dotted => [arrow.From, arrow.Control ?? Midpoint(arrow.From, arrow.To), arrow.To],
            ArrowAnnotation arrow => [arrow.From, arrow.To],
            RectangleAnnotation rectangle => RectangleHandlePoints(rectangle.Bounds),
            PixelateAnnotation pixelate => RectangleHandlePoints(pixelate.Bounds),
            _ => []
        };
        foreach (CorePoint point in handles)
        {
            context.DrawEllipse(Brushes.DodgerBlue, new Pen(Brushes.White, 1.2), new Point(point.X, point.Y), 5, 5);
        }
    }

    private static bool Near(Point point, CorePoint target, double radius) =>
        Math.Abs(point.X - target.X) <= radius && Math.Abs(point.Y - target.Y) <= radius;

    private static CorePoint Midpoint(CorePoint a, CorePoint b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

    private bool TryBeginCropEdit(Point point)
    {
        if (_editor?.PendingCrop is not CoreRect crop) return false;
        EditHandle handle = FindRectangleHandle(point, crop, 12 / Zoom);
        if (handle == EditHandle.None) return false;
        _editingCropOriginal = crop;
        _editHandle = handle;
        _gestureStart = point;
        _gestureCurrent = point;
        return true;
    }

    private void UpdateEditedCrop(Point current)
    {
        if (_editor is null || _editingCropOriginal is not CoreRect crop) return;
        _editor.SetPendingCrop(ResizeRectangle(crop, _editHandle, current));
    }

    private static EditHandle FindRectangleHandle(Point point, CoreRect bounds, double radius)
    {
        CorePoint[] points = RectangleHandlePoints(bounds).ToArray();
        EditHandle[] handles =
        [
            EditHandle.TopLeft, EditHandle.Top, EditHandle.TopRight, EditHandle.Left,
            EditHandle.Right, EditHandle.BottomLeft, EditHandle.Bottom, EditHandle.BottomRight
        ];
        for (int index = 0; index < points.Length; index++)
        {
            if (Near(point, points[index], radius)) return handles[index];
        }
        return EditHandle.None;
    }

    private static IEnumerable<CorePoint> RectangleHandlePoints(CoreRect bounds) =>
    [
        new(bounds.X, bounds.Y),
        new(bounds.X + bounds.Width / 2, bounds.Y),
        new(bounds.Right, bounds.Y),
        new(bounds.X, bounds.Y + bounds.Height / 2),
        new(bounds.Right, bounds.Y + bounds.Height / 2),
        new(bounds.X, bounds.Bottom),
        new(bounds.X + bounds.Width / 2, bounds.Bottom),
        new(bounds.Right, bounds.Bottom)
    ];

    private static void DrawCropOverlay(DrawingContext context, CoreRect image, CoreRect selection)
    {
        IBrush shade = new SolidColorBrush(Color.Parse("#99000000"));
        context.DrawRectangle(shade, null, new Rect(image.X, image.Y, image.Width, Math.Max(0, selection.Y - image.Y)));
        context.DrawRectangle(shade, null, new Rect(image.X, selection.Bottom, image.Width, Math.Max(0, image.Bottom - selection.Bottom)));
        context.DrawRectangle(shade, null, new Rect(image.X, selection.Y, Math.Max(0, selection.X - image.X), selection.Height));
        context.DrawRectangle(shade, null, new Rect(selection.Right, selection.Y, Math.Max(0, image.Right - selection.Right), selection.Height));
        context.DrawRectangle(null, new Pen(Brushes.DodgerBlue, 2), ToRect(selection));
        foreach (CorePoint point in RectangleHandlePoints(selection))
        {
            context.DrawEllipse(Brushes.DodgerBlue, new Pen(Brushes.White, 1.2), new Point(point.X, point.Y), 5, 5);
        }
    }

    private static Pen Pen(string color, double thickness) => new(Brush.Parse(color), thickness);
    private Point ToDocumentPoint(Point point)
    {
        CoreRect crop = _editor?.Document.Crop.Normalize() ?? default;
        return new Point(point.X / Zoom + crop.X, point.Y / Zoom + crop.Y);
    }
    private static CorePoint ToCore(Point point) => new(point.X, point.Y);
    private static CoreRect NewRect(Point a, Point b) => new(a.X, a.Y, b.X - a.X, b.Y - a.Y);
    private static Rect ToRect(CoreRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    private enum EditHandle
    {
        None,
        Start,
        End,
        Control,
        TopLeft,
        Top,
        TopRight,
        Left,
        Right,
        BottomLeft,
        Bottom,
        BottomRight
    }
}
