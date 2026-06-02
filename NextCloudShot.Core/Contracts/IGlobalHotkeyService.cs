using NextCloudShot.Core.Models;

namespace NextCloudShot.Core.Contracts;

public interface IGlobalHotkeyService : IDisposable
{
    event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;
    void Start(GlobalHotkeySettings settings);
    void Stop();
}
