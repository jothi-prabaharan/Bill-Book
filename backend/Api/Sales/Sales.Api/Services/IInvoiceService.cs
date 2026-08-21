using Sales.Entity.Models;

namespace Sales.Api.Services;

public interface IInvoiceService
{
    Task<InvoiceResult> CreateAsync(SaveInvoiceRequest request, CancellationToken ct);

    /// <summary>
    /// Invoices a confirmed sales order, reading its lines server-side.
    /// See <see cref="InvoiceService.CreateFromSalesOrderAsync"/>.
    /// </summary>
    Task<InvoiceResult> CreateFromSalesOrderAsync(
        long salesOrderId, CreateInvoiceFromOrderRequest request, CancellationToken ct);
    Task<InvoiceResult> UpdateAsync(long invoiceId, SaveInvoiceRequest request, CancellationToken ct);
    Task<InvoiceResult> SaveAsync(SaveInvoiceRequest request, long? invoiceId, CancellationToken ct);
    Task<InvoiceView?> GetAsync(long invoiceId, CancellationToken ct);
    Task<List<InvoiceListItem>> ListAsync(DateOnly? from, DateOnly? to, CancellationToken ct);

    /// <summary>
    /// One page of invoices with the total that matched. What the list screen
    /// calls; <see cref="ListAsync(DateOnly?, DateOnly?, CancellationToken)"/>
    /// stays for the mixed transaction list, which pages over five document
    /// types at once.
    /// </summary>
    Task<InvoiceListPage> ListPageAsync(
        int skip,
        int take,
        string? status,
        string? search,
        DateOnly? from,
        DateOnly? to,
        bool overdueOnly,
        CancellationToken ct);
    Task<List<InvoiceListItem>> ListAsync(CancellationToken ct);
    Task<InvoiceResult> PostAsync(long invoiceId, CancellationToken ct);
    Task<InvoiceResult> VoidAsync(long invoiceId, VoidInvoiceRequest request, CancellationToken ct);
    Task<InvoiceResult> VoidAsync(long invoiceId, CancellationToken ct);
    Task<GlPreviewResult?> PreviewGlAsync(long invoiceId, CancellationToken ct);
    Task<bool> ExistsInOtherOrgAsync(long invoiceId, CancellationToken ct);
}
