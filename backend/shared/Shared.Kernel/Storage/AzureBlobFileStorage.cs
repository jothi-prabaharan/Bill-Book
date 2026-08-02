using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace Shared.Kernel.Storage;

/// <summary>
/// Blob Storage, for deployments. <see cref="LocalDiskFileStorage"/> is the
/// development counterpart; both exist so nothing has to choose between running
/// locally and running for real.
///
/// One container for every tenant, with the organization id as the first path
/// segment — the same shape the local implementation uses, and the reason
/// <see cref="StorageKey.BuildKey"/> is the only thing that composes a key. A
/// container per customer would mean a management-plane call on every signup and
/// a quota nobody watches.
/// </summary>
public sealed class AzureBlobFileStorage : IFileStorage
{
    private readonly BlobContainerClient _container;

    public AzureBlobFileStorage(BlobContainerClient container) => _container = container;

    public async Task<string> SaveAsync(
        string key, Stream content, string contentType, CancellationToken ct = default)
    {
        BlobClient blob = _container.GetBlobClient(key);

        await blob.UploadAsync(
            content,
            new BlobUploadOptions
            {
                // Set on upload rather than after: a second call to set headers
                // can fail on its own and leave a blob served as
                // application/octet-stream, which browsers download instead of
                // displaying and which loses the type the caller validated.
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            },
            ct);

        return key;
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        BlobClient blob = _container.GetBlobClient(key);

        try
        {
            return await blob.OpenReadAsync(cancellationToken: ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Null rather than throwing: the interface says a missing key is an
            // ordinary answer, and a document row can outlive its blob.
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default) =>
        await _container.DeleteBlobIfExistsAsync(
            key, DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);

    /// <summary>
    /// A read-only SAS URL, so the browser fetches the bytes from storage rather
    /// than through the service.
    ///
    /// Returns null when the client cannot sign — which is the case whenever it
    /// was built from a managed identity rather than a shared key. That is not a
    /// failure: the caller streams the file through the API instead, which is
    /// what every deployment does today.
    /// </summary>
    public Task<Uri?> GetDownloadUrlAsync(
        string key, TimeSpan lifetime, CancellationToken ct = default)
    {
        BlobClient blob = _container.GetBlobClient(key);

        if (!blob.CanGenerateSasUri)
        {
            return Task.FromResult<Uri?>(null);
        }

        var builder = new BlobSasBuilder
        {
            BlobContainerName = _container.Name,
            BlobName = key,
            Resource = "b",
            // A minute of leeway for clock skew between here and storage.
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-1),
            ExpiresOn = DateTimeOffset.UtcNow.Add(lifetime),
        };

        // Read only. A SAS that could write would let anyone holding the link
        // replace the document it was issued for.
        builder.SetPermissions(BlobSasPermissions.Read);

        return Task.FromResult<Uri?>(blob.GenerateSasUri(builder));
    }
}
