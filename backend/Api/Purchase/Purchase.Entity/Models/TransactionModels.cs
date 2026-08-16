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
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly? DueDate { get; set; }
}
