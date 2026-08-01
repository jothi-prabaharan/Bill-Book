namespace Shared.Kernel.Storage;

/// <summary>
/// Where uploaded files live. Files never go in the database: a scanned GST
/// certificate in a row makes every backup and every restore carry it.
///
/// Keys are caller-supplied and must start with the organization id, so the
/// tenant boundary exists in the storage path as well as in the row that points
/// at it. <see cref="BuildKey"/> is the only thing that should compose them.
/// </summary>
public interface IFileStorage
{
    /// <summary>Writes the file and returns the key it was stored under.</summary>
    Task<string> SaveAsync(
        string key, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Opens the file for reading, or null when the key does not exist.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// A time-limited URL the browser can fetch directly, or null when the
    /// backing store cannot mint one. A null answer is not a failure — it means
    /// the caller should stream the file through the API instead.
    /// </summary>
    Task<Uri?> GetDownloadUrlAsync(string key, TimeSpan lifetime, CancellationToken ct = default);
}

/// <summary>Composes storage keys so the organization is always the first segment.</summary>
public static class StorageKey
{
    /// <summary>
    /// <c>{orgId}/{area}/{ownerId}/{guid}{extension}</c>. The random component
    /// means an uploaded name never decides where the file lands, so a caller
    /// cannot traverse out of its own prefix by naming a file "../".
    /// </summary>
    public static string BuildKey(Guid orgId, string area, long ownerId, string fileName)
    {
        string extension = Path.GetExtension(fileName);

        // Guard rather than trust: an extension is only ever appended to a name
        // this method generates, but a caller-supplied one containing a slash
        // would still escape the prefix.
        if (extension.Contains('/') || extension.Contains('\\') || extension.Length > 16)
        {
            extension = string.Empty;
        }

        return $"{orgId}/{area}/{ownerId}/{Guid.NewGuid():N}{extension}";
    }
}
