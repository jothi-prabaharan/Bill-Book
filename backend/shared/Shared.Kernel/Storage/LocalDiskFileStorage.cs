using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Shared.Kernel.Storage;

/// <summary>
/// Files on local disk under a configured root.
///
/// Shipped as a working implementation rather than a stub, because an interface
/// with nothing behind it is how <c>ISecretStore</c>, <c>IEventPublisher</c> and
/// <c>IEmailSender</c> ended up blocking DI startup in this codebase. This runs
/// with no cloud account at all, and on a single node with a mounted volume it
/// is a legitimate production choice as well as the development default.
///
/// It cannot mint signed URLs, so <see cref="GetDownloadUrlAsync"/> returns null
/// and the API streams the bytes itself.
/// </summary>
public sealed class LocalDiskFileStorage : IFileStorage
{
    private readonly string _root;
    private readonly ILogger<LocalDiskFileStorage> _log;

    public LocalDiskFileStorage(IConfiguration configuration, ILogger<LocalDiskFileStorage> log)
    {
        _log = log;
        _root = configuration["FileStorage:LocalRoot"] is { Length: > 0 } configured
            ? configured
            : Path.Combine(Path.GetTempPath(), "bill-book-files");

        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(
        string key, Stream content, string contentType, CancellationToken ct = default)
    {
        string path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using FileStream file = File.Create(path);
        await content.CopyToAsync(file, ct);

        _log.LogInformation("Stored {Key} ({ContentType})", key, contentType);
        return key;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        string path = ResolvePath(key);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        string path = ResolvePath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <summary>Null: local disk has no signed-URL concept, so the API streams instead.</summary>
    public Task<Uri?> GetDownloadUrlAsync(
        string key, TimeSpan lifetime, CancellationToken ct = default) =>
        Task.FromResult<Uri?>(null);

    /// <summary>
    /// Maps a key to a path, refusing anything that would land outside the root.
    /// Keys are generated rather than typed, but this is the last line between a
    /// storage key and the filesystem and it costs nothing to hold.
    /// </summary>
    private string ResolvePath(string key)
    {
        string full = Path.GetFullPath(Path.Combine(_root, key));
        string root = Path.GetFullPath(_root);

        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Storage key '{key}' resolves outside the root.");
        }

        return full;
    }
}
