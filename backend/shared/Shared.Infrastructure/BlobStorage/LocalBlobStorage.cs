using Shared.Kernel.Documents;
using System.IO;

namespace Shared.Infrastructure.BlobStorage;

public class LocalBlobStorage : IBlobStorage
{
    private readonly string _basePath;

    public LocalBlobStorage()
    {
        _basePath = Path.Combine(Path.GetTempPath(), "BillBookBlobStorage");
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveBlobAsync(string containerName, string blobName, byte[] content, string contentType, CancellationToken ct = default)
    {
        string containerPath = Path.Combine(_basePath, containerName);
        Directory.CreateDirectory(containerPath);
        
        string filePath = Path.Combine(containerPath, blobName);
        await File.WriteAllBytesAsync(filePath, content, ct);
        
        return $"file:///{filePath.Replace('\\', '/')}";
    }

    public async Task<byte[]> GetBlobAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        string filePath = Path.Combine(_basePath, containerName, blobName);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Blob {blobName} not found in container {containerName}.");
        }
        
        return await File.ReadAllBytesAsync(filePath, ct);
    }
}
