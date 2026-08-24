using Microsoft.EntityFrameworkCore;
using Sales.Repository;
using Shared.Kernel.Internal;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Documents;

namespace Sales.Api.Services;

public class AgedReceivableRow
{
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public string CustomerCode { get; set; } = null!;
    public decimal Current { get; set; }
    public decimal Days1To30 { get; set; }
    public decimal Days31To60 { get; set; }
    public decimal Days61To90 { get; set; }
    public decimal Days90Plus { get; set; }
    public decimal Total => Current + Days1To30 + Days31To60 + Days61To90 + Days90Plus;
}

public class CustomerOutstandingInvoiceView
{
    public long InvoiceId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
}

public class OutstandingService
{
    private readonly SalesDbContext _db;
    private readonly ILedgerClient _ledger;
    private readonly IContactNameLookup _contactNames;

    public OutstandingService(SalesDbContext db, ILedgerClient ledger, IContactNameLookup contactNames)
    {
        _db = db;
        _ledger = ledger;
        _contactNames = contactNames;
    }

    public async Task<List<AgedReceivableRow>> GetAgingSummaryAsync(CancellationToken ct)
    {
        // AR ledger type is 1
        var balances = await _ledger.GetAllOutstandingBalancesAsync(1, ct);
        var invBalances = balances.Where(b => b.TransactionTypeCode == "INV").ToList();

        if (invBalances.Count == 0)
        {
            return [];
        }

        var invoiceIds = invBalances.Select(b => b.TransactionId).ToList();

        var invoices = await _db.Invoices
            .Where(i => invoiceIds.Contains(i.InvoiceId))
            .Select(i => new
            {
                i.InvoiceId,
                i.DueDate
            })
            .ToDictionaryAsync(i => i.InvoiceId, ct);

        // Resolve names
        var contactIds = invBalances.Select(b => b.ContactId).Distinct().ToList();
        var contacts = await _contactNames.ResolveAsync(contactIds, ct);

        // Calculate buckets
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rows = new Dictionary<long, AgedReceivableRow>();

        foreach (var b in invBalances)
        {
            if (!invoices.TryGetValue(b.TransactionId, out var inv))
                continue;

            var dueDate = inv.DueDate ?? b.DocumentDate;
            var daysOverdue = today.DayNumber - dueDate.DayNumber;
            
            var contactId = b.ContactId;
            
            if (!rows.TryGetValue(contactId, out var row))
            {
                var named = contacts.TryGetValue(contactId, out var n) ? n : null;
                row = new AgedReceivableRow
                {
                    CustomerId = contactId,
                    CustomerName = named?.Name ?? $"Customer {contactId}",
                    CustomerCode = named?.Code ?? ""
                };
                rows[contactId] = row;
            }

            if (daysOverdue <= 0)
                row.Current += b.OutstandingAmount;
            else if (daysOverdue <= 30)
                row.Days1To30 += b.OutstandingAmount;
            else if (daysOverdue <= 60)
                row.Days31To60 += b.OutstandingAmount;
            else if (daysOverdue <= 90)
                row.Days61To90 += b.OutstandingAmount;
            else
                row.Days90Plus += b.OutstandingAmount;
        }

        return rows.Values.OrderBy(r => r.CustomerName).ToList();
    }

    public async Task<List<CustomerOutstandingInvoiceView>> GetUnpaidInvoicesAsync(long customerId, CancellationToken ct)
    {
        var balances = await _ledger.GetOutstandingBalancesAsync(customerId, 1, ct);
        var invBalances = balances.Where(b => b.TransactionTypeCode == "INV").ToList();

        if (invBalances.Count == 0)
        {
            return [];
        }

        var invoiceIds = invBalances.Select(b => b.TransactionId).ToList();

        var invoices = await _db.Invoices
            .Where(i => invoiceIds.Contains(i.InvoiceId))
            .Select(i => new { i.InvoiceId, i.DocumentNo, i.DueDate })
            .ToDictionaryAsync(i => i.InvoiceId, ct);

        var result = new List<CustomerOutstandingInvoiceView>();

        foreach (var b in invBalances)
        {
            if (!invoices.TryGetValue(b.TransactionId, out var inv))
                continue;

            result.Add(new CustomerOutstandingInvoiceView
            {
                InvoiceId = b.TransactionId,
                DocumentNo = inv.DocumentNo,
                DocumentDate = b.DocumentDate,
                DueDate = inv.DueDate ?? b.DocumentDate,
                TotalAmount = b.TotalAmount,
                PaidAmount = b.PaidAmount,
                OutstandingAmount = b.OutstandingAmount
            });
        }

        return result.OrderBy(x => x.DueDate).ToList();
    }
}
