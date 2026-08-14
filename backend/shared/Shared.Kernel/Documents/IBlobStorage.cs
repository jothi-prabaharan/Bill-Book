namespace Shared.Kernel.Documents;

public interface IBlobStorage
{
    /// <summary>
    /// Saves a blob asynchronously. Returns the URI of the saved blob.
    /// </summary>
    Task<string> SaveBlobAsync(string containerName, string blobName, byte[] content, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a blob asynchronously.
    /// </summary>
    Task<byte[]> GetBlobAsync(string containerName, string blobName, CancellationToken ct = default);
}
