using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Accounting.Repository.Clients;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Repository.Repositories;

public sealed class AccountRepository
{
    private readonly AccountingDbContext _db;
    private readonly IBranchCurrencies _currencies;

    public AccountRepository(AccountingDbContext db, IBranchCurrencies currencies)
    {
        _db = db;
        _currencies = currencies;
    }

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

        HashSet<long> parentIds = [.. rows.Where(r => r.ParentAccountId != null).Select(r => r.ParentAccountId!.Value)];
        Dictionary<long, string> names = rows.ToDictionary(r => r.AccountId, r => r.AccountName);

        List<long> ledgerIds = await _db.JournalLedger.Select(l => l.AccountId).Distinct().ToListAsync(ct);
        List<long> detailIds = await _db.JournalDetails.Select(d => d.AccountId).Distinct().ToListAsync(ct);
        List<long> obIds = await _db.OpeningBalanceLines.Where(l => l.AccountId != null).Select(l => l.AccountId!.Value).Distinct().ToListAsync(ct);
        HashSet<long> txAccountIds = [.. ledgerIds.Concat(detailIds).Concat(obIds)];

        var result = new List<AccountListItem>(rows.Count);
        var byType = rows.GroupBy(r => r.AccountTypeId).OrderBy(g => g.Key);

        foreach (var group in byType)
        {
            var byParent = group.Where(r => r.ParentAccountId != null).GroupBy(r => r.ParentAccountId!.Value).ToDictionary(g => g.Key, g => g.OrderBy(a => a.AccountCode).ToList());
            var visited = new HashSet<long>();

            void AddBranch(Account a, int depth)
            {
                visited.Add(a.AccountId);
                result.Add(Map(a, depth, parentIds.Contains(a.AccountId),
                    a.ParentAccountId != null && names.TryGetValue(a.ParentAccountId.Value, out string? pName) ? pName : null,
                    txAccountIds.Contains(a.AccountId) || (a.IsUsed && !parentIds.Contains(a.AccountId))));

                if (byParent.TryGetValue(a.AccountId, out var children))
                {
                    foreach (Account child in children)
                    {
                        AddBranch(child, depth + 1);
                    }
                }
            }

            var roots = group.Where(a => a.ParentAccountId == null || !group.Any(p => p.AccountId == a.ParentAccountId)).OrderBy(a => a.AccountCode);
            foreach (Account root in roots)
            {
                if (!visited.Contains(root.AccountId))
                {
                    AddBranch(root, 0);
                }
            }

            foreach (Account remaining in group.Where(a => !visited.Contains(a.AccountId)).OrderBy(a => a.AccountCode))
            {
                AddBranch(remaining, 0);
            }
        }

