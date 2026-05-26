using System.Security.Cryptography;
using System.Text;
using NextCloudShot.Core.Contracts;

namespace NextCloudShot.Desktop.Services;

public sealed class DpapiCredentialVault : ICredentialVault
{
    private readonly string _credentialDirectory;

    public DpapiCredentialVault()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        _credentialDirectory = Path.Combine(appData, "NextCloudShot", "credentials");
    }

    public async Task StoreSecretAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("NextCloudShot stores secrets with Windows DPAPI in this build.");
        }

        Directory.CreateDirectory(_credentialDirectory);

        byte[] plainBytes = Encoding.UTF8.GetBytes(secret);
        byte[] protectedBytes = ProtectedData.Protect(plainBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(GetCredentialPath(key), protectedBytes, cancellationToken);
    }

    public async Task<string?> ReadSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("NextCloudShot stores secrets with Windows DPAPI in this build.");
        }

        string credentialPath = GetCredentialPath(key);
        if (!File.Exists(credentialPath))
        {
            return null;
        }

        byte[] protectedBytes = await File.ReadAllBytesAsync(credentialPath, cancellationToken);
        byte[] plainBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public Task RemoveSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string credentialPath = GetCredentialPath(key);
        if (File.Exists(credentialPath))
        {
            File.Delete(credentialPath);
        }

        return Task.CompletedTask;
    }

    private string GetCredentialPath(string key)
    {
        string safeKey = string.Concat(key.Select(character => char.IsLetterOrDigit(character) ? character : '_'));
        return Path.Combine(_credentialDirectory, $"{safeKey}.bin");
    }
}
