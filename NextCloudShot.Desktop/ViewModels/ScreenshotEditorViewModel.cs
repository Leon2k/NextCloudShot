using System.Windows.Input;
using NextCloudShot.Core.Models;
using NextCloudShot.Core.Services;

namespace NextCloudShot.Desktop.ViewModels;

public sealed class ScreenshotEditorViewModel : ObservableObject
{
    private readonly ScreenshotUploadWorkflow _workflow;
    private readonly Func<NextcloudConnectionSettings> _settingsFactory;
    private readonly Func<ScreenshotOutputSettings> _outputSettingsFactory;
    private readonly Stack<DocumentState> _undo = [];
    private readonly Stack<DocumentState> _redo = [];
    private AnnotationTool _tool = AnnotationTool.Arrow;
    private ArrowStyle _arrowStyle = ArrowStyle.Parallel;
    private ShapeStyle _shapeStyle = ShapeStyle.Rectangle;
    private Guid? _selectedAnnotationId;
    private PixelRect? _pendingCrop;
    private string _textValue = "Text";
    private string _toolColor = "#E45A4F";
    private double _toolThickness = 2;
    private double _zoom = 0.8;
    private bool _hasUnsavedChanges = true;
    private string _status = "Готово. Выберите инструмент и отредактируйте снимок.";

