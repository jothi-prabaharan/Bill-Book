using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reporting.Repository;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Reporting.Api.Controllers;

/// <summary>
/// Sales asks this before it lets an order through, so the caller is a service
/// rather than a person and carries no user token.
///
/// It shipped with neither guard, which made it two faults at once. Reporting
/// sets a FallbackPolicy of RequireAuthenticatedUser, so the route demanded the
/// one credential its only caller does not have — Sales' CreditCheckClient is
/// registered with InternalKeyHandler and sends the shared key alone — while
/// still being reachable by any signed-in user of any tenant, who could read
/// another contact's outstanding balance and credit limit out of the refusal
/// message. [AllowAnonymous] steps off the fallback and [InternalOnly] puts the
/// right credential in its place, which is what every other internal/ route in
/// the product already does.
/// </summary>
[Route("internal/credit")]
[ApiController]
[AllowAnonymous]
[InternalOnly]
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
