using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;
using Shared.Kernel.Tenancy;

namespace Payroll.Entity.TableEntities;

public class SalaryStructure : OrgScopedEntity
{
    public long SalaryStructureId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public ICollection<SalaryStructureComponent> Components { get; set; } = new List<SalaryStructureComponent>();
}
