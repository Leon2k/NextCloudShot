using System.Windows.Input;
using NextCloudShot.Core.Models;
using NextCloudShot.Core.Services;

namespace NextCloudShot.Desktop.ViewModels;

public sealed class ScreenshotEditorViewModel : ObservableObject
{
    private readonly ScreenshotUploadWorkflow _workflow;
    private readonly Func<NextcloudConnectionSettings> _settingsFactory;
    private AnnotationTool _tool = AnnotationTool.Arrow;
    private string _textValue = "Text";
    private string _status = "Draw annotations, crop if needed, then upload.";

    public ScreenshotEditorViewModel(
        ScreenshotDocument document,
        ScreenshotUploadWorkflow workflow,
        Func<NextcloudConnectionSettings> settingsFactory)
    {
        Document = document;
        _workflow = workflow;
        _settingsFactory = settingsFactory;
        SelectToolCommand = new ParameterRelayCommand<string>(SelectTool);
        UploadCommand = new AsyncRelayCommand(UploadAsync);
        ClearCommand = new RelayCommand(() => { Document.ClearAnnotations(); RaisePropertyChanged(nameof(Document)); Changed?.Invoke(this, EventArgs.Empty); });
    }

    public event EventHandler? Changed;

    public ScreenshotDocument Document { get; }
    public AnnotationTool Tool { get => _tool; set { if (SetProperty(ref _tool, value)) Changed?.Invoke(this, EventArgs.Empty); } }
    public string TextValue { get => _textValue; set => SetProperty(ref _textValue, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public ICommand SelectToolCommand { get; }
    public ICommand UploadCommand { get; }
    public ICommand ClearCommand { get; }

    public void CommitRectangle(PixelRect rect) => Commit(new RectangleAnnotation(Guid.NewGuid(), rect.Normalize(), "#FF3D57", 3));
    public void CommitArrow(PixelPoint from, PixelPoint to) => Commit(new ArrowAnnotation(Guid.NewGuid(), from, to, "#FF3D57", 3));
    public void CommitPen(IReadOnlyList<PixelPoint> points) => Commit(new PenAnnotation(Guid.NewGuid(), points, "#FF3D57", 3));
    public void CommitPixelation(PixelRect rect) => Commit(new PixelateAnnotation(Guid.NewGuid(), rect.Normalize(), 14));
    public void CommitText(PixelPoint position) => Commit(new TextAnnotation(Guid.NewGuid(), position, TextValue, "#FF3D57", 24));
    public void CommitCrop(PixelRect rect)
    {
        if (!rect.Normalize().IsEmpty)
        {
            Document.Crop = rect.Normalize();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Commit(Annotation annotation)
    {
        Document.Add(annotation);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void SelectTool(string name)
    {
        if (Enum.TryParse(name, true, out AnnotationTool selected)) Tool = selected;
    }

    private async Task UploadAsync()
    {
        try
        {
            Status = "Rendering and uploading…";
            UploadResult result = await _workflow.UploadAndCopyLinkAsync(Document, _settingsFactory());
            Status = result.PublicUrl is null ? $"Uploaded: {result.RemotePath}" : $"Link copied: {result.PublicUrl}";
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
    }
}
