using System.Windows.Input;
using NextCloudShot.Core.Contracts;
using NextCloudShot.Core.Models;

namespace NextCloudShot.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly INextCloudShotStorageClient _storage;
    private string _serverUrl = "https://cloud.example.ru/";
    private string _username = string.Empty;
    private string _appPassword = string.Empty;
    private string _uploadFolder = "/NextCloudShot/Screenshots";
    private bool _createPublicLink = true;
    private string _status = "Ready. PrintScreen: region; Alt + PrintScreen: active window.";

    public MainWindowViewModel(INextCloudShotStorageClient storage)
    {
        _storage = storage;
        CaptureRegionCommand = new RelayCommand(() => CaptureRequested?.Invoke(this, CaptureMode.Region));
        CaptureWindowCommand = new RelayCommand(() => CaptureRequested?.Invoke(this, CaptureMode.ActiveWindow));
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
    public ICommand TestConnectionCommand { get; }

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
            await _storage.TestConnectionAsync(CreateConnectionSettings());
            Status = "Connection accepted by Nextcloud.";
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
    }
}
