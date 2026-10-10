using System;

namespace Purchase.Entity.Models;

public class PurchaseTransactionListItem
{
    public long TransactionId { get; set; }
    public string TransactionType { get; set; } = string.Empty; // "Bill", "PurchaseOrder", "DebitNote", "GoodsReceipt"
    public string DocumentNo { get; set; } = string.Empty;
    public DateOnly DocumentDate { get; set; }
    public long ContactId { get; set; }
    public string? ContactName { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>The value before tax, in <see cref="CurrencyCode"/> (TK-187).</summary>
    public decimal TaxableAmount { get; set; }

    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// An order's fulfilment — Open, Partly…, Closed, Cancelled — for the
    /// list's status once the order is confirmed; null for every other type (TK-187).
    /// </summary>
    public string? FulfilmentStatus { get; set; }
    public DateOnly? DueDate { get; set; }
}

/// <summary>
/// The document a printed number names (TK-162): which kind it is and its id,
/// so a scanned document code can open it.
/// </summary>
public sealed class DocumentNumberMatch
{
    public string TransactionType { get; set; } = string.Empty;

    public long TransactionId { get; set; }
}