        return result;
    }

    public async Task<AccountListItem?> GetAsync(long accountId, CancellationToken ct)
    {
        Account? account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId, ct);
        if (account is null) return null;

        bool hasChildren = await _db.Accounts.AnyAsync(a => a.ParentAccountId == accountId, ct);
        string? parentName = account.ParentAccountId != null
            ? await _db.Accounts.Where(a => a.AccountId == account.ParentAccountId).Select(a => a.AccountName).FirstOrDefaultAsync(ct)
            : null;

        bool hasTx = await _db.JournalLedger.AnyAsync(l => l.AccountId == accountId, ct)
            || await _db.JournalDetails.AnyAsync(d => d.AccountId == accountId, ct)
            || await _db.OpeningBalanceLines.AnyAsync(l => l.AccountId == accountId, ct)
            || (account.IsUsed && !hasChildren);

        return Map(account, 0, hasChildren, parentName, hasTx);
    }

    /// <summary>
    /// Ok when the branch may use this currency; otherwise the reason. A list that cannot be read refuses
    /// rather than letting an unchecked currency through.
    /// </summary>
    private async Task<SaveAccountOutcome> CurrencyOutcomeAsync(string code, CancellationToken ct)
    {
        IReadOnlyList<string>? active = await _currencies.ActiveCodesAsync(ct);
        if (active is null)
        {
            return SaveAccountOutcome.CurrencyUnavailable;
        }

        return active.Contains(code, StringComparer.Ordinal) ? SaveAccountOutcome.Ok : SaveAccountOutcome.CurrencyNotInUse;
    }

    public async Task<SaveAccountResult> CreateAsync(SaveAccountRequest request, CancellationToken ct)
    {
        bool codeTaken = await _db.Accounts.AnyAsync(a => a.AccountCode == request.AccountCode, ct);
        if (codeTaken)
        {
            return new SaveAccountResult(SaveAccountOutcome.CodeInUse, null);
        }

        // Bank accounts are set up under Banking, which provisions their ledger account and sets this flag itself.
        if (request.IsBank)
        {
            return new SaveAccountResult(SaveAccountOutcome.BankFlagNotAllowed, null);
        }

        if (request.CurrencyCode is not null && await CurrencyOutcomeAsync(request.CurrencyCode, ct) is { } currencyOutcome and not SaveAccountOutcome.Ok)
        {
            return new SaveAccountResult(currencyOutcome, null);
        }

        if (request.ParentAccountId is long parentId)
        {
            SaveAccountOutcome parentOutcome = await ValidateParentAsync(null, parentId, request.AccountTypeId, ct);
            if (parentOutcome != SaveAccountOutcome.Ok)
            {
                return new SaveAccountResult(parentOutcome, null);
            }
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
            // Set by Banking when a bank account is made, so it cannot be turned on or off here.
            if (request.IsBank != account.IsBank)
            {
                return new SaveAccountResult(SaveAccountOutcome.BankFlagNotAllowed, null);
            }

            if (request.CurrencyCode != account.CurrencyCode && request.CurrencyCode is not null
                && await CurrencyOutcomeAsync(request.CurrencyCode, ct) is { } changedOutcome and not SaveAccountOutcome.Ok)
            {
                return new SaveAccountResult(changedOutcome, null);
            }

            account.CurrencyCode = request.CurrencyCode;
        }
        else if (ChangesLockedFields(account, request))
        {
            return new SaveAccountResult(SaveAccountOutcome.ConfigLocked, null);
        }

        if (request.ParentAccountId is long parentId && parentId != account.ParentAccountId)
        {
            SaveAccountOutcome parentOutcome = await ValidateParentAsync(accountId, parentId, account.AccountTypeId, ct);
            if (parentOutcome != SaveAccountOutcome.Ok)
            {
                return new SaveAccountResult(parentOutcome, null);
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

    /// <summary>
    /// Writes the default chart of accounts, adding only the control accounts an
    /// organization does not already have.
    ///
    /// Deliberately per-account rather than all-or-nothing. The seed list grows:
    /// the three bank parent groups were added after organizations already
    /// existed, and an all-or-nothing guard would skip them forever, leaving
    /// those organizations unable to create a bank account. Re-running this is
    /// how such a gap is closed, and the unique index on
    /// (OrgId, AccountSystemName) makes running it twice harmless.
    /// </summary>
    public async Task<int> SeedForOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        List<string> existing = await _db.Accounts
            .IgnoreQueryFilters()
            .Where(a => a.OrgId == orgId && a.AccountSystemName != null)
            .Select(a => a.AccountSystemName!)
            .ToListAsync(ct);

        HashSet<string> present = [.. existing];

        // AccountCode is unique per organization. A customer-created account on
        // 1400 would otherwise fail the insert for every account in the batch,
        // not just its own — and this runs during provisioning, where a throw
        // fails the whole branch.
        List<string> codes = await _db.Accounts
            .IgnoreQueryFilters()
            .Where(a => a.OrgId == orgId)
            .Select(a => a.AccountCode)
            .ToListAsync(ct);

        HashSet<string> taken = [.. codes];

        List<Account> missing = Repository.SeedData.ChartOfAccountsSeed.Build(orgId)
            .Where(a => a.AccountSystemName is not null
                && !present.Contains(a.AccountSystemName)
                && !taken.Contains(a.AccountCode))
            .ToList();

        if (missing.Count == 0)
        {
            return 0;
        }

        _db.Accounts.AddRange(missing);
        await _db.SaveChangesAsync(ct);
        return missing.Count;
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
        || account.IsBank != request.IsBank
        // Repricing an account that already holds postings would restate every
        // one of them, so the currency is frozen with the rest of the config.
        || account.CurrencyCode != request.CurrencyCode;

    private async Task<SaveAccountOutcome> ValidateParentAsync(
        long? accountId, long parentId, int accountTypeId, CancellationToken ct)
    {
        Account? parent = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == parentId, ct);
        if (parent is null || parent.AccountTypeId != accountTypeId)
        {
            return SaveAccountOutcome.InvalidParent;
        }

        // Rule 3: An account that has been used in transactions cannot be assigned as a parent account.
        bool hasTransactions = await _db.JournalLedger.AnyAsync(l => l.AccountId == parentId, ct)
            || await _db.JournalDetails.AnyAsync(d => d.AccountId == parentId, ct)
            || await _db.OpeningBalanceLines.AnyAsync(l => l.AccountId == parentId, ct);

        if (!hasTransactions && parent.IsUsed)
        {
            bool hasChildren = await _db.Accounts.AnyAsync(a => a.ParentAccountId == parentId, ct);
            if (!hasChildren)
            {
                hasTransactions = true;
            }
        }

        if (hasTransactions)
        {
            return SaveAccountOutcome.ParentHasTransactions;
        }

        if (accountId.HasValue && (accountId.Value == parentId || await WouldCycleAsync(accountId.Value, parentId, ct)))
        {
            return SaveAccountOutcome.InvalidParent;
        }

        return SaveAccountOutcome.Ok;
    }

    /// <summary>
    /// Walks up from the proposed parent looking for this account. Without it,
    /// A→B followed by B→A makes a ring that no report could ever total, and
    /// checking self-parenting alone does not catch it.
    /// </summary>
    private async Task<bool> WouldCycleAsync(long accountId, long parentId, CancellationToken ct)
    {
        long? cursor = parentId;
        var seen = new HashSet<long>();

        while (cursor is long current)
        {
            if (current == accountId)
            {
                return true;
            }

            // Defensive: a ring that predates this check would otherwise spin here.
            if (!seen.Add(current))
            {
                return true;
            }

            cursor = await _db.Accounts
                .Where(a => a.AccountId == current)
                .Select(a => a.ParentAccountId)
                .FirstOrDefaultAsync(ct);
        }

        return false;
    }

    private static AccountListItem Map(
        Account a, int depth = 0, bool hasChildren = false, string? parentName = null, bool hasTransactions = false) => new()
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
        HasChildren = hasChildren,
        ParentAccountName = parentName,
        Depth = depth,
        HasTransactions = hasTransactions,
    };
}

public enum SaveAccountOutcome
{
    Ok = 1,
    NotFound = 2,
    CodeInUse = 3,
    ConfigLocked = 4,
    InvalidParent = 5,
    ParentHasTransactions = 6,
    CurrencyNotInUse = 7,
    CurrencyUnavailable = 8,
    BankFlagNotAllowed = 9,
}

public sealed record SaveAccountResult(SaveAccountOutcome Outcome, long? AccountId);

public enum DeleteAccountOutcome
{
    Ok = 1,
    NotFound = 2,
    SystemAccount = 3,
    HasChildren = 4,
}
