using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Shared.Kernel.Storage;

/// <summary>
/// Chooses the file store, the way <see cref="Secrets.SecretStoreRegistration"/>
/// chooses the secret store.
///
/// <b>This was copied between two services before it was a method.</b> Master
/// registered it, Sales copied the block when <c>InvoiceService</c> needed it,
/// and a third way of connecting would have meant editing both — which is the
/// shape of change that gets done once and forgotten once. There is now one
/// place that knows how the choice is made.
///
/// <b>Presence of a setting decides, not the environment name.</b> A developer
/// can point at real storage without pretending to be Production, and a
/// deployment cannot silently fall back to a disk that disappears with the
/// container.
/// </summary>
public static class FileStorageRegistration
{
    /// <summary>
    /// Blob Storage by connection string when <c>Storage:ConnectionString</c>
    /// is set, Blob Storage by managed identity when <c>Storage:AccountUrl</c>
    /// is, an SFTP server when <c>Storage:Sftp:Host</c> is, local disk
    /// otherwise. SFTP is for a self-hosted deployment keeping its files on
    /// another machine (deploy/local).
    ///
    /// <b>The connection string is for development</b> — Azurite, or pointing
    /// a local run at a real account. It carries the account key, and the
    /// storage account deploy/azure creates refuses shared-key access outright,
    /// so in a deployment only the account-URL path can work. That is the
    /// point: a key that is not accepted cannot leak into being useful.
    ///
    /// <b>The connection string wins when both are set</b>, so a developer who
    /// has one configured is not surprised by the other. A deployment has no
    /// reason to set it.
    ///
    /// <b>There is no Production guard here, unlike the secret store.</b> Local
    /// disk holds uploaded attachments, and losing them with the container is a
    /// visible, recoverable failure — a secret read from the wrong place is
    /// neither. The asymmetry is deliberate rather than an omission.
    /// </summary>
    public static IServiceCollection AddFileStorage(
        this IServiceCollection services, IConfiguration configuration)
    {
        string containerName = configuration["Storage:Container"] ?? "documents";

        if (configuration["Storage:ConnectionString"] is { Length: > 0 } connection)
        {
            services.AddSingleton<IFileStorage>(_ =>
            {
                var container = new BlobContainerClient(connection, containerName);

                // Created on startup rather than per upload: it is one call, it
                // is idempotent, and the alternative is every first upload in a
                // fresh development account failing on a container nobody made.
                container.CreateIfNotExists();

                return new AzureBlobFileStorage(container);
            });

            return services;
        }

        if (configuration["Storage:AccountUrl"] is { Length: > 0 } accountUrl)
        {
            services.AddSingleton<IFileStorage>(_ =>
            {
                var service = new BlobServiceClient(new Uri(accountUrl), new DefaultAzureCredential());

                // No CreateIfNotExists here. The deployment declares the
                // container, and a managed identity's role assignment can take a
                // few minutes to propagate after a deploy — a create attempted in
                // that window fails with 403 and would take the first upload with
                // it, for a container that already exists.
                return new AzureBlobFileStorage(
                    service.GetBlobContainerClient(containerName), service);
            });

            return services;
        }

        if (configuration["Storage:Sftp:Host"] is { Length: > 0 } host)
        {
            SftpStorageOptions options = SftpOptions(configuration, host);

            services.AddSingleton<IFileStorage>(provider => new SftpFileStorage(
                options, provider.GetRequiredService<ILogger<SftpFileStorage>>()));

            return services;
        }

        services.AddSingleton<IFileStorage, LocalDiskFileStorage>();

        return services;
    }

    /// <summary>
    /// Read once at startup, and refused at startup when incomplete: a store
    /// that could not sign in would otherwise fail on the first upload, hours
    /// later, in front of someone trying to attach a document.
    /// </summary>
    private static SftpStorageOptions SftpOptions(IConfiguration configuration, string host)
    {
        string Required(string key) =>
            configuration[key] is { Length: > 0 } value
                ? value
                : throw new InvalidOperationException(
                    $"Storage:Sftp:Host is set, so file storage is SFTP, but {key} is not configured.");

        int port = configuration["Storage:Sftp:Port"] is { Length: > 0 } text
            ? int.TryParse(text, out int parsed) && parsed is > 0 and <= 65535
                ? parsed
                : throw new InvalidOperationException($"Storage:Sftp:Port '{text}' is not a port number.")
            : 22;

        return new SftpStorageOptions(
            host,
            port,
            Required("Storage:Sftp:Username"),
            Required("Storage:Sftp:Password"),
            configuration["Storage:Sftp:Root"] is { Length: > 0 } root ? root : "billbook-files",
            configuration["Storage:Sftp:HostKeySha256"]);
    }
}
