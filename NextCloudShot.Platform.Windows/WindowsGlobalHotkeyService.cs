using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using NextCloudShot.Core.Contracts;
using NextCloudShot.Core.Models;

namespace NextCloudShot.Platform.Windows;

public sealed class WindowsGlobalHotkeyService : IGlobalHotkeyService
{
    private const int RegionHotkeyId = 6101;
    private const int ActiveWindowHotkeyId = 6102;
    private static readonly ConcurrentDictionary<nint, WindowsGlobalHotkeyService> Instances = new();

    private readonly ManualResetEventSlim _ready = new(false);
    private readonly Win32.WindowProcedure _windowProcedure;
    private Thread? _thread;
    private nint _window;
    private Exception? _startupError;

    public WindowsGlobalHotkeyService() => _windowProcedure = WindowProc;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public void Start()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Native PrintScreen hotkeys are implemented for Windows only.");
        }
        if (_thread is not null)
        {
            return;
        }

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
        try
        {
            string className = $"NextCloudShotHotkeyWindow_{Environment.ProcessId}";
            Win32.WindowClass wc = new()
            {
                ClassName = className,
                WindowProcedure = _windowProcedure,
                Instance = Win32.GetModuleHandleW(null)
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
            Register(RegionHotkeyId, 0, Win32.VK_SNAPSHOT);
            Register(ActiveWindowHotkeyId, Win32.MOD_ALT, Win32.VK_SNAPSHOT);
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
                Win32.UnregisterHotKey(_window, RegionHotkeyId);
                Win32.UnregisterHotKey(_window, ActiveWindowHotkeyId);
                Instances.TryRemove(_window, out _);
                Win32.DestroyWindow(_window);
                _window = nint.Zero;
            }
        }
    }

    private void Register(int id, uint modifier, uint key)
    {
        if (!Win32.RegisterHotKey(_window, id, modifier, key))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"The hotkey with id {id} is already in use.");
        }
    }

    private static nint WindowProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        if (msg == Win32.WM_HOTKEY && Instances.TryGetValue(hwnd, out WindowsGlobalHotkeyService? service))
        {
            CaptureMode mode = (int)wParam == ActiveWindowHotkeyId ? CaptureMode.ActiveWindow : CaptureMode.Region;
            service.HotkeyPressed?.Invoke(service, new HotkeyPressedEventArgs(mode));
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
