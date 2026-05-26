using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NextCloudShot.Desktop.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow() => AvaloniaXamlLoader.Load(this);
}
