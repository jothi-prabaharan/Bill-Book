using System.ComponentModel.DataAnnotations;
using Payroll.Entity.Enums;
using Shared.Kernel.Entities;
using Shared.Kernel.Tenancy;

namespace Payroll.Entity.TableEntities;

public class PayrollRun : OrgScopedEntity
{
    public long PayrollRunId { get; set; }

    public DateOnly Month { get; set; }
    
    public PayrollRunStatus Status { get; set; }
    public PayrollRunKind Kind { get; set; } = PayrollRunKind.Regular;
    
    public int EmployeeCount { get; set; }
    public decimal TotalNetPay { get; set; }
    
    [MaxLength(50, ErrorMessage = "Days source cannot exceed 50 characters.")]
    public string DaysSource { get; set; } = null!;
    
    // Populated on post
    public long? JournalId { get; set; }

    public ICollection<Payslip> Payslips { get; set; } = new List<Payslip>();
}
