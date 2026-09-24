using System.ComponentModel.DataAnnotations;
using Payroll.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Payroll.Entity.TableEntities;

public class FullAndFinalSettlement : OrgScopedEntity
{
    public long FullAndFinalSettlementId { get; set; }

    public long EmployeeId { get; set; }

    public long? SeparationId { get; set; }

    public DateOnly LastWorkingDate { get; set; }

    public FnfStatus Status { get; set; } = FnfStatus.Draft;

    public long? PayrollRunId { get; set; }

    public decimal NetPayable { get; set; }

    [MaxLength(500, ErrorMessage = "Remarks cannot exceed 500 characters.")]
    public string? Remarks { get; set; }

    public ICollection<FnfLine> Lines { get; set; } = new List<FnfLine>();
}

public class FnfLine : OrgScopedEntity
{
    public long FnfLineId { get; set; }

    public long FullAndFinalSettlementId { get; set; }

    public FullAndFinalSettlement Settlement { get; set; } = null!;

    public FnfLineKind Kind { get; set; }

    public decimal Amount { get; set; }

    public bool IsDeduction { get; set; }

    [MaxLength(200, ErrorMessage = "Remarks cannot exceed 200 characters.")]
    public string? Remarks { get; set; }
}
