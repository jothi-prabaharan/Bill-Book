namespace Reporting.Entity.Enums;

/// <summary>The three activities a cash flow statement groups its lines into, in order (TK-201).</summary>
public enum CashFlowActivity
{
    /// <summary>Trading: customers, suppliers, staff, taxes and everything not below.</summary>
    Operating = 1,

    /// <summary>Fixed assets bought and sold.</summary>
    Investing = 2,

    /// <summary>The owners' capital and drawings.</summary>
    Financing = 3,
}
