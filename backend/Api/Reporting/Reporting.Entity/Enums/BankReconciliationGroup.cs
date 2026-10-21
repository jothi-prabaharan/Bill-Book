namespace Reporting.Entity.Enums;

/// <summary>The items standing between the books and the bank statement, in the order a reconciliation reads (TK-202).</summary>
public enum BankReconciliationGroup
{
    /// <summary>Money paid out in the books that no statement line up to the date shows yet.</summary>
    OutstandingPayments = 1,

    /// <summary>Money received in the books that no statement line up to the date shows yet.</summary>
    OutstandingReceipts = 2,

    /// <summary>Statement lines up to the date with no document in the books on or before it.</summary>
    StatementLinesNotInBooks = 3,
}
