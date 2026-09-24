using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;
using Shared.Kernel.Tenancy;

namespace Payroll.Entity.TableEntities;

public class SalaryHold : OrgScopedEntity
{
    public long SalaryHoldId { get; set; }
    public long EmployeeId { get; set; }

    public DateOnly FromMonth { get; set; }
    public DateOnly? ToMonth { get; set; }
    
    public bool IsActive { get; set; }

    [MaxLength(200, ErrorMessage = "Reason cannot exceed 200 characters.")]
    public string? Reason { get; set; }
}
