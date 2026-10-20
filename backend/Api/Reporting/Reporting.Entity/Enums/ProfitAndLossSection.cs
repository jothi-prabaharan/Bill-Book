namespace Reporting.Entity.Enums;

/// <summary>The parts of a profit and loss, in the order it reads (TK-200).</summary>
public enum ProfitAndLossSection
{
    /// <summary>Income accounts for sales, with returns and discounts netting off.</summary>
    Revenue = 1,

    /// <summary>Cost of goods sold less purchase returns.</summary>
    CostOfSales = 2,

    /// <summary>Every other income account: fees aside, FX gains, disposal gains.</summary>
    OtherIncome = 3,

    /// <summary>Every other expense account.</summary>
    Expenses = 4,
}
