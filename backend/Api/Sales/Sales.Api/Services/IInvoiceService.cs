using Sales.Entity.Models;

namespace Sales.Api.Services;

public interface IInvoiceService
{
    Task<InvoiceResult> CreateAsync(SaveInvoiceRequest request, CancellationToken ct);
    Task<InvoiceResult> UpdateAsync(long invoiceId, SaveInvoiceRequest request, CancellationToken ct);
    Task<InvoiceResult> SaveAsync(SaveInvoiceRequest request, long? invoiceId, CancellationToken ct);
    Task<InvoiceView?> GetAsync(long invoiceId, CancellationToken ct);
    Task<List<InvoiceListItem>> ListAsync(DateOnly? from, DateOnly? to, CancellationToken ct);
    Task<List<InvoiceListItem>> ListAsync(CancellationToken ct);
    Task<InvoiceResult> PostAsync(long invoiceId, CancellationToken ct);
    Task<InvoiceResult> VoidAsync(long invoiceId, VoidInvoiceRequest request, CancellationToken ct);
    Task<InvoiceResult> VoidAsync(long invoiceId, CancellationToken ct);
    Task<GlPreviewResult?> PreviewGlAsync(long invoiceId, CancellationToken ct);
    Task<bool> ExistsInOtherOrgAsync(long invoiceId, CancellationToken ct);
}
