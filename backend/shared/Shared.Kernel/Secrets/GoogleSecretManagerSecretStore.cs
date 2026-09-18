using System.Text;
using System.Text.RegularExpressions;
using Google.Api.Gax.ResourceNames;
using Google.Cloud.SecretManager.V1;
using Grpc.Core;
using Shared.Kernel.Interfaces;

namespace Shared.Kernel.Secrets;

/// <summary>
/// Secret Manager, for deployments on Google Cloud. <see cref="KeyVaultSecretStore"/>
/// is the Azure counterpart and <see cref="ConfigurationSecretStore"/> the
/// development one; all three exist so nothing has to choose between running
/// locally, running on one cloud, and running on the other.
///
/// <b>The cache is the same ten minutes, for the same reason.</b> A tenant
/// connection string is read on the way into a request, and a network round trip
/// per request would put Secret Manager's availability in front of every query.
/// Rotating a secret takes hold within that window without a restart.
///
/// <b>Versions are the difference worth knowing about.</b> Key Vault hands back
/// the current value of a named secret; Secret Manager stores an append-only
/// list of versions under that name and makes you say which one. This reads
/// <c>latest</c> always, which is the behaviour the interface describes, and
/// writes by adding a version rather than replacing one — so a rotation that
/// turns out to be wrong is recoverable, and nothing here can destroy the value
/// it is replacing.
///
/// <b>Nothing here logs a value.</b> The name of a secret is safe to say and its
/// contents never are, so a failure names the secret and the RPC status, never
/// what came back.
/// </summary>
public sealed class GoogleSecretManagerSecretStore : ISecretStore
{
    /// <summary>
    /// The same window <see cref="KeyVaultSecretStore.CacheFor"/> uses. Two
    /// stores behind one interface that disagreed about staleness would make a
    /// rotation behave differently depending on which cloud was serving it.
    /// </summary>
    public static readonly TimeSpan CacheFor = KeyVaultSecretStore.CacheFor;

    /// <summary>
    /// Secret Manager's own rule for a secret id: letters, digits, underscores
    /// and hyphens, at most 255 of them. Checked here rather than left to the
    /// service because the failure otherwise arrives as an opaque
    /// <see cref="StatusCode.InvalidArgument"/> at the moment of first use,
    /// which in this system is a request being served rather than a deployment
    /// starting.
    /// </summary>
    private static readonly Regex ValidSecretId =
        new("^[A-Za-z0-9_-]{1,255}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly SecretManagerServiceClient _client;
    private readonly ProjectName _project;
    private readonly TimeProvider _clock;

    private readonly Dictionary<string, (string Value, DateTimeOffset FetchedAt)> _cache =
        new(StringComparer.Ordinal);

    private readonly SemaphoreSlim _gate = new(1, 1);

    public GoogleSecretManagerSecretStore(
        SecretManagerServiceClient client, string projectId, TimeProvider clock)
    {
        _client = client;
        _project = new ProjectName(projectId);
        _clock = clock;
    }

    public async Task<string> GetSecretAsync(
        string name, CancellationToken cancellationToken = default)
    {
        Validate(name);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_cache.TryGetValue(name, out (string Value, DateTimeOffset FetchedAt) hit)
                && _clock.GetUtcNow() - hit.FetchedAt < CacheFor)
            {
                return hit.Value;
            }

            try
            {
                AccessSecretVersionResponse response = await _client.AccessSecretVersionAsync(
                    new SecretVersionName(_project.ProjectId, name, "latest"),
                    cancellationToken);

                // Stored as bytes, and this system only ever puts text in them.
                // Decoding here rather than at the call site keeps every caller
                // out of the question of what encoding a secret is in.
                string value = Encoding.UTF8.GetString(response.Payload.Data.ToByteArray());

                _cache[name] = (value, _clock.GetUtcNow());

                return value;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                // The name, never a value, and never the project id — which is
                // not secret but is a detail worth not scattering through logs.
                throw new KeyNotFoundException(
                    $"Secret '{name}' has no accessible version in Secret Manager.");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Adds a version, creating the secret first when it does not exist yet.
    ///
    /// The create is attempted only after a write has failed with
    /// <see cref="StatusCode.NotFound"/>, rather than probing first: two
    /// services writing the same new secret at once would both see it missing
    /// and both try to create it, and one losing that race with
    /// <see cref="StatusCode.AlreadyExists"/> is an ordinary outcome rather than
    /// a failure — the secret exists, which is all the caller needed.
    /// </summary>
    public async Task SetSecretAsync(
        string name, string value, CancellationToken cancellationToken = default)
    {
        Validate(name);

        SecretPayload payload = new()
        {
            Data = Google.Protobuf.ByteString.CopyFrom(value, Encoding.UTF8),
        };

        var secret = new SecretName(_project.ProjectId, name);

        try
        {
            await _client.AddSecretVersionAsync(secret, payload, cancellationToken);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            await CreateSecretAsync(name, cancellationToken);
            await _client.AddSecretVersionAsync(secret, payload, cancellationToken);
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            // Written through rather than invalidated, so the caller that just
            // set it does not read the old one back for up to ten minutes.
            _cache[name] = (value, _clock.GetUtcNow());
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Automatic replication: Google chooses the regions. The alternative is
    /// naming them, which is a data-residency decision this class is the wrong
    /// place to make — a deployment that needs its secrets pinned to
    /// <c>asia-south1</c> should create the secret with that policy ahead of
    /// time, and this method will then never run for it.
    /// </summary>
    private async Task CreateSecretAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            await _client.CreateSecretAsync(
                _project,
                name,
                new Secret { Replication = new Replication { Automatic = new Replication.Types.Automatic() } },
                cancellationToken);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
        {
            // Lost the race to another writer. The secret exists, which is the
            // only thing this call was for.
        }
    }

    private static void Validate(string name)
    {
        if (!ValidSecretId.IsMatch(name))
        {
            throw new ArgumentException(
                $"'{name}' is not a usable Secret Manager id. Ids are 1-255 characters of "
                + "letters, digits, underscores and hyphens.",
                nameof(name));
        }
    }
}
