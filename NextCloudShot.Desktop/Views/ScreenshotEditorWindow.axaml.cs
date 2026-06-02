using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using NextCloudShot.Desktop.ViewModels;

namespace NextCloudShot.Desktop.Views;

public sealed partial class ScreenshotEditorWindow : Window
{
    private readonly ScrollViewer _scrollViewer;

    public ScreenshotEditorWindow()
    {
        AvaloniaXamlLoader.Load(this);
        _scrollViewer = this.FindControl<ScrollViewer>("EditorScrollViewer")!;
        KeyDown += OnKeyDown;
        Opened += (_, _) => FitScreenshot();
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

    private void FitScreenshot()
    {
        if (DataContext is ScreenshotEditorViewModel editor)
        {
            editor.FitToViewport(_scrollViewer.Bounds.Width, _scrollViewer.Bounds.Height);
        }
    }
}
