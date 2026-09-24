using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Shared.Kernel.Storage;

/// <summary>Where the SFTP file store connects, and as whom.</summary>
/// <param name="Root">
/// The folder every key is stored under. A relative path is resolved against
/// the account's home folder, which is what a chrooted account needs.
/// </param>
/// <param name="HostKeySha256">
/// The server's host key fingerprint, as <c>ssh-keygen -lf</c> prints it
/// (<c>SHA256:…</c>, the prefix optional). When set, a server presenting any
/// other key is refused before the password is sent. When not set, any key is
/// accepted — right for a server on the same Docker network, wrong across the
/// internet.
/// </param>
public sealed record SftpStorageOptions(
    string Host,
    int Port,
    string Username,
    string Password,
    string Root,
    string? HostKeySha256);

/// <summary>
/// Files on an SFTP server — a NAS, another machine, or the SFTP container
/// deploy/local runs beside the services.
///
/// <b>SFTP, not FTP.</b> Plain FTP sends the password and every file readable to
/// anyone on the network. And only SFTP can keep <see cref="IFileStorage"/>'s
/// promise that a create-only save is refused by the server itself: it opens
/// with an exclusive create (<c>SSH_FXF_EXCL</c>), where FTP can only check for
/// the file and then write it, which two saves can both get through.
///
/// <b>One connection per operation.</b> A save is an attachment upload or an
/// invoice archive — a few a minute, not a few a millisecond — so the SSH
/// handshake is not the cost worth optimising. What one connection per call
/// buys is that nothing is shared between requests: no client to reconnect
/// after the server restarts, and no question of whether two requests may use
/// one channel at the same time.
///
/// <b>Folders are checked and created on every save</b>, one level at a time,
/// because an SFTP server — unlike Blob Storage — will not create them with the
/// file. A folder another save creates at the same moment is not an error.
///
/// There are no signed URLs, so <see cref="GetDownloadUrlAsync"/> returns null
/// and the API streams the file, as it does for local disk.
/// </summary>
public sealed class SftpFileStorage : IFileStorage
{
    private readonly SftpStorageOptions _options;
    private readonly ILogger<SftpFileStorage> _log;

    public SftpFileStorage(SftpStorageOptions options, ILogger<SftpFileStorage> log)
    {
        _options = options;
        _log = log;
    }

    public async Task<string> SaveAsync(
        string key,
        Stream content,
        string contentType,
        FileWriteMode mode = FileWriteMode.CreateNew,
        CancellationToken ct = default)
    {
        string path = RemotePath(_options.Root, key);

        using SftpClient client = await ConnectAsync(ct);
        await EnsureFoldersAsync(client, path, ct);

        Stream remote;

        try
        {
            // CreateNew is the server's exclusive create, so the refusal cannot
            // race with another save the way an existence check followed by a
            // write could.
            remote = await client.OpenAsync(
                path,
                mode == FileWriteMode.Replace ? FileMode.Create : FileMode.CreateNew,
                FileAccess.Write,
                ct);
        }
        catch (SftpException) when (mode == FileWriteMode.CreateNew)
        {
            // SFTP version 3 reports "already exists" as a generic failure, so
            // this asks which it was. The answer only names the error: the
            // write has already been refused either way.
            if (await client.ExistsAsync(path, ct))
            {
                throw new StorageKeyExistsException(key);
            }

            throw;
        }

        await using (remote)
        {
            await content.CopyToAsync(remote, ct);
        }

        _log.LogInformation("Stored {Key} ({ContentType}) over SFTP", key, contentType);
        return key;
    }

    /// <summary>
    /// Downloads the whole file before returning it, so the connection can be
    /// closed at once rather than held open by a caller still reading. Files are
    /// attachments and documents of a few megabytes at most.
    /// </summary>
    public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        string path = RemotePath(_options.Root, key);

        using SftpClient client = await ConnectAsync(ct);
        var buffer = new MemoryStream();

