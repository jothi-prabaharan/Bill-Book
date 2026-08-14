using Accounting.Entity.Enums;
using Accounting.Entity.TableEntities;

namespace Accounting.Repository.SeedData;

/// <summary>
/// The default chart of accounts written when an organization is created. Not
/// EF seed data — these rows are per-organization, so they are inserted at org
/// creation rather than by migration.
///
/// Flags follow the rule that the system posts to control accounts directly:
/// AR, AP, Inventory and GST are off the manual-journal picker, because a hand
/// posting to a control account would break its tie to the sub-ledger.
/// </summary>
public static class ChartOfAccountsSeed
{
    // mst.AccountTypes ids — contractual.
    private const int Asset = 1;
    private const int Liability = 2;
    private const int Equity = 3;
    private const int Income = 4;
    private const int Expense = 5;

    public static IReadOnlyList<Account> Build(Guid orgId) =>
    [
        Account(orgId, "1100", SystemAccount.AccountsReceivable, Asset),
        Account(orgId, "1200", SystemAccount.Inventory, Asset),
        Account(orgId, "1300", SystemAccount.InputGst, Asset),
        Account(orgId, "2100", SystemAccount.AccountsPayable, Liability),

        // Goods received and not yet billed. Off the manual-journal picker for
        // the same reason AR and AP are: it is cleared by the bill that matches
        // the receipt, and a hand posting would leave a residue that no document
        // can ever clear. Decision T4.1 — docs/modules/Purchase.md §8.
        Account(orgId, "2150", SystemAccount.GoodsReceivedNotInvoiced, Liability),

        Account(orgId, "2200", SystemAccount.OutputGst, Liability),
        Account(orgId, "3100", SystemAccount.OpeningBalanceEquity, Equity, isJe: true),
        Account(orgId, "4100", SystemAccount.SalesRevenue, Income, isSales: true),
        Account(orgId, "5100", SystemAccount.CostOfGoodsSold, Expense, isPurchase: true),

        // Parent groups for bank accounts. Each bank account created in Banking
        // hangs a child account under one of these, so the chart of accounts
        // grows a line per real account rather than needing one seeded per bank.
        Account(orgId, "1400", SystemAccount.CashInHand, Asset, isLock: true),
        Account(orgId, "1500", SystemAccount.BankAccounts, Asset, isLock: true),
        Account(orgId, "2300", SystemAccount.BankOverdraftAndCards, Liability, isLock: true),
        Account(orgId, "4900", SystemAccount.RealizedFxGainLoss, Income, isJe: true),
        Account(orgId, "4910", SystemAccount.UnrealizedFxGainLoss, Income, isJe: true),
    ];

    // There are deliberately no separate advance control accounts. A contact's
    // prepayments and overpayments are sub-accounts beneath Accounts Receivable
    // and Accounts Payable, told apart by SubAccountPurpose — so the party's
    // whole position sits under two control accounts rather than four.
    //
    // The consequence to know: neither control total is a Schedule III line on
    // its own. Advances to suppliers and from customers must be reported apart
    // from trade receivables and payables, and the purpose column is what splits
    // them back out.

    private static Account Account(
        Guid orgId,
        string code,
        SystemAccount systemAccount,
        int accountTypeId,
        bool isJe = false,
        bool isSales = false,
        bool isPurchase = false,
        bool isLock = false)
    {
        string name = SystemAccountNames.Of(systemAccount);
        return new()
        {
            OrgId = orgId,
            AccountCode = code,
            AccountSystemName = name,
            AccountName = name,
            AccountTypeId = accountTypeId,
            IsSystemDefault = true,
            IsActive = true,
            IsJE = isJe,
            IsSales = isSales,
            IsPurchase = isPurchase,
            // A grouping row: locking it stops a posting landing on the group
            // instead of the account underneath.
            IsLock = isLock,
        };
    }
}
