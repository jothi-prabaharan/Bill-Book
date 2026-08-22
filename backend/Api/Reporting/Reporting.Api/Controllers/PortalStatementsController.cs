using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reporting.Repository;
using System.Security.Claims;

namespace Reporting.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/portal/statements")]
public sealed class PortalStatementsController : ControllerBase
{
    private readonly ReportingDbContext _db;

    public PortalStatementsController(ReportingDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetContactStatement(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken ct)
    {
        // Require portal access claim
        if (User.FindFirst("portal_access")?.Value != "true")
        {
            return Forbid();
        }

        // Extract contact ID from token
        string? contactIdStr = User.FindFirst("contact_id")?.Value;
        if (string.IsNullOrEmpty(contactIdStr) || !long.TryParse(contactIdStr, out long contactId))
        {
            return Forbid();
        }

        // Query the ledger for this contact's sub-accounts
        // SubAccounts are linked to the ledger rows via SubAccountId.
        // We need to fetch AR and AP sub-accounts for this contact.

        var subAccounts = await _db.SubAccounts
            .Where(sa => sa.ReferenceType == 1 && sa.ReferenceId == contactId)
            .Select(sa => sa.SubAccountId)
            .ToListAsync(ct);

        if (!subAccounts.Any())
        {
            return Ok(new { OpeningBalance = 0, Transactions = new List<object>(), ClosingBalance = 0 });
        }

        var query = _db.Ledger
            .Where(l => l.SubAccountId != null && subAccounts.Contains(l.SubAccountId.Value));

        if (fromDate.HasValue)
        {
            query = query.Where(l => l.LedgerDate >= fromDate.Value);
        }
        if (toDate.HasValue)
        {
            query = query.Where(l => l.LedgerDate <= toDate.Value);
        }

        var transactions = await query
            .OrderBy(l => l.LedgerDate)
            .ThenBy(l => l.LedgerId)
            .Select(l => new
            {
                l.LedgerDate,
                TransactionNo = l.TransactionTypeCode + "-" + l.TransactionId, // Approximation since we don't have a string TransactionNo on leg
                Reference = l.TransactionDesc,
                l.DebitAmountBase,
                l.CreditAmountBase,
                Description = l.TransactionDesc
            })
            .ToListAsync(ct);

        // Opening balance calculation (everything before fromDate)
        decimal openingBalance = 0;
        if (fromDate.HasValue)
        {
            var priorQuery = _db.Ledger
                .Where(l => l.SubAccountId != null && subAccounts.Contains(l.SubAccountId.Value) && l.LedgerDate < fromDate.Value);
            
            var priorDebits = await priorQuery.SumAsync(l => l.DebitAmountBase, ct);
            var priorCredits = await priorQuery.SumAsync(l => l.CreditAmountBase, ct);
            
            // Typically AR is debit normal, AP is credit normal. 
            // A positive balance here means they owe us (Net Debit). 
            openingBalance = priorDebits - priorCredits;
        }

        decimal runningBalance = openingBalance;
        var txnList = new List<object>();

        foreach (var txn in transactions)
        {
            runningBalance += (txn.DebitAmountBase - txn.CreditAmountBase);
            txnList.Add(new
            {
                txn.LedgerDate,
                txn.TransactionNo,
                txn.Reference,
                txn.Description,
                Debit = txn.DebitAmountBase,
                Credit = txn.CreditAmountBase,
                Balance = runningBalance
            });
        }

        return Ok(new
        {
            OpeningBalance = openingBalance,
            Transactions = txnList,
            ClosingBalance = runningBalance
        });
    }
}
