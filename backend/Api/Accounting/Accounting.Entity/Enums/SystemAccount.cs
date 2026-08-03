namespace Accounting.Entity.Enums;

/// <summary>
/// The ten control accounts seeded into every organization's chart of accounts.
/// The seed writes <see cref="SystemAccountNames"/> into Account.AccountSystemName
/// and sub-account provisioning looks the parent up by the same name, so the two
/// must never drift — hence one enum rather than a string literal at each site.
/// </summary>
public enum SystemAccount
{
    AccountsReceivable = 1,
    Inventory = 2,
    InputGst = 3,
    AccountsPayable = 4,
    OutputGst = 5,
    OpeningBalanceEquity = 6,
    SalesRevenue = 7,
    CostOfGoodsSold = 8,
    RealizedFxGainLoss = 9,
    UnrealizedFxGainLoss = 10,

    /// <summary>Parent group for cash and wallet accounts. Locked: nothing posts to the group itself.</summary>
    CashInHand = 11,

    /// <summary>Parent group for savings and current accounts.</summary>
    BankAccounts = 12,

    /// <summary>
    /// Parent group for overdrafts, cash credit and credit cards. A Liability,
    /// not an Asset — an overdrawn account is borrowing, and showing it as a
    /// negative asset is what auditors query.
    /// </summary>
    BankOverdraftAndCards = 13,

    /// <summary>
    /// Money paid to a vendor before their bill arrives, and the excess when a
    /// payment runs past what was owed. An <b>Asset</b> — they owe us goods.
    ///
    /// Kept off Accounts Payable deliberately: a payable is what we owe against a
    /// document, and netting an unapplied advance into it would understate
    /// payables and put the aging out by the same amount.
    /// </summary>
    AdvanceToVendor = 14,

    /// <summary>
    /// Money received from a customer before an invoice exists, and the excess
    /// when a receipt runs past what was owed. A <b>Liability</b> — we owe them
    /// goods or the money back.
    /// </summary>
    AdvanceFromCustomer = 15,
}

/// <summary>
/// The canonical <c>AccountSystemName</c> for each control account. These strings
/// are stored data: once an organization is seeded they can never change, because
/// the column is the key provisioning and reporting resolve against.
/// </summary>
public static class SystemAccountNames
{
    public static string Of(SystemAccount account) => account switch
    {
        SystemAccount.AccountsReceivable => "Accounts Receivable",
        SystemAccount.Inventory => "Inventory",
        SystemAccount.InputGst => "Input GST",
        SystemAccount.AccountsPayable => "Accounts Payable",
        SystemAccount.OutputGst => "Output GST",
        SystemAccount.OpeningBalanceEquity => "Opening Balance Equity",
        SystemAccount.SalesRevenue => "Sales Revenue",
        SystemAccount.CostOfGoodsSold => "Cost of Goods Sold",
        SystemAccount.RealizedFxGainLoss => "Realized FX Gain/Loss",
        SystemAccount.UnrealizedFxGainLoss => "Unrealized FX Gain/Loss",
        SystemAccount.CashInHand => "Cash in Hand",
        SystemAccount.BankAccounts => "Bank Accounts",
        SystemAccount.BankOverdraftAndCards => "Bank OD & Credit Cards",
        SystemAccount.AdvanceToVendor => "Advance to Vendor",
        SystemAccount.AdvanceFromCustomer => "Advance from Customer",
        _ => throw new ArgumentOutOfRangeException(nameof(account), account, "Unknown system account."),
    };
}
