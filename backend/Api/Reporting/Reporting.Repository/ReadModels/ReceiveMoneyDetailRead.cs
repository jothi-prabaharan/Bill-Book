using System;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

public class ReceiveMoneyDetailRead : OrgScopedEntity
{
    public long ReceiveMoneyDetailId { get; set; }
    public long ReceiveMoneyId { get; set; }
    public int LineNumber { get; set; }
    public int LedgerSourceId { get; set; }
    public string? MappingTransactionTypeCode { get; set; }
    public long? MappingTransactionId { get; set; }
    public decimal Amount { get; set; }
    public decimal AmountBase { get; set; }
    public string? LineMemo { get; set; }
}
