using Accounting.Entity.Enums;
using Accounting.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Internal;
using Shared.Kernel.Ledgers;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Controllers;

/// <summary>
/// Which ledger and bank accounts another service may name (School fees,
/// TK-64): a fee head's income account, the bank a fee receipt lands in. An
/// account id is a per-branch number in this database, so the caller lists and
/// checks them here rather than guessing. The branch comes in the body and the
/// query filter keeps the answer inside it.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/accounts")]
public sealed class InternalAccountsController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalAccountsController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("lookup")]
    public async Task<IActionResult> Lookup([FromBody] AccountLookupRequest request, CancellationToken ct)
    {
        if (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId) != InternalTenantOutcome.Applied)
        {
            return BadRequest();
        }

        var db = _services.GetRequiredService<AccountingDbContext>();
        List<int> types = [.. request.AccountTypeIds.Distinct()];
        List<long> ids = [.. request.AccountIds.Distinct().Take(500)];

        List<AccountSummary> found = await db.Accounts.AsNoTracking()
            .Where(a => ids.Contains(a.AccountId) || (types.Contains(a.AccountTypeId) && a.IsActive && !a.IsLock))
            .OrderBy(a => a.AccountCode)
            .Select(a => new AccountSummary
            {
                AccountId = a.AccountId,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                AccountSystemName = a.AccountSystemName,
                AccountTypeId = a.AccountTypeId,
                IsActive = a.IsActive,
                IsContra = a.IsContra,
                IsLock = a.IsLock,
            })
            .ToListAsync(ct);

        return Ok(found);
    }

    [HttpPost("bank-accounts")]
    public async Task<IActionResult> BankAccounts([FromBody] BankAccountLookupRequest request, CancellationToken ct)
    {
        if (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId) != InternalTenantOutcome.Applied)
        {
            return BadRequest();
        }

        var db = _services.GetRequiredService<AccountingDbContext>();
        var rows = await db.BankAccounts.AsNoTracking()
            .Where(b => b.IsActive && b.LedgerAccountId != null)
            .OrderBy(b => b.AccountName)
            .Select(b => new { b.BankAccountId, b.AccountName, b.AccountNumber, b.AccountType })
            .ToListAsync(ct);

        return Ok(rows.Select(b => new BankAccountSummary
        {
            BankAccountId = b.BankAccountId,
            AccountName = b.AccountName,
            MaskedNumber = b.AccountNumber.Length <= 4 ? b.AccountNumber : $"••••{b.AccountNumber[^4..]}",
            IsCash = b.AccountType == BankAccountType.Cash,
        }));
    }
}
