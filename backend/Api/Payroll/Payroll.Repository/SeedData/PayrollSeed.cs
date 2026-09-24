using Payroll.Entity.Enums;
using Payroll.Entity.TableEntities;

namespace Payroll.Repository.SeedData;

public static class PayrollSeed
{
    public static PayGroup DefaultPayGroup(Guid orgId) => new()
    {
        OrgId = orgId,
        Name = "Default"
    };

    public static SalaryComponent BasicSalary(Guid orgId) => new()
    {
        OrgId = orgId,
        Name = "Basic Salary",
        Kind = ComponentKind.Earning,
        ValueType = SalaryValueType.FlatAmount,
        IsTaxable = true
    };
}
