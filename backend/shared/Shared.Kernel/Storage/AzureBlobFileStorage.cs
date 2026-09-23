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

    /// <summary>
    /// The account-level client a user delegation key is requested from, or
    /// null when this storage was built from a connection string — in which
    /// case the container client signs with the account key itself and no
    /// delegation is needed.
    /// </summary>
    private readonly BlobServiceClient? _service;

    private readonly SemaphoreSlim _keyGate = new(1, 1);
    private UserDelegationKey? _delegationKey;

    public AzureBlobFileStorage(BlobContainerClient container, BlobServiceClient? service = null)
    {
        _container = container;
        _service = service;
    }

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
    /// <b>Two ways to sign, depending on how the client was built.</b> From a
    /// connection string the container client holds the account key and signs
    /// directly. From a managed identity there is no key — and the storage
    /// account in a deployment refuses key access anyway — so the URL is signed
    /// with a <i>user delegation key</i> instead: a short-lived key Azure issues
    /// to the identity itself. It needs the identity to hold Storage Blob Data
    /// Contributor, which deploy/azure grants.
    ///
    /// Before this, a managed-identity deployment answered null here and every
    /// download streamed through the API. That works, and is exactly the kind of
    /// quiet degradation nobody notices — which is why it is now signed rather
    /// than left as the documented fallback.
    ///
    /// Null is still a legitimate answer: when neither route can sign, or Azure
    /// refuses the delegation key, the caller streams the file instead. The
    /// interface says so, and turning a download into a 500 when a working
    /// fallback exists would be the wrong trade.
    /// </summary>
    public async Task<Uri?> GetDownloadUrlAsync(
        string key, TimeSpan lifetime, CancellationToken ct = default)
    {
        BlobClient blob = _container.GetBlobClient(key);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        var builder = new BlobSasBuilder
        {
            BlobContainerName = _container.Name,
            BlobName = key,
            Resource = "b",
            // A minute of leeway for clock skew between here and storage.
            StartsOn = now.AddMinutes(-1),
            ExpiresOn = now.Add(lifetime),
        };

        // Read only. A SAS that could write would let anyone holding the link
        // replace the document it was issued for.
        builder.SetPermissions(BlobSasPermissions.Read);

        if (blob.CanGenerateSasUri)
        {
            return blob.GenerateSasUri(builder);
        }

        if (_service is null)
        {
            return null;
        }

        try
        {
            UserDelegationKey delegation = await GetDelegationKeyAsync(now, lifetime, ct);

            // A SAS cannot outlive the key that signed it; storage would reject
            // it at the moment of use, which is later and more confusing than
            // capping it here.
            if (builder.ExpiresOn > delegation.SignedExpiresOn)
            {
                builder.ExpiresOn = delegation.SignedExpiresOn;
            }

            return new BlobUriBuilder(blob.Uri)
            {
                Sas = builder.ToSasQueryParameters(delegation, _service.AccountName),
            }.ToUri();
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    /// <summary>
    /// One delegation key, reused until it would expire before the URL being
    /// signed does. Asking per URL would put a round trip to Entra ID in front
    /// of every download link a list screen renders.
    ///
    /// Requested for a day beyond what the current URL needs, capped at the
    /// seven days the service allows.
    /// </summary>
    private async Task<UserDelegationKey> GetDelegationKeyAsync(
        DateTimeOffset now, TimeSpan lifetime, CancellationToken ct)
    {
        DateTimeOffset neededUntil = now.Add(lifetime).AddMinutes(5);

        await _keyGate.WaitAsync(ct);

        try
        {
            if (_delegationKey is { } cached && cached.SignedExpiresOn >= neededUntil)
            {
                return cached;
            }

            TimeSpan span = lifetime + TimeSpan.FromDays(1);
            if (span > TimeSpan.FromDays(7))
            {
                span = TimeSpan.FromDays(7);
            }

            Response<UserDelegationKey> response =
                await _service!.GetUserDelegationKeyAsync(now.AddMinutes(-1), now.Add(span), ct);

            _delegationKey = response.Value;

            return response.Value;
        }
        finally
        {
            _keyGate.Release();
        }
    }
}
