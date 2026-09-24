using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;
using Shared.Kernel.Tenancy;

namespace Payroll.Entity.TableEntities;

public class EmployeeSalary : OrgScopedEntity
{
    public long EmployeeSalaryId { get; set; }
    public long EmployeeId { get; set; }
    public long SalaryStructureId { get; set; }

    public decimal AnnualCtc { get; set; }
    
    public DateOnly EffectiveFrom { get; set; }
    
    public SalaryStructure Structure { get; set; } = null!;
}
