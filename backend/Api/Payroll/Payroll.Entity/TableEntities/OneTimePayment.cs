using System.ComponentModel.DataAnnotations;
using Payroll.Entity.Enums;
using Shared.Kernel.Entities;
using Shared.Kernel.Tenancy;

namespace Payroll.Entity.TableEntities;

public class OneTimePayment : OrgScopedEntity
{
    public long OneTimePaymentId { get; set; }
    public long EmployeeId { get; set; }
    public long SalaryComponentId { get; set; }

    public decimal Amount { get; set; }
    
    public DateOnly Month { get; set; }
    
    public bool Processed { get; set; }

    public SalaryComponent Component { get; set; } = null!;
}
