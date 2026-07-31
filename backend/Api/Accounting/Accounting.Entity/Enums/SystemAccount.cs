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
        _ => throw new ArgumentOutOfRangeException(nameof(account), account, "Unknown system account."),
    };
}
