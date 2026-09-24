using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;
using Shared.Kernel.Tenancy;

namespace Payroll.Entity.TableEntities;

public class MonthlyAttendanceInput : OrgScopedEntity
{
    public long MonthlyAttendanceInputId { get; set; }
    public long EmployeeId { get; set; }

    public DateOnly Month { get; set; }
    
    public decimal PaidDays { get; set; }
    public decimal LossOfPayDays { get; set; }
}