        try
        {
            await client.DownloadFileAsync(path, buffer, ct);
        }
        catch (SftpPathNotFoundException)
        {
            await buffer.DisposeAsync();
            return null;
        }

        buffer.Position = 0;
        return buffer;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        string path = RemotePath(_options.Root, key);

        using SftpClient client = await ConnectAsync(ct);

        try
        {
            await client.DeleteFileAsync(path, ct);
        }
        catch (SftpPathNotFoundException)
        {
            // Already gone, which is what was asked for.
        }
    }

    /// <summary>Null: SFTP has no signed-URL concept, so the API streams instead.</summary>
    public Task<Uri?> GetDownloadUrlAsync(
        string key, TimeSpan lifetime, CancellationToken ct = default) =>
        Task.FromResult<Uri?>(null);

    /// <summary>
    /// The key under the root, refusing anything that could land outside it.
    /// Keys come from <see cref="StorageKey"/>, which validates every segment,
    /// but this is the last line between a key and someone else's folder on a
    /// server that may hold more than this product's files.
    /// </summary>
    public static string RemotePath(string root, string key)
    {
        if (string.IsNullOrEmpty(key)
            || key.StartsWith('/')
            || key.Contains('\\')
            || key.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidOperationException($"Storage key '{key}' is not a relative path inside the root.");
        }

        string trimmed = root.TrimEnd('/');
        return trimmed.Length == 0 ? key : $"{trimmed}/{key}";
    }

    /// <summary>
    /// Whether a fingerprint the server presented matches the configured one.
    /// Accepts the form <c>ssh-keygen</c> prints (<c>SHA256:</c> and unpadded
    /// base64) and tolerates padding, and compares in constant time.
    /// </summary>
    public static bool FingerprintMatches(string presented, string configured)
    {
        static string Normalise(string value)
        {
            string v = value.Trim();
            if (v.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase))
            {
                v = v["SHA256:".Length..];
            }

            return v.TrimEnd('=');
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Normalise(presented)),
            Encoding.ASCII.GetBytes(Normalise(configured)));
    }

    private async Task<SftpClient> ConnectAsync(CancellationToken ct)
    {
        var client = new SftpClient(_options.Host, _options.Port, _options.Username, _options.Password);

        if (_options.HostKeySha256 is { Length: > 0 } pinned)
        {
            client.HostKeyReceived += (_, e) =>
            {
                e.CanTrust = FingerprintMatches(e.FingerPrintSHA256, pinned);

                if (!e.CanTrust)
                {
                    // Named in the log because the exception SSH.NET raises says
                    // only that the key was not trusted, not which key it saw.
                    _log.LogError(
                        "SFTP server {Host} presented host key SHA256:{Presented}, which is not the "
                        + "configured Storage:Sftp:HostKeySha256. Refusing to connect.",
                        _options.Host, e.FingerPrintSHA256);
                }
            };
        }

        try
        {
            await client.ConnectAsync(ct);
        }
        catch
        {
            client.Dispose();
            throw;
        }

        return client;
    }

    /// <summary>
    /// Creates each missing folder between the root and the file. The root is
    /// not created: it belongs to whoever set up the server, and a missing one is
    /// a configuration mistake better reported than papered over.
    /// </summary>
    private async Task EnsureFoldersAsync(SftpClient client, string filePath, CancellationToken ct)
    {
        string root = _options.Root.TrimEnd('/');
        string relative = root.Length == 0 ? filePath : filePath[(root.Length + 1)..];
        string[] folders = relative.Split('/')[..^1];

        string current = root;

        foreach (string folder in folders)
        {
            current = current.Length == 0 ? folder : $"{current}/{folder}";

            if (await client.ExistsAsync(current, ct))
            {
                continue;
            }

            try
            {
                await client.CreateDirectoryAsync(current, ct);
            }
            catch (SftpException) when (client.Exists(current))
            {
                // Another save created it between the check and here.
            }
        }
    }
}
