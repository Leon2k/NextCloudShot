using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NextCloudShot.Desktop.Views;

public sealed partial class MainWindow : Window
{
    private bool _allowClose;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        Closing += OnClosing;
    }

    public void ShowSettings()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs args)
    {
        if (_allowClose) return;
        args.Cancel = true;
        Hide();
    }
}
