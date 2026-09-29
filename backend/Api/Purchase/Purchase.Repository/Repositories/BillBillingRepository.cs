using Purchase.Repository.Clients;
using Microsoft.EntityFrameworkCore;
using Purchase.Entity.Models;
using Purchase.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Persistence;
using Shared.Kernel.Projects;

namespace Purchase.Repository.Repositories;

/// <summary>
/// Re-billing bill lines to a project's client (TK-106). A billable line of a
/// posted bill waits until an invoice claims it at post; the claim is a guarded
/// update whose row count is the answer, so two invoices cannot both charge the
/// same cost on. A void of the invoice releases it.
/// </summary>
public sealed class BillBillingRepository
{
    private readonly PurchaseDbContext _db;

    public BillBillingRepository(PurchaseDbContext db) => _db = db;

    public async Task<List<BillableBillLine>> BillableAsync(long projectId, CancellationToken ct)
    {
        var rows = await (
            from l in _db.BillDetails.AsNoTracking()
            join b in _db.Bills on l.BillId equals b.BillId
            where l.ProjectId == projectId && l.IsBillable && l.BilledInvoiceId == null && b.Status == DocumentStatus.Posted
            orderby b.DocumentDate, l.BillDetailId
            select new { l.BillDetailId, l.BillId, b.DocumentNo, b.DocumentDate, l.Description, l.TaxableAmount, b.ExchangeRate, l.MarkupPercent })
            .ToListAsync(ct);

        return [.. rows.Select(r =>
        {
            decimal amount = Math.Round(r.TaxableAmount * r.ExchangeRate, 2, MidpointRounding.AwayFromZero);
            return new BillableBillLine
            {
                BillDetailId = r.BillDetailId,
                BillId = r.BillId,
                DocumentNo = r.DocumentNo,
                DocumentDate = r.DocumentDate,
                Description = r.Description,
                Amount = amount,
                MarkupPercent = r.MarkupPercent,
                BillableAmount = Math.Round(amount * (1m + ((r.MarkupPercent ?? 0m) / 100m)), 2, MidpointRounding.AwayFromZero),
            };
        })];
    }

    /// <summary>Claims every bill line for the invoice, or none; a line this invoice already holds counts.</summary>
    public async Task<ClaimProjectItemsResponse> ClaimAsync(long invoiceId, IReadOnlyCollection<ProjectItemRef> items, CancellationToken ct)
    {
        List<long> ids = [.. items.Where(i => i.Kind == ProjectItemKind.BillLine).Select(i => i.ItemId).Distinct()];

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        int touched = ids.Count == 0 ? 0 : await _db.BillDetails
            .Where(l => ids.Contains(l.BillDetailId) && l.IsBillable
                && (l.BilledInvoiceId == null || l.BilledInvoiceId == invoiceId)
                && _db.Bills.Any(b => b.BillId == l.BillId && b.Status == DocumentStatus.Posted))
            .ExecuteUpdateAsync(u => u.SetProperty(l => l.BilledInvoiceId, invoiceId), ct);

        if (touched != ids.Count)
        {
            await tx.RollbackAsync(ct);
            return new ClaimProjectItemsResponse
            {
                Claimed = false,
                Requested = ids.Count,
                Touched = touched,
                Detail = ProjectBillingMessages.AlreadyBilled,
            };
        }

        await tx.CommitAsync(ct);
        return new ClaimProjectItemsResponse { Claimed = true, Requested = ids.Count, Touched = touched };
    }

    public Task<int> ReleaseAsync(long invoiceId, CancellationToken ct) =>
        _db.BillDetails
            .Where(l => l.BilledInvoiceId == invoiceId)
            .ExecuteUpdateAsync(u => u.SetProperty(l => l.BilledInvoiceId, (long?)null), ct);
}
