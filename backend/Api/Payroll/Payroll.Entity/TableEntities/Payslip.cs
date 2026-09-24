using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;
using Shared.Kernel.Tenancy;

namespace Payroll.Entity.TableEntities;

public class Payslip : AuditableEntity
{
    public long PayslipId { get; set; }
    public long PayrollRunId { get; set; }
    public long EmployeeId { get; set; }

    public decimal PaidDays { get; set; }
    public decimal GrossEarnings { get; set; }
    public decimal GrossDeductions { get; set; }
    public decimal NetPay { get; set; }

    public PayrollRun Run { get; set; } = null!;
    public ICollection<PayslipLine> Lines { get; set; } = new List<PayslipLine>();
}
