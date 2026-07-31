namespace Shared.Kernel.Interfaces;

/// <summary>
/// Key Vault abstraction. Tenant connection strings and the SMTP-password
/// encryption key live here, never in the database or config.
/// </summary>
public interface ISecretStore
{
    Task<string> GetSecretAsync(string name, CancellationToken cancellationToken = default);

    Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default);
}