    public ScreenshotEditorViewModel(
        ScreenshotDocument document,
        ScreenshotUploadWorkflow workflow,
        Func<NextcloudConnectionSettings> settingsFactory,
        Func<ScreenshotOutputSettings> outputSettingsFactory)
    {
        Document = document;
        _workflow = workflow;
        _settingsFactory = settingsFactory;
        _outputSettingsFactory = outputSettingsFactory;
        SelectToolCommand = new ParameterRelayCommand<string>(SelectTool);
        SelectColorCommand = new ParameterRelayCommand<string>(color => ToolColor = color);
        SelectThicknessCommand = new ParameterRelayCommand<string>(SelectThickness);
        SelectArrowStyleCommand = new ParameterRelayCommand<string>(SelectArrowStyle);
        SelectShapeStyleCommand = new ParameterRelayCommand<string>(SelectShapeStyle);
        ApplyCropCommand = new RelayCommand(ApplyCrop, () => PendingCrop is not null);
        CancelCropCommand = new RelayCommand(CancelCrop, () => PendingCrop is not null);
        UploadCommand = new AsyncRelayCommand(UploadAsync);
        CopyCommand = new AsyncRelayCommand(CopyAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ClearCommand = new RelayCommand(Clear);
        UndoCommand = new RelayCommand(Undo, () => _undo.Count > 0);
        RedoCommand = new RelayCommand(Redo, () => _redo.Count > 0);
        ZoomInCommand = new RelayCommand(() => Zoom = Math.Min(2, Zoom + 0.1));
        ZoomOutCommand = new RelayCommand(() => Zoom = Math.Max(0.2, Zoom - 0.1));
    }

    public event EventHandler? Changed;
    public event EventHandler? RequestClose;

    public ScreenshotDocument Document { get; }
    public AnnotationTool Tool
    {
        get => _tool;
        set
        {
            if (!SetProperty(ref _tool, value)) return;
            if (value == AnnotationTool.Crop)
            {
                PendingCrop = Document.Crop.Normalize();
            }
            else if (PendingCrop is not null)
            {
                PendingCrop = null;
            }
            RaiseToolSelectionChanged();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
    public string TextValue { get => _textValue; set => SetProperty(ref _textValue, value); }
    public ArrowStyle ArrowStyle
    {
        get => _arrowStyle;
        set
        {
            if (!SetProperty(ref _arrowStyle, value)) return;
            RaisePropertyChanged(nameof(IsParallelArrowSelected));
            RaisePropertyChanged(nameof(IsTriangleArrowSelected));
            RaisePropertyChanged(nameof(IsDottedArrowSelected));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
    public ShapeStyle ShapeStyle
    {
        get => _shapeStyle;
        set
        {
            if (!SetProperty(ref _shapeStyle, value)) return;
            RaisePropertyChanged(nameof(IsRectangleShapeSelected));
            RaisePropertyChanged(nameof(IsEllipseShapeSelected));
            RaisePropertyChanged(nameof(IsCloudShapeSelected));
            RaisePropertyChanged(nameof(IsLineShapeSelected));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
    public PixelRect? PendingCrop
    {
        get => _pendingCrop;
        set
        {
            if (!SetProperty(ref _pendingCrop, value?.Normalize())) return;
            ((RelayCommand)ApplyCropCommand).NotifyCanExecuteChanged();
            ((RelayCommand)CancelCropCommand).NotifyCanExecuteChanged();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
    public Guid? SelectedAnnotationId
    {
        get => _selectedAnnotationId;
        set
        {
            if (!SetProperty(ref _selectedAnnotationId, value)) return;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
    public string ToolColor { get => _toolColor; set => SetProperty(ref _toolColor, value); }
    public double ToolThickness
    {
        get => _toolThickness;
        set
        {
            if (!SetProperty(ref _toolThickness, value)) return;
            RaiseThicknessSelectionChanged();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
    public double Zoom
    {
        get => _zoom;
        set
        {
            if (!SetProperty(ref _zoom, Math.Round(value, 1))) return;
            RaisePropertyChanged(nameof(ZoomPercent));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
    public string ZoomPercent => $"{Zoom:P0}";
    public bool HasUnsavedChanges { get => _hasUnsavedChanges; private set => SetProperty(ref _hasUnsavedChanges, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public bool IsArrowSelected => Tool == AnnotationTool.Arrow;
    public bool IsRectangleSelected => Tool == AnnotationTool.Rectangle;
    public bool IsPenSelected => Tool == AnnotationTool.Pen;
    public bool IsPixelateSelected => Tool == AnnotationTool.Pixelate;
    public bool IsTextSelected => Tool == AnnotationTool.Text;
    public bool IsCropSelected => Tool == AnnotationTool.Crop;
    public bool ShowsStrokeOptions => Tool is AnnotationTool.Arrow or AnnotationTool.Rectangle or AnnotationTool.Pen;
    public bool IsParallelArrowSelected => ArrowStyle == ArrowStyle.Parallel;
    public bool IsTriangleArrowSelected => ArrowStyle == ArrowStyle.Triangle;
    public bool IsDottedArrowSelected => ArrowStyle == ArrowStyle.Dotted;
    public bool IsRectangleShapeSelected => ShapeStyle == ShapeStyle.Rectangle;
    public bool IsEllipseShapeSelected => ShapeStyle == ShapeStyle.Ellipse;
    public bool IsCloudShapeSelected => ShapeStyle == ShapeStyle.Cloud;
    public bool IsLineShapeSelected => ShapeStyle == ShapeStyle.Line;
    public bool IsThinThicknessSelected => Math.Abs(ToolThickness - 2) < 0.01;
    public bool IsMediumThicknessSelected => Math.Abs(ToolThickness - 4) < 0.01;
    public bool IsThickThicknessSelected => Math.Abs(ToolThickness - 7) < 0.01;
    public ICommand SelectToolCommand { get; }
    public ICommand SelectColorCommand { get; }
    public ICommand SelectThicknessCommand { get; }
    public ICommand SelectArrowStyleCommand { get; }
    public ICommand SelectShapeStyleCommand { get; }
    public ICommand ApplyCropCommand { get; }
    public ICommand CancelCropCommand { get; }
    public ICommand UploadCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }

    public void CommitRectangle(PixelRect rect) => Commit(new RectangleAnnotation(Guid.NewGuid(), rect.Normalize(), ToolColor, ToolThickness, ShapeStyle));
    public void CommitArrow(PixelPoint from, PixelPoint to) => Commit(new ArrowAnnotation(Guid.NewGuid(), from, to, ToolColor, ToolThickness, ArrowStyle, Midpoint(from, to)));
    public void CommitPen(IReadOnlyList<PixelPoint> points) => Commit(new PenAnnotation(Guid.NewGuid(), points, ToolColor, ToolThickness));
    public void CommitPixelation(PixelRect rect) => Commit(new PixelateAnnotation(Guid.NewGuid(), rect.Normalize(), 14));
    public void CommitText(PixelPoint position) => Commit(new TextAnnotation(Guid.NewGuid(), position, TextValue, ToolColor, 24));
    public void CommitCrop(PixelRect rect)
    {
        if (!rect.Normalize().IsEmpty)
        {
            RememberForUndo();
            Document.Crop = rect.Normalize();
            MarkDirty();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
    public void SetPendingCrop(PixelRect rect)
    {
        PixelRect normalized = rect.Normalize();
        if (!normalized.IsEmpty) PendingCrop = normalized;
    }

    public void FitToViewport(double width, double height)
    {
        PixelRect crop = Document.Crop.Normalize();
        if (crop.IsEmpty || width <= 0 || height <= 0) return;
        double zoom = Math.Min((width - 80) / crop.Width, (height - 80) / crop.Height);
        Zoom = Math.Clamp(Math.Floor(zoom * 10) / 10, 0.2, 2);
    }

    private void Commit(Annotation annotation)
    {
        RememberForUndo();
        Document.Add(annotation);
        SelectedAnnotationId = annotation.Id;
        MarkDirty();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Annotation? GetSelectedAnnotation() =>
        SelectedAnnotationId is Guid id ? Document.Annotations.FirstOrDefault(annotation => annotation.Id == id) : null;

    public void BeginAnnotationEdit() => RememberForUndo();

    public void UpdateAnnotation(Annotation annotation)
    {
        Document.Replace(annotation);
        MarkDirty();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void FinishAnnotationEdit() => NotifyHistoryChanged();

    public void BeginDrawing() => SelectedAnnotationId = null;

    private void SelectTool(string name)
    {
        if (Enum.TryParse(name, true, out AnnotationTool selected)) Tool = selected;
    }

    private void SelectThickness(string value)
    {
        if (double.TryParse(value, out double thickness))
        {
            ToolThickness = thickness;
        }
    }

    private void SelectArrowStyle(string value)
    {
        if (Enum.TryParse(value, true, out ArrowStyle style)) ArrowStyle = style;
    }
    private void SelectShapeStyle(string value)
    {
        if (Enum.TryParse(value, true, out ShapeStyle style)) ShapeStyle = style;
    }

    private void ApplyCrop()
    {
        if (PendingCrop is not PixelRect crop) return;
        CommitCrop(crop);
        PendingCrop = null;
        Tool = AnnotationTool.Arrow;
    }

    private void CancelCrop()
    {
        PendingCrop = null;
        Tool = AnnotationTool.Arrow;
    }

    private async Task CopyAsync()
    {
        try
        {
            Status = "Копирование и сохранение в папку Nextcloud...";
            LocalScreenshotResult result = await _workflow.CopyImageAsync(Document, _settingsFactory(), _outputSettingsFactory());
            HasUnsavedChanges = false;
            Status = $"Скопировано и сохранено: {result.RemotePath}";
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
    }

    private async Task SaveAsync() => await SaveCurrentAsync(showInFolder: true, closeAfterSave: true);

    public async Task<bool> SaveForClosePromptAsync() =>
        await SaveCurrentAsync(showInFolder: true, closeAfterSave: false);

    private async Task<bool> SaveCurrentAsync(bool showInFolder, bool closeAfterSave)
    {
        try
        {
            Status = "Сохранение в Nextcloud...";
            LocalScreenshotResult result = await _workflow.SaveToNextcloudAsync(Document, _settingsFactory(), _outputSettingsFactory(), showInFolder);
            HasUnsavedChanges = false;
            Status = $"Сохранено в Nextcloud: {result.RemotePath}";
            if (closeAfterSave)
            {
                RequestClose?.Invoke(this, EventArgs.Empty);
            }

            return true;
        }
        catch (Exception exception)
        {
            Status = exception.Message;
            return false;
        }
    }

    private async Task UploadAsync()
    {
        try
        {
            Status = "Загрузка в Nextcloud...";
            UploadResult result = await _workflow.UploadAndCopyLinkAsync(Document, _settingsFactory(), _outputSettingsFactory());
            HasUnsavedChanges = false;
            Status = result.PublicUrl is null ? $"Загружено: {result.RemotePath}" : $"Ссылка скопирована: {result.PublicUrl}";
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
    }

    private void Clear()
    {
        if (Document.Annotations.Count == 0) return;
        RememberForUndo();
        Document.ClearAnnotations();
        MarkDirty();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Push(CaptureState());
        Restore(_undo.Pop());
    }

    private void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Push(CaptureState());
        Restore(_redo.Pop());
    }

    private void RememberForUndo()
    {
        _undo.Push(CaptureState());
        _redo.Clear();
        NotifyHistoryChanged();
    }

    private DocumentState CaptureState() => new(Document.Crop, Document.Annotations.ToArray());

    private void Restore(DocumentState state)
    {
        Document.Crop = state.Crop;
        Document.ReplaceAnnotations(state.Annotations);
        MarkDirty();
        NotifyHistoryChanged();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyHistoryChanged()
    {
        ((RelayCommand)UndoCommand).NotifyCanExecuteChanged();
        ((RelayCommand)RedoCommand).NotifyCanExecuteChanged();
    }

    private void RaiseToolSelectionChanged()
    {
        RaisePropertyChanged(nameof(IsArrowSelected));
        RaisePropertyChanged(nameof(IsRectangleSelected));
        RaisePropertyChanged(nameof(IsPenSelected));
        RaisePropertyChanged(nameof(IsPixelateSelected));
        RaisePropertyChanged(nameof(IsTextSelected));
        RaisePropertyChanged(nameof(IsCropSelected));
        RaisePropertyChanged(nameof(ShowsStrokeOptions));
    }

    private void RaiseThicknessSelectionChanged()
    {
        RaisePropertyChanged(nameof(IsThinThicknessSelected));
        RaisePropertyChanged(nameof(IsMediumThicknessSelected));
        RaisePropertyChanged(nameof(IsThickThicknessSelected));
    }

    private void MarkDirty() => HasUnsavedChanges = true;

    private sealed record DocumentState(PixelRect Crop, IReadOnlyList<Annotation> Annotations);

    private static PixelPoint Midpoint(PixelPoint a, PixelPoint b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);
}
