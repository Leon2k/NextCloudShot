using Avalonia.Controls;
using Avalonia.Threading;
using NextCloudShot.Core.Contracts;
using NextCloudShot.Core.Models;
using NextCloudShot.Core.Services;
using NextCloudShot.Desktop.ViewModels;
using NextCloudShot.Desktop.Views;

namespace NextCloudShot.Desktop.Services;

public sealed class CaptureCoordinator : IDisposable
{
    private readonly IScreenCaptureService _capture;
    private readonly IGlobalHotkeyService _hotkeys;
    private readonly MainWindowViewModel _main;
    private readonly ScreenshotUploadWorkflow _workflow;
    private readonly Window _owner;
    private bool _captureRunning;

    public CaptureCoordinator(
        IScreenCaptureService capture,
        IGlobalHotkeyService hotkeys,
        MainWindowViewModel main,
        ScreenshotUploadWorkflow workflow,
        Window owner)
    {
        _capture = capture;
        _hotkeys = hotkeys;
        _main = main;
        _workflow = workflow;
        _owner = owner;
        _hotkeys.HotkeyPressed += OnHotkeyPressed;
    }

    public void Start()
    {
        try { _hotkeys.Start(); }
        catch (Exception exception) { _main.Status = exception.Message; }
    }

    public async Task CaptureAsync(CaptureMode mode)
    {
        if (_captureRunning) return;
        _captureRunning = true;
        try
        {
            ScreenshotImage source = mode == CaptureMode.ActiveWindow
                ? await _capture.CaptureActiveWindowAsync()
                : await _capture.CaptureVirtualDesktopAsync();

            ScreenshotDocument document = new(source);
            if (mode == CaptureMode.Region)
            {
                SelectionOverlayWindow selector = new(source);
                PixelRect? crop = await selector.SelectAsync();
                if (crop is null || crop.Value.IsEmpty) return;
                document.Crop = crop.Value.Normalize();
            }

            ScreenshotEditorViewModel editorVm = new(document, _workflow, _main.CreateConnectionSettings);
            ScreenshotEditorWindow editor = new() { DataContext = editorVm };
            editor.Show(_owner);
        }
        catch (Exception exception)
        {
            _main.Status = exception.Message;
        }
        finally
        {
            _captureRunning = false;
        }
    }

    public void Dispose()
    {
        _hotkeys.HotkeyPressed -= OnHotkeyPressed;
        _hotkeys.Dispose();
    }

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs args) =>
        Dispatcher.UIThread.Post(() => _ = CaptureAsync(args.Mode));
}
