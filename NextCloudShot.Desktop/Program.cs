using Avalonia;

namespace NextCloudShot.Desktop;

internal static class Program
{
    public static bool ShowSettingsOnStartup { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        ShowSettingsOnStartup = args.Contains("--settings", StringComparer.OrdinalIgnoreCase);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
