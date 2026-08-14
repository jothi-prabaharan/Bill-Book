namespace Shared.Kernel.Documents;

public interface IPdfGenerator
{
    /// <summary>
    /// Generates a PDF document for a specified transaction type and source ID.
    /// Returns the generated PDF as a byte array.
    /// </summary>
    Task<byte[]> GenerateAsync(string transactionTypeCode, long sourceId, CancellationToken ct = default);
}
