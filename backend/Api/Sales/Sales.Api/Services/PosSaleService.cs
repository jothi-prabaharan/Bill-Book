using Microsoft.EntityFrameworkCore;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Interfaces;

namespace Sales.Api.Services;

/// <summary>
/// A till sale in one call (T7.1, TK-39): make the invoice, record how it was
/// paid, and post it.
///
/// <b>A POS sale is an ordinary invoice.</b> <see cref="IInvoiceService"/> makes
/// and posts it, so the tax, the numbering (the <c>POS</c> series), the stock
/// issue and the ledger are the invoice's own. What the till adds is the
/// tenders: they replace the customer's receivable with the accounts the money
/// went into, so a paid sale leaves nothing owing.
///
/// <b>The stock decrement is the invoice's, and it is synchronous.</b> Posting
/// calls Inventory's guarded issue inside this request, so two tills selling the
/// last unit cannot both succeed: the second is refused with the item named, and
/// because every write endpoint runs in one transaction that commits only on
/// success, its draft invoice is rolled back with it.
///
/// <b>Change is given only from cash.</b> Card and UPI together may pay at most
/// the total; cash covers the rest and anything over it is change.
/// </summary>
public sealed class PosSaleService
{
    private readonly IInvoiceService _invoices;
    private readonly SalesDbContext _db;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public PosSaleService(IInvoiceService invoices, SalesDbContext db, ICurrentUser user, TimeProvider clock)
    {
        _invoices = invoices;
        _db = db;
        _user = user;
        _clock = clock;
    }

    public async Task<PosSaleResult> SellAsync(PosSaleRequest request, CancellationToken ct)
    {
        if (request.Tenders.Count == 0)
        {
            return new PosSaleResult(PosSaleOutcome.TenderShort, Detail: "A sale needs at least one tender.");
        }

        DateOnly date = request.DocumentDate ?? DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);

        InvoiceResult created = await _invoices.CreateAsync(new SaveInvoiceRequest
        {
            DocumentDate = date,
            DueDate = date,
            ContactId = request.ContactId,
            TillId = request.TillId,
            CashierUserId = _user.UserId,
            ContactGstin = request.ContactGstin,
            PlaceOfSupplyStateCode = request.PlaceOfSupplyStateCode,
            Notes = request.Notes,
            Lines = request.Lines,
        }, ct);

        if (created.Outcome != InvoiceOutcome.Ok)
        {
            return Refused(created);
        }

        Invoice invoice = await _db.Invoices.FirstAsync(i => i.InvoiceId == created.InvoiceId, ct);

        (PosSaleOutcome check, decimal change, string? detail) = CheckTenders(invoice.TotalAmount, request.Tenders);
        if (check != PosSaleOutcome.Ok)
        {
            return new PosSaleResult(check, invoice.InvoiceId, invoice.DocumentNo, invoice.TotalAmount, Detail: detail);
        }

        invoice.TenderedAmount = request.Tenders.Sum(t => t.Amount);
        invoice.ChangeAmount = change;
        invoice.PaymentMode = request.Tenders.Select(t => t.Mode).Distinct().Count() == 1
            ? request.Tenders[0].Mode.ToString()
            : "Split";

        _db.InvoiceTenders.AddRange(request.Tenders.Select(t => new InvoiceTender
        {
            InvoiceId = invoice.InvoiceId,
            Mode = t.Mode,
            Amount = t.Amount,
            BankAccountId = t.BankAccountId,
            Reference = string.IsNullOrWhiteSpace(t.Reference) ? null : t.Reference.Trim(),
        }));
        await _db.SaveChangesAsync(ct);

        InvoiceResult posted = await _invoices.PostAsync(invoice.InvoiceId, ct);
        if (posted.Outcome != InvoiceOutcome.Ok)
        {
            return Refused(posted);
        }

        return new PosSaleResult(
            PosSaleOutcome.Ok, invoice.InvoiceId, invoice.DocumentNo, invoice.TotalAmount, change);
    }

    /// <summary>
    /// Whether the tenders pay the total, and the change: card and UPI at most
    /// the total, everything together at least the total, the excess handed
    /// back from cash.
    /// </summary>
    public static (PosSaleOutcome Outcome, decimal Change, string? Detail) CheckTenders(
        decimal total, IReadOnlyList<PosTenderRequest> tenders)
    {
        if (tenders.Any(t => t.Amount <= 0m))
        {
            return (PosSaleOutcome.TenderShort, 0m, "Every tender must be more than zero.");
        }

        decimal nonCash = tenders.Where(t => t.Mode != PosTenderMode.Cash).Sum(t => t.Amount);
        if (nonCash > total)
        {
            return (PosSaleOutcome.TenderOverpaid, 0m,
                $"Card and UPI can pay at most the total of {total:0.00}. Only cash gives change.");
        }

        decimal paid = tenders.Sum(t => t.Amount);
        if (paid < total)
        {
            return (PosSaleOutcome.TenderShort, 0m,
                $"The tenders cover {paid:0.00} of {total:0.00}. {total - paid:0.00} is still to pay.");
        }

        return (PosSaleOutcome.Ok, paid - total, null);
    }

    private static PosSaleResult Refused(InvoiceResult result) =>
        new(PosSaleOutcome.InvoiceRefused, result.InvoiceId, Detail: result.Detail, Refusal: result.Outcome);
}
