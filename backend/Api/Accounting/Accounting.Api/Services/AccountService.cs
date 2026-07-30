using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Api.Services;

public sealed class AccountService
{
    private readonly AccountingDbContext _db;

    public AccountService(AccountingDbContext db) => _db = db;

    public async Task<IReadOnlyList<AccountListItem>> ListAsync(
        bool includeInactive, CancellationToken ct)
    {
        IQueryable<Account> query = _db.Accounts;
        if (!includeInactive)
        {
            query = query.Where(a => a.IsActive);
        }

        List<Account> rows = await query
            .OrderBy(a => a.AccountTypeId)
            .ThenBy(a => a.AccountCode)
            .ToListAsync(ct);

        return rows.Select(Map).ToList();
    }

    public async Task<AccountListItem?> GetAsync(long accountId, CancellationToken ct)
    {
        Account? account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId, ct);
        return account is null ? null : Map(account);
    }

    public async Task<SaveAccountResult> CreateAsync(SaveAccountRequest request, CancellationToken ct)
    {
        bool codeTaken = await _db.Accounts.AnyAsync(a => a.AccountCode == request.AccountCode, ct);
        if (codeTaken)
        {
            return new SaveAccountResult(SaveAccountOutcome.CodeInUse, null);
        }

        if (request.ParentAccountId is long parentId && !await ParentIsValidAsync(parentId, request.AccountTypeId, ct))
        {
            return new SaveAccountResult(SaveAccountOutcome.InvalidParent, null);
        }

        var account = new Account
        {
            AccountTypeId = request.AccountTypeId,
            AccountCode = request.AccountCode,
            AccountName = request.AccountName,
            AccountSystemName = null,
            ParentAccountId = request.ParentAccountId,
            CurrencyCode = request.CurrencyCode,
            IsContra = request.IsContra,
            IsActive = request.IsActive,
            IsLock = request.IsLock,
            IsSales = request.IsSales,
            IsPurchase = request.IsPurchase,
            IsPayment = request.IsPayment,
            IsBank = request.IsBank,
            IsSystemDefault = false,
            IsUsed = false,
            IsJE = false,
        };

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync(ct);
        return new SaveAccountResult(SaveAccountOutcome.Ok, account.AccountId);
    }

    /// <summary>
    /// Updates an account, honouring the config lock: once used (or if a system
    /// account), type, contra, code and every usage flag are frozen. Only the
    /// display name, active state, posting lock and parent stay editable.
    /// </summary>
    public async Task<SaveAccountResult> UpdateAsync(
        long accountId, SaveAccountRequest request, CancellationToken ct)
    {
        Account? account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId, ct);
        if (account is null)
        {
            return new SaveAccountResult(SaveAccountOutcome.NotFound, null);
        }

        bool locked = IsConfigLocked(account);

        if (!locked)
        {
            if (account.AccountCode != request.AccountCode)
            {
                bool codeTaken = await _db.Accounts.AnyAsync(
                    a => a.AccountCode == request.AccountCode && a.AccountId != accountId, ct);
                if (codeTaken)
                {
                    return new SaveAccountResult(SaveAccountOutcome.CodeInUse, null);
                }

                account.AccountCode = request.AccountCode;
            }

            account.AccountTypeId = request.AccountTypeId;
            account.IsContra = request.IsContra;
            account.IsSales = request.IsSales;
            account.IsPurchase = request.IsPurchase;
            account.IsPayment = request.IsPayment;
            account.IsBank = request.IsBank;
            account.CurrencyCode = request.CurrencyCode;
        }
        else if (ChangesLockedFields(account, request))
        {
            return new SaveAccountResult(SaveAccountOutcome.ConfigLocked, null);
        }

        if (request.ParentAccountId is long parentId && parentId != account.ParentAccountId)
        {
            if (parentId == accountId || !await ParentIsValidAsync(parentId, account.AccountTypeId, ct))
            {
                return new SaveAccountResult(SaveAccountOutcome.InvalidParent, null);
            }
        }

        // Always editable, even on a locked or system account.
        account.AccountName = request.AccountName;
        account.IsActive = request.IsActive;
        account.IsLock = request.IsLock;
        account.ParentAccountId = request.ParentAccountId;

        await _db.SaveChangesAsync(ct);
        return new SaveAccountResult(SaveAccountOutcome.Ok, accountId);
    }

    /// <summary>Deactivates rather than deletes. A used or system account is never removed.</summary>
    public async Task<DeleteAccountOutcome> DeactivateAsync(long accountId, CancellationToken ct)
    {
        Account? account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId, ct);
        if (account is null)
        {
            return DeleteAccountOutcome.NotFound;
        }

        if (account.IsSystemDefault)
        {
            return DeleteAccountOutcome.SystemAccount;
        }

        bool hasChildren = await _db.Accounts.AnyAsync(a => a.ParentAccountId == accountId, ct);
        if (hasChildren)
        {
            return DeleteAccountOutcome.HasChildren;
        }

        account.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return DeleteAccountOutcome.Ok;
    }

    /// <summary>Writes the default chart of accounts for a newly created organization.</summary>
    public async Task<int> SeedForOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        if (await _db.Accounts.IgnoreQueryFilters().AnyAsync(a => a.OrgId == orgId, ct))
        {
            return 0;
        }

        IReadOnlyList<Account> seed = Repository.SeedData.ChartOfAccountsSeed.Build(orgId);
        _db.Accounts.AddRange(seed);
        await _db.SaveChangesAsync(ct);
        return seed.Count;
    }

    /// <summary>
    /// The config lock: an account whose nature can no longer change. Once used,
    /// its postings would silently reclassify if its type moved; system accounts
    /// are locked from creation.
    /// </summary>
    private static bool IsConfigLocked(Account account) => account.IsUsed || account.IsSystemDefault;

    private static bool ChangesLockedFields(Account account, SaveAccountRequest request) =>
        account.AccountTypeId != request.AccountTypeId
        || account.AccountCode != request.AccountCode
        || account.IsContra != request.IsContra
        || account.IsSales != request.IsSales
        || account.IsPurchase != request.IsPurchase
        || account.IsPayment != request.IsPayment
        || account.IsBank != request.IsBank;

    private async Task<bool> ParentIsValidAsync(long parentId, int accountTypeId, CancellationToken ct)
    {
        Account? parent = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == parentId, ct);

        // A child must sit under the same account type, or the tree would put an
        // expense under an asset and reports would double-count.
        return parent is not null && parent.AccountTypeId == accountTypeId;
    }

    private static AccountListItem Map(Account a) => new()
    {
        AccountId = a.AccountId,
        AccountTypeId = a.AccountTypeId,
        AccountCode = a.AccountCode,
        AccountName = a.AccountName,
        ParentAccountId = a.ParentAccountId,
        CurrencyCode = a.CurrencyCode,
        IsContra = a.IsContra,
        IsSystemDefault = a.IsSystemDefault,
        IsActive = a.IsActive,
        IsUsed = a.IsUsed,
        IsLock = a.IsLock,
        IsJE = a.IsJE,
        IsSales = a.IsSales,
        IsPurchase = a.IsPurchase,
        IsPayment = a.IsPayment,
        IsBank = a.IsBank,
        IsConfigLocked = IsConfigLocked(a),
    };
}

public enum SaveAccountOutcome
{
    Ok = 1,
    NotFound = 2,
    CodeInUse = 3,
    ConfigLocked = 4,
    InvalidParent = 5,
}

public sealed record SaveAccountResult(SaveAccountOutcome Outcome, long? AccountId);

public enum DeleteAccountOutcome
{
    Ok = 1,
    NotFound = 2,
    SystemAccount = 3,
    HasChildren = 4,
}
