using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using NextCloudShot.Desktop.ViewModels;

namespace NextCloudShot.Desktop.Views;

public sealed partial class ScreenshotEditorWindow : Window
{
    private readonly ScrollViewer _scrollViewer;
    private ScreenshotEditorViewModel? _editor;
    private bool _closeAccepted;
    private bool _promptOpen;

    public ScreenshotEditorWindow()
    {
        AvaloniaXamlLoader.Load(this);
        _scrollViewer = this.FindControl<ScrollViewer>("EditorScrollViewer")!;
        DataContextChanged += OnDataContextChanged;
        KeyDown += OnKeyDown;
        Closing += OnClosing;
        Opened += (_, _) => FitScreenshot();
    }

    private void OnDataContextChanged(object? sender, EventArgs args)
    {
        if (_editor is not null)
        {
            _editor.RequestClose -= OnEditorRequestClose;
        }

        _editor = DataContext as ScreenshotEditorViewModel;
        if (_editor is not null)
        {
            _editor.RequestClose += OnEditorRequestClose;
        }
    }

    private void OnEditorRequestClose(object? sender, EventArgs args)
    {
        _closeAccepted = true;
        Close();
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

    private void OnClosing(object? sender, WindowClosingEventArgs args)
    {
        if (_closeAccepted || DataContext is not ScreenshotEditorViewModel editor || !editor.HasUnsavedChanges)
        {
            return;
        }

        args.Cancel = true;
        if (_promptOpen) return;
        _ = ConfirmCloseAsync(editor);
    }

    private async Task ConfirmCloseAsync(ScreenshotEditorViewModel editor)
    {
        _promptOpen = true;
        try
        {
            CloseChoice choice = await ShowUnsavedPromptAsync();
            switch (choice)
            {
                case CloseChoice.Save:
                    if (await editor.SaveForClosePromptAsync())
                    {
                        _closeAccepted = true;
                        Close();
                    }
                    break;
                case CloseChoice.Discard:
                    _closeAccepted = true;
                    Close();
                    break;
            }
        }
        finally
        {
            _promptOpen = false;
        }
    }

    private async Task<CloseChoice> ShowUnsavedPromptAsync()
    {
        CloseChoice choice = CloseChoice.Cancel;
        Window dialog = new()
        {
            Title = "Сохранить снимок?",
            Width = 430,
            Height = 170,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brush.Parse("#20252B")
        };
        dialog.Content = BuildUnsavedPromptContent(
            save: () => { choice = CloseChoice.Save; dialog.Close(); },
            discard: () => { choice = CloseChoice.Discard; dialog.Close(); },
            cancel: () => { choice = CloseChoice.Cancel; dialog.Close(); });
        await dialog.ShowDialog(this);
        return choice;
    }

    private static Control BuildUnsavedPromptContent(Action save, Action discard, Action cancel)
    {
        TextBlock message = new()
        {
            Text = "Снимок ещё не сохранён. Сохранить его в папку Nextcloud перед закрытием?",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.White,
            Margin = new Avalonia.Thickness(18, 18, 18, 8)
        };
        Button saveButton = DialogButton("Сохранить");
        Button discardButton = DialogButton("Не сохранять");
        Button cancelButton = DialogButton("Отмена");
        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Avalonia.Thickness(18)
        };
        buttons.Children.Add(saveButton);
        buttons.Children.Add(discardButton);
        buttons.Children.Add(cancelButton);

        Grid grid = new()
        {
            RowDefinitions = new RowDefinitions("*,Auto")
        };
        grid.Children.Add(message);
        Grid.SetRow(buttons, 1);
        grid.Children.Add(buttons);

        saveButton.Click += (_, _) => save();
        discardButton.Click += (_, _) => discard();
        cancelButton.Click += (_, _) => cancel();
        return grid;
    }

    private static Button DialogButton(string text) => new()
    {
        Content = text,
        MinWidth = 105,
        Padding = new Avalonia.Thickness(12, 6),
        Background = Brush.Parse("#303841"),
        Foreground = Brushes.White
    };

    private enum CloseChoice
    {
        Cancel,
        Save,
        Discard
    }
}
