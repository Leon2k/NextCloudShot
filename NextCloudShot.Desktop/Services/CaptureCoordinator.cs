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
    private bool _captureRunning;

    public CaptureCoordinator(
        IScreenCaptureService capture,
        IGlobalHotkeyService hotkeys,
        MainWindowViewModel main,
        ScreenshotUploadWorkflow workflow)
    {
        _capture = capture;
        _hotkeys = hotkeys;
        _main = main;
        _workflow = workflow;
        _hotkeys.HotkeyPressed += OnHotkeyPressed;
    }

    public void Start()
    {
        try
        {
            _hotkeys.Start(_main.CreateHotkeySettings());
            DesktopDiagnostics.Write("Global hotkeys started.");
        }
        catch (Exception exception)
        {
            _main.Status = exception.Message;
            DesktopDiagnostics.Write($"Global hotkey startup failed: {exception}");
        }
    }

    public void RestartHotkeys()
    {
        _hotkeys.Stop();
        Start();
    }

    public async Task CaptureAsync(CaptureAction action)
    {
        if (_captureRunning) return;
        DesktopDiagnostics.Write($"Capture requested: {action}.");
        _captureRunning = true;
        try
        {
            CaptureMode mode = action switch
            {
                CaptureAction.ActiveWindow => CaptureMode.ActiveWindow,
                CaptureAction.FullScreen => CaptureMode.FullScreen,
                _ => CaptureMode.Region
            };
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

            if (action == CaptureAction.RegionAndShare)
            {
                UploadResult result = await _workflow.UploadAndCopyLinkAsync(
                    document,
                    _main.CreateConnectionSettings(),
                    _main.CreateOutputSettings());
                _main.Status = result.PublicUrl is null
                    ? $"Снимок загружен: {result.RemotePath}"
                    : "Снимок загружен, публичная ссылка скопирована.";
                return;
            }

            ScreenshotEditorViewModel editorVm = new(
                document,
                _workflow,
                _main.CreateConnectionSettings,
                _main.CreateOutputSettings);
            ScreenshotEditorWindow editor = new() { DataContext = editorVm };
            editor.Show();
        }
        catch (Exception exception)
        {
            _main.Status = exception.Message;
            DesktopDiagnostics.Write($"Capture failed: {exception}");
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
        Dispatcher.UIThread.Post(() => _ = CaptureAsync(args.Action));
}
