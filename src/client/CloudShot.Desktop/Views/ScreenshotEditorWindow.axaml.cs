using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NextCloudShot.Desktop.Views;

public sealed partial class ScreenshotEditorWindow : Window
{
    public ScreenshotEditorWindow() => AvaloniaXamlLoader.Load(this);
}
