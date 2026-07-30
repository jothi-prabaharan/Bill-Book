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
        Account(orgId, "1100", "Accounts Receivable", Asset),
        Account(orgId, "1200", "Inventory", Asset),
        Account(orgId, "1300", "Input GST", Asset),
        Account(orgId, "2100", "Accounts Payable", Liability),
        Account(orgId, "2200", "Output GST", Liability),
        Account(orgId, "3100", "Opening Balance Equity", Equity, isJe: true),
        Account(orgId, "4100", "Sales Revenue", Income, isSales: true),
        Account(orgId, "5100", "Cost of Goods Sold", Expense, isPurchase: true),
        Account(orgId, "4900", "Realized FX Gain/Loss", Income, isJe: true),
        Account(orgId, "4910", "Unrealized FX Gain/Loss", Income, isJe: true),
    ];

    private static Account Account(
        Guid orgId,
        string code,
        string name,
        int accountTypeId,
        bool isJe = false,
        bool isSales = false,
        bool isPurchase = false) =>
        new()
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
        };
}
