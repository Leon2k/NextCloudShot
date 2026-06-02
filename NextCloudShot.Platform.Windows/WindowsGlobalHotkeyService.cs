using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using NextCloudShot.Core.Contracts;
using NextCloudShot.Core.Models;

namespace NextCloudShot.Platform.Windows;

public sealed class WindowsGlobalHotkeyService : IGlobalHotkeyService
{
    private const int FirstHotkeyId = 6101;
    private static readonly ConcurrentDictionary<nint, WindowsGlobalHotkeyService> Instances = new();

    private readonly ManualResetEventSlim _ready = new(false);
    private readonly Win32.WindowProcedure _windowProcedure;
    private Thread? _thread;
    private nint _window;
    private Exception? _startupError;
    private GlobalHotkeySettings _settings = GlobalHotkeySettings.Default;
    private readonly Dictionary<int, CaptureAction> _actions = [];

    public WindowsGlobalHotkeyService() => _windowProcedure = WindowProc;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public void Start(GlobalHotkeySettings settings)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Native PrintScreen hotkeys are implemented for Windows only.");
        }
        if (_thread is not null)
        {
            return;
        }

        _ready.Reset();
        _startupError = null;
        _settings = settings;
        _thread = new Thread(MessageLoop) { IsBackground = true, Name = "NextCloudShot.Hotkeys" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait();
        if (_startupError is not null)
        {
            throw new InvalidOperationException("Unable to register NextCloudShot screenshot hotkeys.", _startupError);
        }
    }

    public void Stop()
    {
        if (_window != nint.Zero)
        {
            Win32.PostMessageW(_window, Win32.WM_CLOSE, 0, nint.Zero);
        }
        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }

    public void Dispose()
    {
        Stop();
        _ready.Dispose();
    }

    private void MessageLoop()
    {
        string? className = null;
        nint instance = Win32.GetModuleHandleW(null);
        try
        {
            className = $"NextCloudShotHotkeyWindow_{Environment.ProcessId}";
            Win32.WindowClass wc = new()
            {
                ClassName = className,
                WindowProcedure = _windowProcedure,
                Instance = instance
            };
            if (Win32.RegisterClassW(ref wc) == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            _window = Win32.CreateWindowExW(0, className, string.Empty, 0, 0, 0, 0, 0,
                Win32.HwndMessage, nint.Zero, wc.Instance, nint.Zero);
            if (_window == nint.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            Instances[_window] = this;
            RegisterConfiguredHotkeys();
            _ready.Set();

            while (Win32.GetMessageW(out Win32.Message message, nint.Zero, 0, 0) > 0)
            {
                Win32.TranslateMessage(ref message);
                Win32.DispatchMessageW(ref message);
            }
        }
        catch (Exception exception)
        {
            _startupError = exception;
            _ready.Set();
        }
        finally
        {
            if (_window != nint.Zero)
            {
                foreach (int id in _actions.Keys) Win32.UnregisterHotKey(_window, id);
                _actions.Clear();
                Instances.TryRemove(_window, out _);
                Win32.DestroyWindow(_window);
                _window = nint.Zero;
            }
            if (className is not null)
            {
                Win32.UnregisterClassW(className, instance);
            }
        }
    }

    private void RegisterConfiguredHotkeys()
    {
        if (!_settings.Enabled) return;

        int id = FirstHotkeyId;
        RegisterExpression(ref id, _settings.Region, CaptureAction.Region);
        RegisterExpression(ref id, _settings.RegionAndShare, CaptureAction.RegionAndShare);
        RegisterExpression(ref id, _settings.FullScreen, CaptureAction.FullScreen);
        RegisterExpression(ref id, _settings.ActiveWindow, CaptureAction.ActiveWindow);
        if (_actions.Count == 0)
        {
            throw new InvalidOperationException("Не удалось зарегистрировать ни одного сочетания клавиш.");
        }
    }

    private void RegisterExpression(ref int id, string expression, CaptureAction action)
    {
        foreach (string alternative in expression.Split(["или", "or"], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            (uint modifiers, uint key) = ParseGesture(alternative);
            if (Register(id, modifiers, key))
            {
                _actions[id++] = action;
            }
        }
    }

    private static (uint Modifiers, uint Key) ParseGesture(string expression)
    {
        uint modifiers = 0;
        uint key = 0;
        foreach (string token in expression.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) modifiers |= Win32.MOD_CONTROL;
            else if (token.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= Win32.MOD_SHIFT;
            else if (token.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= Win32.MOD_ALT;
            else if (token.Equals("PrtScr", StringComparison.OrdinalIgnoreCase) || token.Equals("PrintScreen", StringComparison.OrdinalIgnoreCase)) key = Win32.VK_SNAPSHOT;
            else if (token.Length == 1 && char.IsLetterOrDigit(token[0])) key = char.ToUpperInvariant(token[0]);
            else throw new InvalidOperationException($"Неизвестная клавиша: {token}.");
        }

        return key == 0 ? throw new InvalidOperationException($"Не указана клавиша: {expression}.") : (modifiers, key);
    }

    private bool Register(int id, uint modifier, uint key)
    {
        return Win32.RegisterHotKey(_window, id, modifier, key);
    }

    private static nint WindowProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        if (msg == Win32.WM_HOTKEY && Instances.TryGetValue(hwnd, out WindowsGlobalHotkeyService? service) &&
            service._actions.TryGetValue((int)wParam, out CaptureAction action))
        {
            service.HotkeyPressed?.Invoke(service, new HotkeyPressedEventArgs(action));
            return nint.Zero;
        }

        if (msg == Win32.WM_CLOSE)
        {
            Win32.DestroyWindow(hwnd);
            return nint.Zero;
        }

        if (msg == Win32.WM_DESTROY)
        {
            Win32.PostQuitMessage(0);
            return nint.Zero;
        }

        return Win32.DefWindowProcW(hwnd, msg, wParam, lParam);
    }
}
