using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using NextCloudShot.Core.Models;
using CorePoint = NextCloudShot.Core.Models.PixelPoint;
using CoreRect = NextCloudShot.Core.Models.PixelRect;
using AvaloniaPoint = Avalonia.Point;

namespace NextCloudShot.Desktop.Views;

public sealed partial class SelectionOverlayWindow : Window
{
    private readonly TaskCompletionSource<CoreRect?> _completion = new();
    private readonly Image _image;
    private readonly Border _selectionBorder;
    private AvaloniaPoint? _start;

    public SelectionOverlayWindow()
    {
        AvaloniaXamlLoader.Load(this);
        _image = this.FindControl<Image>("SourceImage")!;
        _selectionBorder = this.FindControl<Border>("SelectionBorder")!;
    }

    public SelectionOverlayWindow(ScreenshotImage source)
        : this()
    {
        using MemoryStream stream = new(source.PngBytes);
        _image.Source = new Bitmap(stream);
        Width = source.PixelSize.Width;
        Height = source.PixelSize.Height;
        Position = new Avalonia.PixelPoint((int)source.DesktopOrigin.X, (int)source.DesktopOrigin.Y);

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        KeyDown += OnKeyDown;
        Closed += (_, _) => _completion.TrySetResult(null);
    }

    public Task<CoreRect?> SelectAsync()
    {
        Show();
        Activate();
        Focus();
        return _completion.Task;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        _start = args.GetPosition(this);
        _selectionBorder.IsVisible = true;
        UpdateSelection(_start.Value, _start.Value);
        args.Pointer.Capture(this);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs args)
    {
        if (_start is AvaloniaPoint start && args.Pointer.Captured == this)
        {
            UpdateSelection(start, args.GetPosition(this));
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (_start is not AvaloniaPoint start) return;
        AvaloniaPoint end = args.GetPosition(this);
        args.Pointer.Capture(null);
        CoreRect crop = ToRect(start, end).Normalize();
        if (!crop.IsEmpty)
        {
            _completion.TrySetResult(crop);
            Close();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Escape)
        {
            _completion.TrySetResult(null);
            Close();
        }
    }

    private void UpdateSelection(AvaloniaPoint start, AvaloniaPoint end)
    {
        CoreRect rect = ToRect(start, end).Normalize();
        Canvas.SetLeft(_selectionBorder, rect.X);
        Canvas.SetTop(_selectionBorder, rect.Y);
        _selectionBorder.Width = rect.Width;
        _selectionBorder.Height = rect.Height;
    }

    private static CoreRect ToRect(AvaloniaPoint start, AvaloniaPoint end) =>
        new(start.X, start.Y, end.X - start.X, end.Y - start.Y);
}
