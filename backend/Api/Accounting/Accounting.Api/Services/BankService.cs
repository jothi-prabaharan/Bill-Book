using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;
using Shared.Kernel.Ordering;

namespace Accounting.Api.Services;

/// <summary>
/// Banks and the organization's own accounts.
///
/// The part that is not CRUD: creating a bank account creates its GL account in
/// Accounting. That call cannot join this transaction, so the row is inserted
/// first with a null ledger id and linked immediately after. An account that
/// never got linked is visibly incomplete rather than silently broken.
/// </summary>
public sealed class BankService
{
    private readonly AccountingDbContext _db;
    private readonly IAccountingLedger _ledger;
    private readonly INumberGenerator _numbers;
    private readonly TimeProvider _clock;

    public BankService(
        AccountingDbContext db,
        IAccountingLedger ledger,
        INumberGenerator numbers,
        TimeProvider clock)
    {
        _db = db;
        _ledger = ledger;
        _numbers = numbers;
        _clock = clock;
    }

    public async Task<IReadOnlyList<BankListItem>> ListBanksAsync(
        bool includeInactive, CancellationToken ct)
    {
        IQueryable<Bank> query = includeInactive ? _db.Banks : _db.Banks.Where(b => b.IsActive);

        List<Bank> banks = await query
            .OrderBy(b => b.DisplayOrder)
            .ThenBy(b => b.BankName)
            .ToListAsync(ct);

        Dictionary<long, int> counts = await _db.BankAccounts
            .Where(a => a.BankId != null)
            .GroupBy(a => a.BankId!.Value)
            .Select(g => new { BankId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BankId, x => x.Count, ct);

        return banks.Select(b => new BankListItem
        {
            BankId = b.BankId,
            BankCode = b.BankCode,
            BankName = b.BankName,
            DisplayOrder = b.DisplayOrder,
            IsSystem = b.IsSystem,
            IsActive = b.IsActive,
            AccountCount = counts.GetValueOrDefault(b.BankId),
        }).ToList();
    }

    public async Task<SaveBankOutcome> CreateBankAsync(SaveBankRequest request, CancellationToken ct)
    {
        string name = request.BankName.Trim();
        if (await _db.Banks.AnyAsync(b => b.BankName == name, ct))
        {
            return SaveBankOutcome.DuplicateName;
        }

        string code = await ResolveBankCodeAsync(request, ct);
        if (await _db.Banks.AnyAsync(b => b.BankCode == code, ct))
        {
            return SaveBankOutcome.DuplicateCode;
        }

        int highest = await _db.Banks.Select(b => (int?)b.DisplayOrder).MaxAsync(ct) ?? 0;

        _db.Banks.Add(new Bank
        {
            BankCode = code,
            BankName = name,
            DisplayOrder = highest + Reordering.Gap,
            IsSystem = false,
            IsActive = request.IsActive,
        });

        await _db.SaveChangesAsync(ct);
        return SaveBankOutcome.Ok;
    }

    public async Task<SaveBankOutcome> UpdateBankAsync(
        long bankId, SaveBankRequest request, CancellationToken ct)
    {
        Bank? bank = await _db.Banks.FirstOrDefaultAsync(b => b.BankId == bankId, ct);
        if (bank is null)
        {
            return SaveBankOutcome.NotFound;
        }

        string name = request.BankName.Trim();
        if (await _db.Banks.AnyAsync(b => b.BankName == name && b.BankId != bankId, ct))
        {
            return SaveBankOutcome.DuplicateName;
        }

        bank.BankName = name;
        bank.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        return SaveBankOutcome.Ok;
    }

    public async Task<SaveBankOutcome> DeleteBankAsync(long bankId, CancellationToken ct)
    {
        Bank? bank = await _db.Banks.FirstOrDefaultAsync(b => b.BankId == bankId, ct);
        if (bank is null)
        {
            return SaveBankOutcome.NotFound;
        }

        if (await _db.BankAccounts.AnyAsync(a => a.BankId == bankId, ct))
        {
            return SaveBankOutcome.BankInUse;
        }

        _db.Banks.Remove(bank);
        await _db.SaveChangesAsync(ct);
        return SaveBankOutcome.Ok;
    }

    public async Task<SaveBankOutcome> ReorderBanksAsync(ReorderRequest request, CancellationToken ct)
    {
        List<Bank> all = await _db.Banks
            .OrderBy(b => b.DisplayOrder)
            .ThenBy(b => b.BankName)
            .ToListAsync(ct);

        if (!Reordering.Apply(all, request, b => b.BankId, b => b.DisplayOrder,
                (b, order) => b.DisplayOrder = order))
        {
            return SaveBankOutcome.NotFound;
        }

        await _db.SaveChangesAsync(ct);
        return SaveBankOutcome.Ok;
    }

    public async Task<IReadOnlyList<BankAccountListItem>> ListAccountsAsync(
        bool includeInactive, CancellationToken ct)
    {
        IQueryable<BankAccount> query = includeInactive
            ? _db.BankAccounts
            : _db.BankAccounts.Where(a => a.IsActive);

        List<BankAccount> accounts = await query
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.DisplayOrder)
            .ThenBy(a => a.AccountName)
            .ToListAsync(ct);

        Dictionary<long, string> bankNames = await _db.Banks
            .ToDictionaryAsync(b => b.BankId, b => b.BankName, ct);

        return accounts.Select(a => Map(a, bankNames)).ToList();
    }

