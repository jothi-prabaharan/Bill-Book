using Shared.Kernel.Documents;

namespace Shared.Infrastructure.DocumentGeneration;

public class SyncfusionPdfGenerator : IPdfGenerator
{
    public Task<byte[]> GenerateAsync(string transactionTypeCode, long sourceId, CancellationToken ct = default)
    {
        // For now, this is a mock implementation.
        // In a real scenario, this would use Syncfusion.Pdf to generate a document based on HTML or layout templates.
        
        string mockContent = $"PDF Content for {transactionTypeCode} #{sourceId}";
        byte[] pdfBytes = System.Text.Encoding.UTF8.GetBytes(mockContent);
        
        return Task.FromResult(pdfBytes);
    }
}
