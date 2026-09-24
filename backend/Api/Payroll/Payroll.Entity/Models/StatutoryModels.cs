using System.ComponentModel.DataAnnotations;

namespace Payroll.Entity.Models;

public sealed class PfSettingView
{
    public long PfSettingId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public decimal EmployeeContributionRate { get; set; }
    public decimal EmployerContributionRate { get; set; }
    public decimal WageCeiling { get; set; }
    public bool RestrictToWageCeiling { get; set; }
    public decimal AdminChargesRate { get; set; }
    public decimal EdliRate { get; set; }
}

public sealed class SavePfSettingRequest
{
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public decimal EmployeeContributionRate { get; set; }
    public decimal EmployerContributionRate { get; set; }
    public decimal WageCeiling { get; set; }
    public bool RestrictToWageCeiling { get; set; }
    public decimal AdminChargesRate { get; set; }
    public decimal EdliRate { get; set; }
}

public sealed class EsiSettingView
{
    public long EsiSettingId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public decimal EmployeeContributionRate { get; set; }
    public decimal EmployerContributionRate { get; set; }
    public decimal WageCeiling { get; set; }
}

public sealed class SaveEsiSettingRequest
{
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public decimal EmployeeContributionRate { get; set; }
    public decimal EmployerContributionRate { get; set; }
    public decimal WageCeiling { get; set; }
}

public sealed class ProfessionalTaxSlabView
{
    public long ProfessionalTaxSlabId { get; set; }
    public int StateId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public decimal MinSalary { get; set; }
    public decimal MaxSalary { get; set; }
    public decimal MonthlyTax { get; set; }
    public decimal? Month12Tax { get; set; }
}

public sealed class SaveProfessionalTaxSlabRequest
{
    public int StateId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public decimal MinSalary { get; set; }
    public decimal MaxSalary { get; set; }
    public decimal MonthlyTax { get; set; }
    public decimal? Month12Tax { get; set; }
}

public sealed class LwfSettingView
{
    public long LwfSettingId { get; set; }
    public int StateId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public decimal EmployeeContribution { get; set; }
    public decimal EmployerContribution { get; set; }
    public string DeductionFrequency { get; set; } = "Monthly";
}

public sealed class SaveLwfSettingRequest
{
    public int StateId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public decimal EmployeeContribution { get; set; }
    public decimal EmployerContribution { get; set; }
    public string DeductionFrequency { get; set; } = "Monthly";
}

public sealed class GratuitySettingView
{
    public long GratuitySettingId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public decimal GratuityPercentage { get; set; }
}

public sealed class SaveGratuitySettingRequest
{
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public decimal GratuityPercentage { get; set; }
}

public sealed class BonusSettingView
{
    public long BonusSettingId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public decimal MinBonusPercentage { get; set; }
    public decimal MaxBonusPercentage { get; set; }
    public decimal CalculationCeiling { get; set; }
}

public sealed class SaveBonusSettingRequest
{
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public decimal MinBonusPercentage { get; set; }
    public decimal MaxBonusPercentage { get; set; }
    public decimal CalculationCeiling { get; set; }
}

public sealed class StatutoryReturnView
{
    public long StatutoryReturnId { get; set; }
    public DateOnly Month { get; set; }
    public string ReturnType { get; set; } = null!;
    public string FileContent { get; set; } = null!;
    public DateTimeOffset GeneratedAt { get; set; }
    public string Status { get; set; } = "Generated";
}
