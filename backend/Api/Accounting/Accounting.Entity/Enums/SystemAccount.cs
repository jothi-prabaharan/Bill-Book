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
    /// Goods received but not yet billed — a Liability, and a clearing account
    /// rather than a resting place.
    ///
    /// The goods receipt opens the obligation (<c>Dr Inventory / Cr GRNI</c>) and
    /// the bill closes it (<c>Dr GRNI / Cr Accounts Payable</c>). A balance
    /// sitting here is stock on the shelf that no vendor has invoiced yet, which
    /// is a figure a controller actually wants — and the alternative, posting
    /// nothing at the receipt, understates the inventory asset for as long as the
    /// bill takes to arrive.
    ///
    /// Decision T4.1, in <c>docs/Purchase.md</c> §8.
    /// </summary>
    GoodsReceivedNotInvoiced = 14,

    /// <summary>
    /// Capitalised purchases — an Asset.
    ///
    /// <b>A holding account until the fixed asset register exists.</b> The design
    /// is that a fixed asset <i>category</i> owns the GL mapping and a capital
    /// line creates the register row (Purchase.md §4), but the register is Phase
    /// 2 and the category table does not exist. A capital line has to land
    /// somewhere real in the meantime, and an asset sitting in an expense or a
    /// control account is worse than one sitting here.
    ///
    /// When the register lands, the category's own mapping supersedes this and
    /// the balance is split out across the real asset accounts.
    /// </summary>
    FixedAsset = 15,

    /// <summary>
    /// Goods sent back to a vendor — a <b>contra Expense</b>.
    ///
    /// <b>Contra is the whole point.</b> A return reduces what was bought, and
    /// booking it as a negative expense would leave every report that groups by
    /// account type adding a negative number. <c>IsContra</c> tells a report to
    /// subtract it instead, which is what keeps cost of goods and the purchase
    /// total honest. CLAUDE.md lists it alongside Sales Returns and Discount
    /// Given for the same reason.
    /// </summary>
    PurchaseReturns = 16,
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
        SystemAccount.GoodsReceivedNotInvoiced => "Goods Received Not Invoiced",
        SystemAccount.FixedAsset => "Fixed Asset",
        SystemAccount.PurchaseReturns => "Purchase Returns",
        _ => throw new ArgumentOutOfRangeException(nameof(account), account, "Unknown system account."),
    };
}
