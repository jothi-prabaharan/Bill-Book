using System;

namespace Sales.Entity.Models;

public class SalesTransactionListItem
{
    public long TransactionId { get; set; }
    public string TransactionType { get; set; } = string.Empty; // "Quote", "SalesOrder", "Invoice", "CreditNote"
    public string DocumentNo { get; set; } = string.Empty;
    public DateOnly DocumentDate { get; set; }
    public long ContactId { get; set; }
    public string? ContactName { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly? DueDate { get; set; } 

    /// <summary>A quote's answer from the customer portal: Accepted or Rejected, null when none (TK-96).</summary>
    public string? CustomerResponse { get; set; }
}
