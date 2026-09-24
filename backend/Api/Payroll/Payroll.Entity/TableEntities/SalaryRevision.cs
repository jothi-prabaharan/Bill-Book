using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;
using Shared.Kernel.Tenancy;

namespace Payroll.Entity.TableEntities;

public class SalaryRevision : OrgScopedEntity
{
    public long SalaryRevisionId { get; set; }
    public long EmployeeId { get; set; }
    public long EmployeeSalaryId { get; set; }

    public decimal PreviousCtc { get; set; }
    public decimal NewCtc { get; set; }
    
    public DateOnly EffectiveFrom { get; set; }
    
    [MaxLength(200, ErrorMessage = "Reason cannot exceed 200 characters.")]
    public string? Reason { get; set; }
    
    public bool ArrearsProcessed { get; set; }

    public EmployeeSalary Salary { get; set; } = null!;
}
