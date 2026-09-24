using System.ComponentModel.DataAnnotations;
using Payroll.Entity.Enums;
using Shared.Kernel.Entities;
using Shared.Kernel.Tenancy;

namespace Payroll.Entity.TableEntities;

public class PayslipLine : AuditableEntity
{
    public long PayslipLineId { get; set; }
    public long PayslipId { get; set; }
    public long SalaryComponentId { get; set; }

    public ComponentKind Kind { get; set; }
    public decimal Amount { get; set; }

    public Payslip Payslip { get; set; } = null!;
}
