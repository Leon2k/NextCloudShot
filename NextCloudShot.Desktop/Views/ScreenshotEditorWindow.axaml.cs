using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using NextCloudShot.Desktop.ViewModels;

namespace NextCloudShot.Desktop.Views;

public sealed partial class ScreenshotEditorWindow : Window
{
    public ScreenshotEditorWindow()
    {
        AvaloniaXamlLoader.Load(this);
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (DataContext is not ScreenshotEditorViewModel editor || args.KeyModifiers != KeyModifiers.Control)
        {
            return;
        }

        if (args.Key == Key.Z && editor.UndoCommand.CanExecute(null))
        {
            editor.UndoCommand.Execute(null);
            args.Handled = true;
        }
        else if (args.Key == Key.Y && editor.RedoCommand.CanExecute(null))
        {
            editor.RedoCommand.Execute(null);
            args.Handled = true;
        }
    }
}
