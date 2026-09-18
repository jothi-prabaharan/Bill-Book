using Azure.Storage.Blobs;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Kernel.Storage;

/// <summary>
/// Chooses the file store, the way <see cref="Secrets.SecretStoreRegistration"/>
/// chooses the secret store.
///
/// <b>This was copied between two services before it was a method.</b> Master
/// registered it, Sales copied the block when <c>InvoiceService</c> needed it,
/// and a third implementation arriving would have meant editing both — which is
/// the shape of change that gets done once and forgotten once. There is now one
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
    /// Blob Storage when <c>Storage:ConnectionString</c> is set, Cloud Storage
    /// when <c>Gcp:ProjectId</c> is, local disk otherwise.
    ///
    /// <b>Azure wins when both are set</b>, matching the secret store's
    /// precedence for the same reason: a deployment configured for both clouds
    /// is misconfigured, and resolving the same way every time beats resolving
    /// by whichever key was set last.
    ///
    /// <b>There is no Production guard here, unlike the secret store.</b> Local
    /// disk holds uploaded attachments, and losing them with the container is a
    /// visible, recoverable failure — a secret read from the wrong place is
    /// neither. The asymmetry is deliberate rather than an omission.
    /// </summary>
    public static IServiceCollection AddFileStorage(
        this IServiceCollection services, IConfiguration configuration)
    {
        string bucketOrContainer = configuration["Storage:Container"] ?? "documents";

        if (configuration["Storage:ConnectionString"] is { Length: > 0 } azure)
        {
            services.AddSingleton<IFileStorage>(_ =>
            {
                var container = new BlobContainerClient(azure, bucketOrContainer);

                // Created on startup rather than per upload: it is one call, it
                // is idempotent, and the alternative is every first upload in a
                // fresh deployment failing on a container nobody made.
                container.CreateIfNotExists();

                return new AzureBlobFileStorage(container);
            });

            return services;
        }

        if (configuration["Gcp:ProjectId"] is { Length: > 0 } project)
        {
            // A bucket name is global across all of Google Cloud, so "documents"
            // is certainly taken by somebody else. Qualifying it with the project
            // id gives a name that is unique without an operator having to think
            // of one, and Storage:Container still overrides it when they have.
            string bucket = configuration["Storage:Bucket"] is { Length: > 0 } named
                ? named
                : $"{project}-{bucketOrContainer}";

            services.AddSingleton<IFileStorage>(_ =>
            {
                StorageClient client = StorageClient.Create();

                GcsFileStorage.EnsureBucket(client, project, bucket);

                // Resolved once here rather than per request: it reads the
                // ambient credential, which on Cloud Run is a metadata-server
                // round trip.
                return new GcsFileStorage(client, bucket, GcsFileStorage.TryCreateSigner());
            });

            return services;
        }

        services.AddSingleton<IFileStorage, LocalDiskFileStorage>();

        return services;
    }
}
