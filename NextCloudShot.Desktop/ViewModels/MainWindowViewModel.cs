using System.Windows.Input;
using NextCloudShot.Core.Contracts;
using NextCloudShot.Core.Models;
using NextCloudShot.Desktop.Services;

namespace NextCloudShot.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly INextCloudShotStorageClient _storage;
    private readonly IDesktopSettingsStore _settingsStore;
    private readonly ICredentialVault _credentialVault;
    private const string AppPasswordCredentialKey = "nextcloud-app-password";
    private string _serverUrl = "https://cloud.example.ru/";
    private string _username = string.Empty;
    private string _appPassword = string.Empty;
    private string _uploadFolder = NextcloudDefaults.UploadFolder;
    private bool _createPublicLink = true;
    private bool _hotkeysEnabled = true;
    private string _regionHotkey = GlobalHotkeySettings.Default.Region;
    private string _regionAndShareHotkey = GlobalHotkeySettings.Default.RegionAndShare;
    private string _fullScreenHotkey = GlobalHotkeySettings.Default.FullScreen;
    private string _activeWindowHotkey = GlobalHotkeySettings.Default.ActiveWindow;
    private string _fileNamePattern = ScreenshotOutputSettings.Default.FileNamePattern;
    private ScreenshotFileFormat _format = ScreenshotOutputSettings.Default.Format;
    private string _status = "Готово. Приложение работает в области уведомлений.";

    public MainWindowViewModel(
        INextCloudShotStorageClient storage,
        IDesktopSettingsStore settingsStore,
        ICredentialVault credentialVault)
    {
        _storage = storage;
        _settingsStore = settingsStore;
        _credentialVault = credentialVault;
        CaptureRegionCommand = new RelayCommand(() => CaptureRequested?.Invoke(this, CaptureAction.Region));
        CaptureWindowCommand = new RelayCommand(() => CaptureRequested?.Invoke(this, CaptureAction.ActiveWindow));
        SaveSettingsCommand = new AsyncRelayCommand(() => SaveSettingsAsync(updateStatus: true));
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
    }

    public event EventHandler<CaptureAction>? CaptureRequested;
    public event EventHandler? SettingsSaved;

    public string ServerUrl { get => _serverUrl; set => SetProperty(ref _serverUrl, value); }
    public string Username { get => _username; set => SetProperty(ref _username, value); }
    public string AppPassword { get => _appPassword; set => SetProperty(ref _appPassword, value); }
    public string UploadFolder { get => _uploadFolder; set => SetProperty(ref _uploadFolder, value); }
    public bool CreatePublicLink { get => _createPublicLink; set => SetProperty(ref _createPublicLink, value); }
    public bool HotkeysEnabled { get => _hotkeysEnabled; set => SetProperty(ref _hotkeysEnabled, value); }
    public string RegionHotkey { get => _regionHotkey; set => SetProperty(ref _regionHotkey, value); }
    public string RegionAndShareHotkey { get => _regionAndShareHotkey; set => SetProperty(ref _regionAndShareHotkey, value); }
    public string FullScreenHotkey { get => _fullScreenHotkey; set => SetProperty(ref _fullScreenHotkey, value); }
    public string ActiveWindowHotkey { get => _activeWindowHotkey; set => SetProperty(ref _activeWindowHotkey, value); }
    public string FileNamePattern { get => _fileNamePattern; set => SetProperty(ref _fileNamePattern, value); }
    public ScreenshotFileFormat Format
    {
        get => _format;
        set
        {
            if (SetProperty(ref _format, value)) RaisePropertyChanged(nameof(SelectedFormat));
        }
    }
    public string SelectedFormat
    {
        get => Format == ScreenshotFileFormat.Jpeg ? "JPEG" : "PNG";
        set
        {
            Format = string.Equals(value, "JPEG", StringComparison.OrdinalIgnoreCase)
                ? ScreenshotFileFormat.Jpeg
                : ScreenshotFileFormat.Png;
        }
    }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public IReadOnlyList<string> FileNamePatterns { get; } = ["Дата + время", "Дата + время + Название окна"];
    public IReadOnlyList<string> Formats { get; } = ["PNG", "JPEG"];

    public ICommand CaptureRegionCommand { get; }
    public ICommand CaptureWindowCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand TestConnectionCommand { get; }

    public async Task LoadSettingsAsync()
    {
        try
        {
            DesktopSettings? settings = await _settingsStore.LoadAsync();
            if (settings is not null)
            {
                ServerUrl = settings.ServerUrl;
                Username = settings.Username;
                UploadFolder = settings.UploadFolder;
                CreatePublicLink = settings.CreatePublicLink;
                ApplyHotkeys(settings.Hotkeys ?? GlobalHotkeySettings.Default);
                FileNamePattern = settings.FileNamePattern ?? ScreenshotOutputSettings.Default.FileNamePattern;
                Format = settings.Format;
            }

            AppPassword = await _credentialVault.ReadSecretAsync(AppPasswordCredentialKey) ?? string.Empty;
            Status = settings is null ? Status : "Настройки загружены. Приложение работает в области уведомлений.";
        }
        catch (Exception exception)
        {
            Status = $"Не удалось загрузить сохранённые настройки: {exception.Message}";
        }
    }

    public NextcloudConnectionSettings CreateConnectionSettings()
    {
        if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out Uri? uri))
        {
            throw new InvalidOperationException("Введите корректный адрес сервера Nextcloud.");
        }
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(AppPassword))
        {
            throw new InvalidOperationException("Введите логин и пароль приложения Nextcloud.");
        }
        return new NextcloudConnectionSettings(uri, Username.Trim(), AppPassword, UploadFolder, CreatePublicLink);
    }

    public GlobalHotkeySettings CreateHotkeySettings() => new(
        HotkeysEnabled,
        RegionHotkey.Trim(),
        RegionAndShareHotkey.Trim(),
        FullScreenHotkey.Trim(),
        ActiveWindowHotkey.Trim());

    public ScreenshotOutputSettings CreateOutputSettings() => new(FileNamePattern.Trim(), Format);

    private async Task TestConnectionAsync()
    {
        try
        {
            Status = "Проверка подключения...";
            NextcloudConnectionInfo connection = await _storage.TestConnectionAsync(CreateConnectionSettings());
            if (NextcloudDefaults.IsDefaultUploadFolder(UploadFolder))
            {
                UploadFolder = NextcloudDefaults.GetUploadFolder(connection.Language);
            }
            await SaveSettingsAsync(updateStatus: false);
            Status = $"Подключение к Nextcloud установлено. Язык профиля: {connection.Language ?? "не определен"}.";
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
    }

    private async Task SaveSettingsAsync(bool updateStatus)
    {
        await _settingsStore.SaveAsync(new DesktopSettings(
            ServerUrl.Trim(),
            Username.Trim(),
            UploadFolder.Trim(),
            CreatePublicLink,
            CreateHotkeySettings(),
            FileNamePattern.Trim(),
            Format));

        if (string.IsNullOrWhiteSpace(AppPassword))
        {
            await _credentialVault.RemoveSecretAsync(AppPasswordCredentialKey);
        }
        else
        {
            await _credentialVault.StoreSecretAsync(AppPasswordCredentialKey, AppPassword);
        }

        if (updateStatus)
        {
            Status = "Настройки сохранены.";
        }

        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyHotkeys(GlobalHotkeySettings hotkeys)
    {
        HotkeysEnabled = hotkeys.Enabled;
        RegionHotkey = hotkeys.Region;
        RegionAndShareHotkey = hotkeys.RegionAndShare;
        FullScreenHotkey = hotkeys.FullScreen;
        ActiveWindowHotkey = hotkeys.ActiveWindow;
    }
}
