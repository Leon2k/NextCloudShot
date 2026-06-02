using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using NextCloudShot.Core.Contracts;
using NextCloudShot.Core.Models;
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
    private TrayIcon? _trayIcon;
    private MainWindow? _settingsWindow;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
            INextCloudShotStorageClient storage = new NextcloudStorageClient(httpClient);
            ICredentialVault credentialVault = new DpapiCredentialVault();
            IDesktopSettingsStore settingsStore = new JsonDesktopSettingsStore();
            IAnnotationRenderer renderer = new SkiaAnnotationRenderer();
            MainWindowViewModel mainVm = new(storage, settingsStore, credentialVault);
            MainWindow mainWindow = new() { DataContext = mainVm };
            _settingsWindow = mainWindow;
            DesktopClipboardService clipboard = new(() =>
                desktop.Windows.FirstOrDefault(window => window.IsActive)?.Clipboard ?? mainWindow.Clipboard);
            ScreenshotUploadWorkflow uploadWorkflow = new(renderer, storage, clipboard);

            if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            {
                _captureCoordinator = new CaptureCoordinator(
                    new WindowsScreenCaptureService(),
                    new WindowsGlobalHotkeyService(),
                    mainVm,
                    uploadWorkflow);
                mainVm.CaptureRequested += (_, action) => _ = _captureCoordinator.CaptureAsync(action);
                mainVm.SettingsSaved += (_, _) => _captureCoordinator.RestartHotkeys();
            }
            else
            {
                mainVm.Status = "Global capture is currently implemented on Windows; editor/upload layers remain portable.";
            }

            _trayIcon = CreateTrayIcon(mainWindow, desktop);
            desktop.Exit += (_, _) =>
            {
                _trayIcon?.Dispose();
                _captureCoordinator?.Dispose();
            };
            _ = InitializeAsync(mainVm, mainWindow);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private async Task InitializeAsync(MainWindowViewModel mainVm, MainWindow mainWindow)
    {
        DesktopDiagnostics.Write("Application initialization started.");
        await mainVm.LoadSettingsAsync();
        _captureCoordinator?.Start();

        // Create the platform implementation once so clipboard access also works while settings stay hidden.
        mainWindow.Show();
        mainWindow.Hide();
        if (Program.ShowSettingsOnStartup) mainWindow.ShowSettings();
        DesktopDiagnostics.Write("Application initialized in tray mode.");
    }

    private TrayIcon CreateTrayIcon(MainWindow settingsWindow, IClassicDesktopStyleApplicationLifetime desktop)
    {
        NativeMenu menu = new();
        NativeMenuItem settings = new("Настройки");
        settings.Click += (_, _) => settingsWindow.ShowSettings();
        NativeMenuItem region = new("Снимок области");
        region.Click += (_, _) => _ = _captureCoordinator?.CaptureAsync(CaptureAction.Region);
        NativeMenuItem window = new("Снимок окна");
        window.Click += (_, _) => _ = _captureCoordinator?.CaptureAsync(CaptureAction.ActiveWindow);
        NativeMenuItem exit = new("Выход");
        exit.Click += (_, _) =>
        {
            settingsWindow.CloseForExit();
            desktop.Shutdown();
        };
        menu.Items.Add(settings);
        menu.Items.Add(region);
        menu.Items.Add(window);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exit);

        TrayIcon trayIcon = new()
        {
            Icon = CreateTrayWindowIcon(),
            ToolTipText = "NextCloudShot",
            Menu = menu,
            IsVisible = true
        };
        trayIcon.Clicked += (_, _) => settingsWindow.ShowSettings();
        return trayIcon;
    }

    private static WindowIcon CreateTrayWindowIcon()
    {
        using Stream icon = AssetLoader.Open(new Uri("avares://NextCloudShot.Desktop/Assets/app-icon.ico"));
        return new WindowIcon(icon);
    }
}
