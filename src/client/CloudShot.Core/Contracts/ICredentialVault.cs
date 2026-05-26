namespace NextCloudShot.Core.Contracts;

public interface ICredentialVault
{
    Task StoreSecretAsync(string key, string secret, CancellationToken cancellationToken = default);
    Task<string?> ReadSecretAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveSecretAsync(string key, CancellationToken cancellationToken = default);
}
