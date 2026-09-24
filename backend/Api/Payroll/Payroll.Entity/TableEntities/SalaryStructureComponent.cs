using Shared.Kernel.Entities;
using Shared.Kernel.Tenancy;
using Payroll.Entity.Enums;

namespace Payroll.Entity.TableEntities;

public class SalaryStructureComponent : AuditableEntity
{
    public long SalaryStructureComponentId { get; set; }
    public long SalaryStructureId { get; set; }
    public long SalaryComponentId { get; set; }
    
    public SalaryValueType ValueType { get; set; }
    public decimal? FlatAmount { get; set; }
    public decimal? Percentage { get; set; }
    
    public SalaryStructure Structure { get; set; } = null!;
    public SalaryComponent Component { get; set; } = null!;
}
