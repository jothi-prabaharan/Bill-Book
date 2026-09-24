using System.ComponentModel.DataAnnotations;
using Payroll.Entity.Enums;
using Shared.Kernel.Entities;
using Shared.Kernel.Tenancy;

namespace Payroll.Entity.TableEntities;

public class SalaryComponent : OrgScopedEntity
{
    public long SalaryComponentId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public ComponentKind Kind { get; set; }
    
    public SalaryValueType ValueType { get; set; }

    public bool IsTaxable { get; set; }

    [MaxLength(500, ErrorMessage = "Formula cannot exceed 500 characters.")]
    public string? Formula { get; set; }
    
    // Links to acc.Accounts for posting
    public long? LedgerAccountId { get; set; }
}
