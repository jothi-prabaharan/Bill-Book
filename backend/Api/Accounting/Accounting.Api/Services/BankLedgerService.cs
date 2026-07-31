using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Api.Services;

/// <summary>
/// Creates and maintains the GL account behind a bank account. Banking owns the
/// account number and IFSC; only Accounting writes ledger rows, so the account
/// itself is made here.
///
/// A bank account gets a full <c>acc.Accounts</c> row rather than a sub-account,
/// because bank and equity journal lines carry no sub-dimension — a sub-account
/// would leave the bank with no ledger identity at all.
/// </summary>
public sealed class BankLedgerService
{
    /// <summary>Prefix of the immutable AccountSystemName, which is what makes creation idempotent.</summary>
    private const string SystemNamePrefix = "BANK:";

    private readonly AccountingDbContext _db;

    public BankLedgerService(AccountingDbContext db) => _db = db;

    /// <summary>
    /// Creates the account, or returns the existing one. Idempotent on
    /// <c>AccountSystemName</c>, so a retried call after a dropped response
    /// cannot produce a second account for the same bank account.
    /// </summary>
    public async Task<BankLedgerResult> ProvisionAsync(
        ProvisionBankAccountRequest request, CancellationToken ct)
    {
        string systemName = SystemNamePrefix + request.BankAccountId;

        Account? existing = await _db.Accounts
            .FirstOrDefaultAsync(a => a.AccountSystemName == systemName, ct);

        if (existing is not null)
        {
            return new BankLedgerResult(
                BankLedgerOutcome.Ok, existing.AccountId, existing.AccountCode);
        }

        if (!Enum.TryParse(request.AccountType, ignoreCase: true, out BankAccountKind kind))
        {
            return new BankLedgerResult(BankLedgerOutcome.InvalidValue, null, null);
        }

        (SystemAccount parentName, int accountTypeId) = MappingFor(kind);

        Account? parent = await _db.Accounts.FirstOrDefaultAsync(
            a => a.AccountSystemName == SystemAccountNames.Of(parentName), ct);

        if (parent is null)
        {
            return new BankLedgerResult(BankLedgerOutcome.ParentMissing, null, null);
        }

        string code = await NextCodeAsync(parent, ct);

        var account = new Account
        {
            AccountTypeId = accountTypeId,
            AccountCode = code,
            AccountSystemName = systemName,
            AccountName = request.AccountName.Trim(),
            ParentAccountId = parent.AccountId,
            CurrencyCode = request.CurrencyCode,
            // System-managed: config-locked from creation and undeletable, which
            // is exactly right for an account tied to a real bank account.
            IsSystemDefault = true,
            IsActive = true,
            IsBank = true,
            IsPayment = true,
            // Bank charges, interest and opening balances are routinely posted
            // by hand, so a manual journal must be able to reach this account.
            IsJE = true,
        };

        _db.Accounts.Add(account);

        // The parent gains a child, so its own configuration is now referenced.
        parent.IsUsed = true;

        await _db.SaveChangesAsync(ct);
        return new BankLedgerResult(BankLedgerOutcome.Ok, account.AccountId, account.AccountCode);
    }

    /// <summary>
    /// Renames or deactivates the account. Banking owns the name, so this is a
    /// one-way push — the Chart of Accounts screen shows it read-only for these
    /// accounts rather than letting the two drift.
    /// </summary>
    public async Task<BankLedgerOutcome> UpdateAsync(
        long bankAccountId, string accountName, bool isActive, CancellationToken ct)
    {
        string systemName = SystemNamePrefix + bankAccountId;

        Account? account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.AccountSystemName == systemName, ct);

        if (account is null)
        {
            return BankLedgerOutcome.NotFound;
        }

        account.AccountName = accountName.Trim();
        account.IsActive = isActive;

        await _db.SaveChangesAsync(ct);
        return BankLedgerOutcome.Ok;
    }

    /// <summary>
    /// Which group the account hangs under, and what it is. Overdrafts, cash
    /// credit and credit cards are Liabilities: an overdrawn account is
    /// borrowing, and reporting it as a negative asset is what gets queried.
    /// </summary>
    private static (SystemAccount Parent, int AccountTypeId) MappingFor(BankAccountKind kind) =>
        kind switch
        {
            BankAccountKind.Cash or BankAccountKind.Wallet => (SystemAccount.CashInHand, AssetTypeId),
            BankAccountKind.OverDraft or BankAccountKind.CashCredit or BankAccountKind.CreditCard =>
                (SystemAccount.BankOverdraftAndCards, LiabilityTypeId),
            _ => (SystemAccount.BankAccounts, AssetTypeId),
        };

    /// <summary>
    /// The next free code under the parent — 1501, 1502 and so on. Retried by
    /// the caller on a unique-index conflict rather than read-max-then-increment,
    /// which hands two concurrent creates the same code.
    /// </summary>
    private async Task<string> NextCodeAsync(Account parent, CancellationToken ct)
    {
        List<string> siblings = await _db.Accounts
            .Where(a => a.ParentAccountId == parent.AccountId)
            .Select(a => a.AccountCode)
            .ToListAsync(ct);

        int highest = int.TryParse(parent.AccountCode, out int parentCode) ? parentCode : 0;

        foreach (string code in siblings)
        {
            if (int.TryParse(code, out int value) && value > highest)
            {
                highest = value;
            }
        }

        return (highest + 1).ToString();
    }

    // mst.AccountTypes ids — contractual, same constants the seed uses.
    private const int AssetTypeId = 1;
    private const int LiabilityTypeId = 2;
}

/// <summary>The kinds of bank account Banking can ask for. Mirrors bnk.BankAccounts.AccountType.</summary>
public enum BankAccountKind
{
    Savings = 0,
    Current = 1,
    OverDraft = 2,
    CashCredit = 3,
    CreditCard = 4,
    Wallet = 5,
    Cash = 6,
}
