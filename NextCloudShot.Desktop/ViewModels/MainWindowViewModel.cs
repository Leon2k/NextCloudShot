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
    private string _status = "Ready. PrintScreen: region; Alt + PrintScreen: active window.";

    public MainWindowViewModel(
        INextCloudShotStorageClient storage,
        IDesktopSettingsStore settingsStore,
        ICredentialVault credentialVault)
    {
        _storage = storage;
        _settingsStore = settingsStore;
        _credentialVault = credentialVault;
        CaptureRegionCommand = new RelayCommand(() => CaptureRequested?.Invoke(this, CaptureMode.Region));
        CaptureWindowCommand = new RelayCommand(() => CaptureRequested?.Invoke(this, CaptureMode.ActiveWindow));
        SaveSettingsCommand = new AsyncRelayCommand(() => SaveSettingsAsync(updateStatus: true));
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
    }

    public event EventHandler<CaptureMode>? CaptureRequested;

    public string ServerUrl { get => _serverUrl; set => SetProperty(ref _serverUrl, value); }
    public string Username { get => _username; set => SetProperty(ref _username, value); }
    public string AppPassword { get => _appPassword; set => SetProperty(ref _appPassword, value); }
    public string UploadFolder { get => _uploadFolder; set => SetProperty(ref _uploadFolder, value); }
    public bool CreatePublicLink { get => _createPublicLink; set => SetProperty(ref _createPublicLink, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }

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
            }

            AppPassword = await _credentialVault.ReadSecretAsync(AppPasswordCredentialKey) ?? string.Empty;
            Status = settings is null ? Status : "Settings loaded. PrintScreen: region; Alt + PrintScreen: active window.";
        }
        catch (Exception exception)
        {
            Status = $"Unable to load saved settings: {exception.Message}";
        }
    }

    public NextcloudConnectionSettings CreateConnectionSettings()
    {
        if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out Uri? uri))
        {
            throw new InvalidOperationException("Enter a valid Nextcloud server URL.");
        }
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(AppPassword))
        {
            throw new InvalidOperationException("Enter username and an app password.");
        }
        return new NextcloudConnectionSettings(uri, Username.Trim(), AppPassword, UploadFolder, CreatePublicLink);
    }

    private async Task TestConnectionAsync()
    {
        try
        {
            Status = "Testing connection...";
            NextcloudConnectionInfo connection = await _storage.TestConnectionAsync(CreateConnectionSettings());
            if (NextcloudDefaults.IsDefaultUploadFolder(UploadFolder))
            {
                UploadFolder = NextcloudDefaults.GetUploadFolder(connection.Language);
            }
            await SaveSettingsAsync(updateStatus: false);
            Status = "Connection accepted by Nextcloud.";
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
            CreatePublicLink));

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
            Status = "Settings saved.";
        }
    }
}
