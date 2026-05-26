using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NextCloudShot.Core.Contracts;
using NextCloudShot.Core.Services;
using NextCloudShot.Desktop.Services;
using NextCloudShot.Desktop.ViewModels;
using NextCloudShot.Desktop.Views;
using NextCloudShot.Infrastructure.Nextcloud;
using NextCloudShot.Platform.Windows;

namespace NextCloudShot.Desktop;

public sealed partial class App : Application
{
    private CaptureCoordinator? _captureCoordinator;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
            INextCloudShotStorageClient storage = new NextcloudStorageClient(httpClient);
            IAnnotationRenderer renderer = new SkiaAnnotationRenderer();
            MainWindowViewModel mainVm = new(storage);
            MainWindow mainWindow = new() { DataContext = mainVm };
            DesktopClipboardService clipboard = new(() => mainWindow.Clipboard);
            ScreenshotUploadWorkflow uploadWorkflow = new(renderer, storage, clipboard);

            if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            {
                _captureCoordinator = new CaptureCoordinator(
                    new WindowsScreenCaptureService(),
                    new WindowsGlobalHotkeyService(),
                    mainVm,
                    uploadWorkflow,
                    mainWindow);
                mainVm.CaptureRequested += (_, mode) => _ = _captureCoordinator.CaptureAsync(mode);
                _captureCoordinator.Start();
            }
            else
            {
                mainVm.Status = "Global capture is currently implemented on Windows; editor/upload layers remain portable.";
            }

            desktop.MainWindow = mainWindow;
            desktop.Exit += (_, _) => _captureCoordinator?.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
