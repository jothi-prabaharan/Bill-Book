using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Google.Apis.Storage.v1.Data;

namespace Shared.Kernel.Storage;

/// <summary>
/// Cloud Storage, for deployments on Google Cloud.
/// <see cref="AzureBlobFileStorage"/> is the Azure counterpart and
/// <see cref="LocalDiskFileStorage"/> the development one.
///
/// One bucket for every tenant, with the organization id as the first path
/// segment — the same shape both siblings use, and the reason
/// <see cref="StorageKey.BuildKey"/> is the only thing that composes a key. A
/// bucket per customer would mean a management-plane call on every signup, a
/// per-project bucket quota nobody watches, and a name collision the first time
/// two deployments picked the same customer code.
///
/// <b>Objects are not listed and keys are not guessed.</b> Nothing here
/// enumerates a prefix, so the tenant boundary in the path is a second line
/// behind the row that points at the key rather than something a caller can walk.
/// </summary>
public sealed class GcsFileStorage : IFileStorage
{
    private readonly StorageClient _client;
    private readonly string _bucket;
    private readonly UrlSigner? _signer;

    /// <param name="signer">
    /// Null when the ambient credential cannot sign, which makes
    /// <see cref="GetDownloadUrlAsync"/> answer null and the caller stream the
    /// file through the API instead. <see cref="TryCreateSigner"/> is what
    /// decides that, once, at startup.
    /// </param>
    public GcsFileStorage(StorageClient client, string bucket, UrlSigner? signer = null)
    {
        _client = client;
        _bucket = bucket;
        _signer = signer;
    }

    public async Task<string> SaveAsync(
        string key, Stream content, string contentType, CancellationToken ct = default)
    {
        await _client.UploadObjectAsync(
            _bucket,
            key,
            // Set on upload rather than after: a second call to patch metadata
            // can fail on its own and leave an object served as
            // application/octet-stream, which browsers download instead of
            // displaying and which loses the type the caller validated.
            contentType,
            content,
            cancellationToken: ct);

        return key;
    }

    /// <summary>
    /// Buffers the object into memory before handing it back.
    ///
    /// The Cloud Storage client downloads *into* a stream rather than handing
    /// one out, so there is no way to return a live network stream here without
    /// a pump and a background task. Buffering is acceptable because
    /// <c>FileStorage:MaxBytes</c> caps an upload at 10 MB — but it is the
    /// reason that cap matters beyond validation, and raising it means
    /// revisiting this method rather than only the setting.
    /// </summary>
    public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        var buffer = new MemoryStream();

        try
        {
            await _client.DownloadObjectAsync(_bucket, key, buffer, cancellationToken: ct);
        }
        catch (Google.GoogleApiException ex)
            when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Null rather than throwing: the interface says a missing key is an
            // ordinary answer, and a document row can outlive its object.
            await buffer.DisposeAsync();
            return null;
        }

        buffer.Position = 0;
        return buffer;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _client.DeleteObjectAsync(_bucket, key, cancellationToken: ct);
        }
        catch (Google.GoogleApiException ex)
            when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Already gone. Delete is idempotent here for the same reason the
            // Azure sibling uses DeleteBlobIfExists: a retried delete must not
            // fail the second time.
        }
    }

    /// <summary>
    /// A V4 signed URL, read-only, so the browser fetches the bytes from storage
    /// rather than through the service.
    ///
    /// <b>Unlike the Azure sibling, this usually works in a deployment.</b> A
    /// managed identity cannot produce a SAS because there is no shared key to
    /// sign with; a Cloud Run service account can sign through the IAM
    /// <c>signBlob</c> API, so the URL is available without mounting a key file.
    /// That needs <c>roles/iam.serviceAccountTokenCreator</c> on the service
    /// account, held over itself.
    ///
    /// Without that grant the signing call fails, and this answers null rather
    /// than throwing — the interface says a null is not a failure, and turning a
    /// download into a 500 when streaming through the API still works would be
    /// the wrong trade. The cost is that a missing IAM grant shows up as
    /// bytes going the slow way rather than as an error, so it is worth
    /// checking deliberately after a deployment rather than waiting to notice.
    /// </summary>
    public async Task<Uri?> GetDownloadUrlAsync(
        string key, TimeSpan lifetime, CancellationToken ct = default)
    {
        if (_signer is null)
        {
            return null;
        }

        try
        {
            string url = await _signer.SignAsync(
                UrlSigner.RequestTemplate
                    .FromBucket(_bucket)
                    .WithObjectName(key)
                    // Read only. A URL that could write would let anyone holding
                    // the link replace the document it was issued for.
                    .WithHttpMethod(HttpMethod.Get),
                UrlSigner.Options.FromDuration(lifetime),
                ct);

            return new Uri(url);
        }
        catch (Exception ex) when (ex is Google.GoogleApiException or HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>
    /// Builds a signer from Application Default Credentials, or null when this
    /// environment has none that can sign.
    ///
    /// Called once at startup rather than per request: it reads the ambient
    /// credential, and on Cloud Run that is a metadata-server round trip.
    /// </summary>
    public static UrlSigner? TryCreateSigner()
    {
        try
        {
            return UrlSigner.FromCredential(GoogleCredential.GetApplicationDefault());
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            // No usable credential, or one of a kind that cannot sign. Both mean
            // the same thing to the caller: stream the file through the API.
            return null;
        }
    }

    /// <summary>
    /// Creates the bucket if it is missing, so a fresh deployment's first upload
    /// does not fail on a bucket nobody made — the same reason the Azure sibling
    /// calls <c>CreateIfNotExists</c> at startup.
    ///
    /// <b>Uniform bucket-level access, and public access prevented.</b> Object
    /// ACLs are off because every read goes through a signed URL or the API, and
    /// a per-object ACL is the mechanism by which one of these documents would
    /// end up world-readable by accident.
    ///
    /// Infrastructure normally creates the bucket ahead of time — the Terraform
    /// under <c>deploy/gcp</c> does — in which case this finds it and returns.
    /// </summary>
    public static void EnsureBucket(StorageClient client, string projectId, string bucket)
    {
        try
        {
            client.GetBucket(bucket);
        }
        catch (Google.GoogleApiException ex)
            when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            try
            {
                client.CreateBucket(projectId, new Bucket
                {
                    Name = bucket,
                    IamConfiguration = new Bucket.IamConfigurationData
                    {
                        UniformBucketLevelAccess =
                            new Bucket.IamConfigurationData.UniformBucketLevelAccessData
                            {
                                Enabled = true,
                            },
                        PublicAccessPrevention = "enforced",
                    },
                });
            }
            catch (Google.GoogleApiException conflict)
                when (conflict.HttpStatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // Another replica won the race. The bucket exists, which is all
                // this call was for.
            }
        }
    }
}
