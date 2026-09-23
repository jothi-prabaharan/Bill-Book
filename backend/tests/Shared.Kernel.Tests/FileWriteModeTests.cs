using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Kernel.Storage;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// What a save does about folders and about a file already at its key, asserted
/// against a real directory rather than a stub — the refusal is the operating
/// system's own, and a stub would only prove the test agrees with itself.
///
/// Blob Storage makes the same promise through If-None-Match on the upload,
/// which needs a storage service to assert against; it was checked by hand
/// against Azurite when this was written.
/// </summary>
public sealed class FileWriteModeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"bb-write-mode-{Guid.NewGuid():N}");
    private readonly LocalDiskFileStorage _storage;

    public FileWriteModeTests()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("FileStorage:LocalRoot", _root)])
            .Build();

        _storage = new LocalDiskFileStorage(config, NullLogger<LocalDiskFileStorage>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static MemoryStream Body(string text) => new(Encoding.UTF8.GetBytes(text));

    private async Task<string> Read(string key)
    {
        await using Stream? stream = await _storage.OpenReadAsync(key);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        return await reader.ReadToEndAsync();
    }

    private const string Key = "0000000042/3f2c9a1e-0000-4000-8000-000000000001/retail-erp/sales/invoices/5521.pdf";

    [Fact]
    public async Task Every_folder_in_the_path_is_created_by_the_save()
    {
        await _storage.SaveAsync(Key, Body("first"), "application/pdf");

        Assert.True(File.Exists(Path.Combine(_root, Key)));
    }

    [Fact]
    public async Task A_second_create_only_save_is_refused_and_the_first_file_survives()
    {
        await _storage.SaveAsync(Key, Body("first"), "application/pdf");

        var ex = await Assert.ThrowsAsync<StorageKeyExistsException>(
            () => _storage.SaveAsync(Key, Body("second"), "application/pdf"));

        Assert.Equal(Key, ex.Key);

        // The refusal wrote nothing: not a truncated file, not the new bytes.
        Assert.Equal("first", await Read(Key));
    }

    [Fact]
    public async Task Create_only_is_the_default()
    {
        await _storage.SaveAsync(Key, Body("first"), "application/pdf");

        await Assert.ThrowsAsync<StorageKeyExistsException>(
            () => _storage.SaveAsync(Key, Body("second"), "application/pdf", ct: CancellationToken.None));
    }

    [Fact]
    public async Task Replace_overwrites_when_asked_to()
    {
        await _storage.SaveAsync(Key, Body("first"), "application/pdf");
        await _storage.SaveAsync(Key, Body("second"), "application/pdf", FileWriteMode.Replace);

        Assert.Equal("second", await Read(Key));
    }
}