    /// <summary>
    /// Creates the account and its GL account. The row goes in first with a null
    /// ledger id: an HTTP call cannot join this transaction, so if Accounting
    /// fails the account exists but is plainly unlinked rather than the insert
    /// being rolled back after the ledger account was already made.
    /// </summary>
    public async Task<SaveBankOutcome> CreateAccountAsync(
        SaveBankAccountRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse(request.AccountType, ignoreCase: true, out BankAccountType type))
        {
            return SaveBankOutcome.InvalidValue;
        }

        SaveBankOutcome invalid = Validate(request, type);
        if (invalid != SaveBankOutcome.Ok)
        {
            return invalid;
        }

        string number = request.AccountNumber.Trim();
        bool duplicate = await _db.BankAccounts.AnyAsync(
            a => a.BankId == request.BankId && a.AccountNumber == number, ct);

        if (duplicate)
        {
            return SaveBankOutcome.DuplicateAccountNumber;
        }

        int highest = await _db.BankAccounts.Select(a => (int?)a.DisplayOrder).MaxAsync(ct) ?? 0;

        var account = new BankAccount
        {
            LedgerAccountId = null,
            DisplayOrder = highest + Reordering.Gap,
            IsDefault = !await _db.BankAccounts.AnyAsync(a => a.IsDefault, ct),
        };
        Apply(account, request, type);

        _db.BankAccounts.Add(account);
        await _db.SaveChangesAsync(ct);

        LedgerAccount? ledger = await _ledger.ProvisionAsync(
            account.BankAccountId,
            account.AccountName,
            type.ToString(),
            account.CurrencyCode,
            ct);

        if (ledger is null)
        {
            return SaveBankOutcome.LedgerUnavailable;
        }

