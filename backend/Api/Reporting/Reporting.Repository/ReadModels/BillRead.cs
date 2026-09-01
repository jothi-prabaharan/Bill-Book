using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

public class BillRead : OrgScopedEntity
{
    public long BillId { get; set; }
    public string TransactionTypeCode { get; set; } = null!;
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public long ContactId { get; set; }
    public DateOnly DueDate { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public Shared.Kernel.Documents.DocumentStatus Status { get; set; }
}
