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

    public static PfSetting DefaultPf(Guid orgId) => new()
    {
        OrgId = orgId,
        EffectiveFrom = new DateOnly(2020, 1, 1),
        EmployeeContributionRate = 12.0m,
        EmployerContributionRate = 12.0m,
        WageCeiling = 15000.0m,
        RestrictToWageCeiling = true,
        AdminChargesRate = 0.50m,
        EdliRate = 0.50m
    };

    public static EsiSetting DefaultEsi(Guid orgId) => new()
    {
        OrgId = orgId,
        EffectiveFrom = new DateOnly(2020, 1, 1),
        EmployeeContributionRate = 0.75m,
        EmployerContributionRate = 3.25m,
        WageCeiling = 21000.0m
    };

    public static GratuitySetting DefaultGratuity(Guid orgId) => new()
    {
        OrgId = orgId,
        EffectiveFrom = new DateOnly(2020, 1, 1),
        GratuityPercentage = 4.81m
    };

    public static BonusSetting DefaultBonus(Guid orgId) => new()
    {
        OrgId = orgId,
        EffectiveFrom = new DateOnly(2020, 1, 1),
        MinBonusPercentage = 8.33m,
        MaxBonusPercentage = 20.0m,
        CalculationCeiling = 7000.0m
    };

    public static IReadOnlyList<ProfessionalTaxSlab> DefaultPtSlabs(Guid orgId) =>
    [
        // Karnataka (StateId: 10)
        new() { OrgId = orgId, StateId = 10, EffectiveFrom = new DateOnly(2020, 1, 1), MinSalary = 0, MaxSalary = 15000, MonthlyTax = 0 },
        new() { OrgId = orgId, StateId = 10, EffectiveFrom = new DateOnly(2020, 1, 1), MinSalary = 15001, MaxSalary = 9999999, MonthlyTax = 200 },

        // Maharashtra (StateId: 14)
        new() { OrgId = orgId, StateId = 14, EffectiveFrom = new DateOnly(2020, 1, 1), MinSalary = 0, MaxSalary = 7500, MonthlyTax = 0 },
        new() { OrgId = orgId, StateId = 14, EffectiveFrom = new DateOnly(2020, 1, 1), MinSalary = 7501, MaxSalary = 10000, MonthlyTax = 175 },
        new() { OrgId = orgId, StateId = 14, EffectiveFrom = new DateOnly(2020, 1, 1), MinSalary = 10001, MaxSalary = 9999999, MonthlyTax = 200, Month12Tax = 300 },

        // Telangana (StateId: 24)
        new() { OrgId = orgId, StateId = 24, EffectiveFrom = new DateOnly(2020, 1, 1), MinSalary = 0, MaxSalary = 15000, MonthlyTax = 0 },
        new() { OrgId = orgId, StateId = 24, EffectiveFrom = new DateOnly(2020, 1, 1), MinSalary = 15001, MaxSalary = 20000, MonthlyTax = 150 },
        new() { OrgId = orgId, StateId = 24, EffectiveFrom = new DateOnly(2020, 1, 1), MinSalary = 20001, MaxSalary = 9999999, MonthlyTax = 200 },

        // Tamil Nadu (StateId: 23)
        new() { OrgId = orgId, StateId = 23, EffectiveFrom = new DateOnly(2020, 1, 1), MinSalary = 0, MaxSalary = 21000, MonthlyTax = 0 },
        new() { OrgId = orgId, StateId = 23, EffectiveFrom = new DateOnly(2020, 1, 1), MinSalary = 21001, MaxSalary = 30000, MonthlyTax = 100 },
        new() { OrgId = orgId, StateId = 23, EffectiveFrom = new DateOnly(2020, 1, 1), MinSalary = 30001, MaxSalary = 9999999, MonthlyTax = 208 }
    ];

    public static IReadOnlyList<LwfSetting> DefaultLwf(Guid orgId) =>
    [
        new() { OrgId = orgId, StateId = 14, EffectiveFrom = new DateOnly(2020, 1, 1), EmployeeContribution = 12m, EmployerContribution = 36m, DeductionFrequency = "HalfYearly" },
        new() { OrgId = orgId, StateId = 10, EffectiveFrom = new DateOnly(2020, 1, 1), EmployeeContribution = 20m, EmployerContribution = 40m, DeductionFrequency = "Yearly" }
    ];
}