        account.LedgerAccountId = ledger.AccountId;
        await _db.SaveChangesAsync(ct);
        return SaveBankOutcome.Ok;
    }

    public async Task<SaveBankOutcome> UpdateAccountAsync(
        long bankAccountId, SaveBankAccountRequest request, CancellationToken ct)
    {
        BankAccount? account = await _db.BankAccounts
            .FirstOrDefaultAsync(a => a.BankAccountId == bankAccountId, ct);

        if (account is null)
        {
            return SaveBankOutcome.NotFound;
        }

        if (!Enum.TryParse(request.AccountType, ignoreCase: true, out BankAccountType type))
        {
            return SaveBankOutcome.InvalidValue;
        }

        SaveBankOutcome invalid = Validate(request, type);
        if (invalid != SaveBankOutcome.Ok)
        {
            return invalid;
        }

        if (account.IsDefault && !request.IsActive)
        {
            return SaveBankOutcome.DefaultAccountLocked;
        }

        Apply(account, request, type);
        await _db.SaveChangesAsync(ct);

        // Banking owns the name, so the change is pushed rather than left to
        // drift from what the chart of accounts shows.
        await _ledger.UpdateAsync(bankAccountId, account.AccountName, account.IsActive, ct);

        return SaveBankOutcome.Ok;
    }

    public async Task<SaveBankOutcome> SetDefaultAccountAsync(
        long bankAccountId, CancellationToken ct)
    {
        BankAccount? account = await _db.BankAccounts
            .FirstOrDefaultAsync(a => a.BankAccountId == bankAccountId, ct);

        if (account is null)
        {
            return SaveBankOutcome.NotFound;
        }

        List<BankAccount> previous = await _db.BankAccounts
            .Where(a => a.IsDefault && a.BankAccountId != bankAccountId)
            .ToListAsync(ct);

        foreach (BankAccount row in previous)
        {
            row.IsDefault = false;
        }

        account.IsDefault = true;
        account.IsActive = true;
        await _db.SaveChangesAsync(ct);
        return SaveBankOutcome.Ok;
    }

    /// <summary>
    /// Deactivates, and deactivates the GL account with it. Never deleted — the
    /// ledger account holds history and documents name it.
    /// </summary>
    public async Task<SaveBankOutcome> DeactivateAccountAsync(
        long bankAccountId, CancellationToken ct)
    {
        BankAccount? account = await _db.BankAccounts
            .FirstOrDefaultAsync(a => a.BankAccountId == bankAccountId, ct);

        if (account is null)
        {
            return SaveBankOutcome.NotFound;
        }

        if (account.IsDefault)
        {
            return SaveBankOutcome.DefaultAccountLocked;
        }

        account.IsActive = false;
        await _db.SaveChangesAsync(ct);

        await _ledger.UpdateAsync(bankAccountId, account.AccountName, false, ct);
        return SaveBankOutcome.Ok;
    }

    /// <summary>
    /// Links an account whose ledger call failed earlier. Idempotent on
    /// Accounting's side, so it either finds the account it already made or
    /// makes it now.
    /// </summary>
    public async Task<SaveBankOutcome> RetryLedgerLinkAsync(
        long bankAccountId, CancellationToken ct)
    {
        BankAccount? account = await _db.BankAccounts
            .FirstOrDefaultAsync(a => a.BankAccountId == bankAccountId, ct);

        if (account is null)
        {
            return SaveBankOutcome.NotFound;
        }

        if (account.LedgerAccountId is not null)
        {
            return SaveBankOutcome.Ok;
        }

        LedgerAccount? ledger = await _ledger.ProvisionAsync(
            account.BankAccountId,
            account.AccountName,
            account.AccountType.ToString(),
            account.CurrencyCode,
            ct);

        if (ledger is null)
        {
            return SaveBankOutcome.LedgerUnavailable;
        }

        account.LedgerAccountId = ledger.AccountId;
        await _db.SaveChangesAsync(ct);
        return SaveBankOutcome.Ok;
    }

    public async Task<SaveBankOutcome> ReorderAccountsAsync(
        ReorderRequest request, CancellationToken ct)
    {
        List<BankAccount> all = await _db.BankAccounts
            .OrderBy(a => a.DisplayOrder)
            .ThenBy(a => a.AccountName)
            .ToListAsync(ct);

        if (!Reordering.Apply(all, request, a => a.BankAccountId, a => a.DisplayOrder,
                (a, order) => a.DisplayOrder = order))
        {
            return SaveBankOutcome.NotFound;
        }

        await _db.SaveChangesAsync(ct);
        return SaveBankOutcome.Ok;
    }

    private static SaveBankOutcome Validate(SaveBankAccountRequest request, BankAccountType type)
    {
        bool needsBank = type is not (BankAccountType.Cash or BankAccountType.Wallet);
        if (needsBank && request.BankId is null)
        {
            return SaveBankOutcome.BankRequired;
        }

        bool canOverdraw = type is BankAccountType.OverDraft
            or BankAccountType.CashCredit
            or BankAccountType.CreditCard;

        return request.OdLimit is not null && !canOverdraw
            ? SaveBankOutcome.OdLimitNotAllowed
            : SaveBankOutcome.Ok;
    }

    private static void Apply(
        BankAccount account, SaveBankAccountRequest request, BankAccountType type)
    {
        account.BankId = type is BankAccountType.Cash or BankAccountType.Wallet
            ? null
            : request.BankId;
        account.AccountName = request.AccountName.Trim();
        account.AccountNumber = request.AccountNumber.Trim();
        account.AccountType = type;
        account.Ifsc = string.IsNullOrWhiteSpace(request.Ifsc)
            ? null
            : request.Ifsc.Trim().ToUpperInvariant();
        account.Micr = request.Micr;
        account.SwiftCode = string.IsNullOrWhiteSpace(request.SwiftCode)
            ? null
            : request.SwiftCode.Trim().ToUpperInvariant();
        account.Iban = request.Iban;
        account.BranchName = request.BranchName;
        account.CurrencyCode = request.CurrencyCode;
        account.OdLimit = request.OdLimit;
        account.IsActive = request.IsActive;
    }

    private async Task<string> ResolveBankCodeAsync(SaveBankRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.BankCode))
        {
            return request.BankCode.Trim().ToUpperInvariant();
        }

        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        NumberAllocation allocation = await _numbers.NextAsync("BANK", today, ct);
        return allocation.Code;
    }

    private static BankAccountListItem Map(BankAccount a, Dictionary<long, string> bankNames) => new()
    {
        BankAccountId = a.BankAccountId,
        BankId = a.BankId,
        BankName = a.BankId is long id ? bankNames.GetValueOrDefault(id) : null,
        LedgerAccountId = a.LedgerAccountId,
        LedgerAccountCode = null,
        AccountName = a.AccountName,
        AccountNumber = a.AccountNumber,
        AccountType = a.AccountType.ToString(),
        Ifsc = a.Ifsc,
        Micr = a.Micr,
        SwiftCode = a.SwiftCode,
        Iban = a.Iban,
        BranchName = a.BranchName,
        CurrencyCode = a.CurrencyCode,
        OdLimit = a.OdLimit,
        IsDefault = a.IsDefault,
        DisplayOrder = a.DisplayOrder,
        IsActive = a.IsActive,
        IsLedgerLinked = a.LedgerAccountId is not null,
    };
}
