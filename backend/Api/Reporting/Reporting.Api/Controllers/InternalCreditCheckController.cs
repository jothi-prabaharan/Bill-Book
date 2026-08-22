using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reporting.Repository;
using Shared.Kernel.Tenancy;

namespace Reporting.Api.Controllers;

[Route("internal/credit")]
[ApiController]
public class InternalCreditCheckController : ControllerBase
{
    private readonly ReportingDbContext _db;
    private readonly TimeProvider _clock;

    public InternalCreditCheckController(ReportingDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    [HttpPost("evaluate")]
    public async Task<IActionResult> EvaluateAsync([FromBody] CreditEvaluateRequest request, CancellationToken ct)
    {
        var contact = await _db.Contacts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ContactId == request.ContactId, ct);

        if (contact == null)
        {
            return NotFound();
        }

        // We know an AR sub-account has ReferenceType = 1 (Contact), Purpose = 0 (Trade).
        var arSubAccountIds = await _db.SubAccounts
            .AsNoTracking()
            .Where(sa => sa.ReferenceType == 1 && sa.ReferenceId == request.ContactId && sa.Purpose == 0)
            .Select(sa => sa.SubAccountId)
            .ToListAsync(ct);

        decimal outstandingBalance = 0;
        if (arSubAccountIds.Count > 0)
        {
            outstandingBalance = await _db.Ledger
                .AsNoTracking()
                .Where(l => l.SubAccountId != null && arSubAccountIds.Contains(l.SubAccountId.Value))
                .SumAsync(l => l.DebitAmountBase - l.CreditAmountBase, ct);
        }

        // 1. Credit Limit check
        if (contact.CreditLimit.HasValue)
        {
            if (outstandingBalance + request.NewOrderAmountBase > contact.CreditLimit.Value)
            {
                return Ok(new CreditEvaluateResponse
                {
                    Allowed = false,
                    Reason = $"Order exceeds credit limit. Current balance: {outstandingBalance:N2}, Limit: {contact.CreditLimit.Value:N2}"
                });
            }
        }

        // 2. Max Outstanding Days check
        // Exact aging requires unravelling allocations which reporting does not map.
        // A simple approximation: if they have a positive balance, we check the oldest invoice date.
        // If the balance is paid, outstandingBalance <= 0, so no old invoices matter.
        if (contact.MaxOutstandingDays.HasValue && outstandingBalance > 0)
        {
            var oldestInvoiceDate = await _db.Ledger
                .AsNoTracking()
                .Where(l => l.SubAccountId != null && arSubAccountIds.Contains(l.SubAccountId.Value) && l.TransactionTypeCode == "INV")
                .MinAsync(l => (DateOnly?)l.LedgerDate, ct);

            if (oldestInvoiceDate.HasValue)
            {
                var today = DateOnly.FromDateTime(_clock.GetUtcNow().Date);
                var daysOld = today.DayNumber - oldestInvoiceDate.Value.DayNumber;

                if (daysOld > contact.MaxOutstandingDays.Value)
                {
                    return Ok(new CreditEvaluateResponse
                    {
                        Allowed = false,
                        Reason = $"Customer has unpaid invoices older than {contact.MaxOutstandingDays.Value} days."
                    });
                }
            }
        }

        return Ok(new CreditEvaluateResponse { Allowed = true });
    }
}

public class CreditEvaluateRequest
{
    public long ContactId { get; set; }
    public decimal NewOrderAmountBase { get; set; }
}

public class CreditEvaluateResponse
{
    public bool Allowed { get; set; }
    public string? Reason { get; set; }
}
