using System.Windows.Input;
using NextCloudShot.Core.Models;
using NextCloudShot.Core.Services;

namespace NextCloudShot.Desktop.ViewModels;

public sealed class ScreenshotEditorViewModel : ObservableObject
{
    private readonly ScreenshotUploadWorkflow _workflow;
    private readonly Func<NextcloudConnectionSettings> _settingsFactory;
    private readonly Stack<DocumentState> _undo = [];
    private readonly Stack<DocumentState> _redo = [];
    private AnnotationTool _tool = AnnotationTool.Arrow;
    private string _textValue = "Text";
    private string _toolColor = "#E45A4F";
    private double _toolThickness = 4;
    private double _zoom = 0.8;
    private string _status = "Готово. Выберите инструмент и отредактируйте снимок.";

    public ScreenshotEditorViewModel(
        ScreenshotDocument document,
        ScreenshotUploadWorkflow workflow,
        Func<NextcloudConnectionSettings> settingsFactory)
    {
        Document = document;
        _workflow = workflow;
        _settingsFactory = settingsFactory;
        SelectToolCommand = new ParameterRelayCommand<string>(SelectTool);
        SelectColorCommand = new ParameterRelayCommand<string>(color => ToolColor = color);
        SelectThicknessCommand = new ParameterRelayCommand<string>(SelectThickness);
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

    public ScreenshotDocument Document { get; }
    public AnnotationTool Tool
    {
        get => _tool;
        set
        {
            if (!SetProperty(ref _tool, value)) return;
            RaiseToolSelectionChanged();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
    public string TextValue { get => _textValue; set => SetProperty(ref _textValue, value); }
    public string ToolColor { get => _toolColor; set => SetProperty(ref _toolColor, value); }
    public double ToolThickness { get => _toolThickness; set => SetProperty(ref _toolThickness, value); }
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
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public bool IsArrowSelected => Tool == AnnotationTool.Arrow;
    public bool IsRectangleSelected => Tool == AnnotationTool.Rectangle;
    public bool IsPenSelected => Tool == AnnotationTool.Pen;
    public bool IsPixelateSelected => Tool == AnnotationTool.Pixelate;
    public bool IsTextSelected => Tool == AnnotationTool.Text;
    public bool IsCropSelected => Tool == AnnotationTool.Crop;
    public bool ShowsStrokeOptions => Tool is AnnotationTool.Arrow or AnnotationTool.Rectangle or AnnotationTool.Pen;
    public ICommand SelectToolCommand { get; }
    public ICommand SelectColorCommand { get; }
    public ICommand SelectThicknessCommand { get; }
    public ICommand UploadCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }

    public void CommitRectangle(PixelRect rect) => Commit(new RectangleAnnotation(Guid.NewGuid(), rect.Normalize(), ToolColor, ToolThickness));
    public void CommitArrow(PixelPoint from, PixelPoint to) => Commit(new ArrowAnnotation(Guid.NewGuid(), from, to, ToolColor, ToolThickness));
    public void CommitPen(IReadOnlyList<PixelPoint> points) => Commit(new PenAnnotation(Guid.NewGuid(), points, ToolColor, ToolThickness));
    public void CommitPixelation(PixelRect rect) => Commit(new PixelateAnnotation(Guid.NewGuid(), rect.Normalize(), 14));
    public void CommitText(PixelPoint position) => Commit(new TextAnnotation(Guid.NewGuid(), position, TextValue, ToolColor, 24));
    public void CommitCrop(PixelRect rect)
    {
        if (!rect.Normalize().IsEmpty)
        {
            RememberForUndo();
            Document.Crop = rect.Normalize();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Commit(Annotation annotation)
    {
        RememberForUndo();
        Document.Add(annotation);
        Changed?.Invoke(this, EventArgs.Empty);
    }

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

    private async Task CopyAsync()
    {
        await _workflow.CopyImageAsync(Document);
        Status = "Изображение скопировано в буфер обмена.";
    }

    private async Task SaveAsync()
    {
        string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        string directory = Path.Combine(pictures, "NextCloudShot");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"Screenshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
        await File.WriteAllBytesAsync(path, _workflow.RenderPng(Document));
        Status = $"Сохранено: {path}";
    }

    private async Task UploadAsync()
    {
        try
        {
            Status = "Загрузка в Nextcloud...";
            UploadResult result = await _workflow.UploadAndCopyLinkAsync(Document, _settingsFactory());
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

    private sealed record DocumentState(PixelRect Crop, IReadOnlyList<Annotation> Annotations);
}
