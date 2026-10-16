#pragma warning disable SYSLIB0014 // FtpWebRequest is obsolete, but standard for built-in .NET FTP operations without adding external packages

using System.Net;
using Microsoft.Extensions.Logging;

namespace Shared.Kernel.Storage;

/// <summary>Options for connecting to an FTP or FTPS storage server.</summary>
public sealed record FtpStorageOptions(
    string Host,
    int Port,
    string Username,
    string Password,
    string Root,
    bool EnableSsl = false,
    bool UsePassive = true);

/// <summary>
/// File storage backed by an FTP / FTPS server.
/// Uses standard built-in .NET network requests with passive transfer mode.
/// </summary>
public sealed class FtpFileStorage : IFileStorage
{
    private readonly FtpStorageOptions _options;
    private readonly ILogger<FtpFileStorage> _log;

    public FtpFileStorage(FtpStorageOptions options, ILogger<FtpFileStorage> log)
    {
        _options = options;
        _log = log;
    }

    public static string RemotePath(string root, string key)
    {
        if (string.IsNullOrEmpty(key)
            || key.StartsWith('/')
            || key.Contains('\\')
            || key.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidOperationException($"Storage key '{key}' is not a relative path inside the root.");
        }

        string trimmed = (root ?? string.Empty).Trim().Trim('/');
        return trimmed.Length == 0 ? key : $"{trimmed}/{key}";
    }

    private Uri GetUri(string path)
    {
        string host = _options.Host.Trim().TrimEnd('/');
        if (host.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
            host = host[6..];
        else if (host.StartsWith("ftps://", StringComparison.OrdinalIgnoreCase))
            host = host[7..];

        int port = _options.Port > 0 ? _options.Port : (_options.EnableSsl ? 990 : 21);
        string relativePath = path.TrimStart('/');
        return new Uri($"ftp://{host}:{port}/{relativePath}");
    }

    private FtpWebRequest CreateRequest(string path, string method)
    {
        Uri uri = GetUri(path);
        var request = (FtpWebRequest)WebRequest.Create(uri);
        request.Method = method;
        request.Credentials = new NetworkCredential(_options.Username, _options.Password);
        request.EnableSsl = _options.EnableSsl;
        request.UsePassive = _options.UsePassive;
        request.UseBinary = true;
        request.KeepAlive = false;
        request.Timeout = 30000;
        request.ReadWriteTimeout = 60000;
        return request;
    }

    public async Task<string> SaveAsync(
        string key,
        Stream content,
        string contentType,
        FileWriteMode mode = FileWriteMode.CreateNew,
        CancellationToken ct = default)
    {
        string remotePath = RemotePath(_options.Root, key);

        if (mode == FileWriteMode.CreateNew && await ExistsAsync(key, ct))
        {
            throw new StorageKeyExistsException(key);
        }

        await EnsureDirectoryExistsAsync(remotePath, ct);

        FtpWebRequest request = CreateRequest(remotePath, WebRequestMethods.Ftp.UploadFile);

        await using Stream requestStream = await request.GetRequestStreamAsync();
        await content.CopyToAsync(requestStream, ct);
        await requestStream.FlushAsync(ct);

        using var response = (FtpWebResponse)await request.GetResponseAsync();
        _log.LogDebug("FTP Upload complete for {Key}: {StatusCode}", key, response.StatusCode);

        return key;
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        string remotePath = RemotePath(_options.Root, key);
        try
        {
            FtpWebRequest request = CreateRequest(remotePath, WebRequestMethods.Ftp.DownloadFile);
            using var response = (FtpWebResponse)await request.GetResponseAsync();
            await using Stream responseStream = response.GetResponseStream();

            var memoryStream = new MemoryStream();
            await responseStream.CopyToAsync(memoryStream, ct);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (WebException ex) when (IsNotFound(ex))
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        string remotePath = RemotePath(_options.Root, key);
        try
        {
            FtpWebRequest request = CreateRequest(remotePath, WebRequestMethods.Ftp.DeleteFile);
            using var response = (FtpWebResponse)await request.GetResponseAsync();
            _log.LogDebug("FTP Deleted {Key}: {StatusCode}", key, response.StatusCode);
        }
        catch (WebException ex) when (IsNotFound(ex))
        {
            // Already absent — idempotent
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        string remotePath = RemotePath(_options.Root, key);
        try
        {
            FtpWebRequest request = CreateRequest(remotePath, WebRequestMethods.Ftp.GetFileSize);
            using var response = (FtpWebResponse)await request.GetResponseAsync();
            return true;
        }
        catch (WebException ex) when (IsNotFound(ex))
        {
            return false;
        }
        catch (WebException ex)
        {
            _log.LogWarning(ex, "Error checking existence of {Key} on FTP.", key);
            return false;
        }
    }

    public Task<Uri?> GetDownloadUrlAsync(string key, TimeSpan lifetime, CancellationToken ct = default) =>
        Task.FromResult<Uri?>(null);

    private async Task EnsureDirectoryExistsAsync(string remotePath, CancellationToken ct)
    {
        string? directory = Path.GetDirectoryName(remotePath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(directory)) return;

        string[] parts = directory.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string current = "";

        foreach (string part in parts)
        {
            current = string.IsNullOrEmpty(current) ? part : $"{current}/{part}";
            try
            {
                FtpWebRequest mkdRequest = CreateRequest(current, WebRequestMethods.Ftp.MakeDirectory);
                using var response = (FtpWebResponse)await mkdRequest.GetResponseAsync();
            }
            catch (WebException)
            {
                // 550 usually means directory already exists
            }
        }
    }

    private static bool IsNotFound(WebException ex)
    {
        if (ex.Response is FtpWebResponse ftpResponse)
        {
            return ftpResponse.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable
                || ftpResponse.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailableOrBusy;
        }

        return ex.Status == WebExceptionStatus.ProtocolError
            || ex.Message.Contains("550");
    }
}
