namespace Shared.Kernel.Storage;

/// <summary>
/// Where uploaded files live. Files never go in the database: a scanned GST
/// certificate in a row makes every backup and every restore carry it.
///
/// Keys are composed by <see cref="StorageKey"/> and nothing else:
/// <c>{customerCode}/{orgId}/{app}/{module}/…</c>, so the tenant boundary exists
/// in the storage path as well as in the row that points at it, and an operator
/// browsing the container sees whose files they are.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Writes the file and returns the key it was stored under.
    ///
    /// <b>Refuses to overwrite unless told to.</b> With
    /// <see cref="FileWriteMode.CreateNew"/> — the default — a file already at
    /// the key raises <see cref="StorageKeyExistsException"/> and nothing is
    /// written. The refusal is the write's own condition, not a separate
    /// existence check beforehand: a check-then-write leaves a gap in which two
    /// saves both see "absent" and the second silently replaces the first.
    ///
    /// Folders need no check and no creation. In Blob Storage a folder is only
    /// the shared start of its files' names, so writing a file brings every
    /// folder in its path into being; on local disk the directories are created
    /// with the file.
    /// </summary>
    Task<string> SaveAsync(
        string key,
        Stream content,
        string contentType,
        FileWriteMode mode = FileWriteMode.CreateNew,
        CancellationToken ct = default);

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

/// <summary>What a save does when a file already exists at its key.</summary>
public enum FileWriteMode
{
    /// <summary>
    /// Write only if nothing is there; otherwise throw
    /// <see cref="StorageKeyExistsException"/>. Right for anything a person
    /// uploads, where a second file at the same key can only be a mistake.
    /// </summary>
    CreateNew = 1,

    /// <summary>
    /// Replace whatever is there. Only for documents the system regenerates
    /// under a name it chose — where the same key means the same document —
    /// and only because blob versioning keeps the copy being replaced.
    /// </summary>
    Replace = 2,
}

/// <summary>
/// A <see cref="FileWriteMode.CreateNew"/> save found a file already at its key.
/// Nothing was written. Carries the key, which is a path of ids and never
/// anything a customer typed.
/// </summary>
public sealed class StorageKeyExistsException(string key)
    : IOException($"A file already exists at '{key}'; nothing was written.")
{
    public string Key { get; } = key;
}
