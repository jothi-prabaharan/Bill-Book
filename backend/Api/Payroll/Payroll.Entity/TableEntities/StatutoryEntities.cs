using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Payroll.Entity.TableEntities;

public class PfSetting : OrgScopedEntity
{
    public long PfSettingId { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public decimal EmployeeContributionRate { get; set; } = 12.0m;

    public decimal EmployerContributionRate { get; set; } = 12.0m;

    public decimal WageCeiling { get; set; } = 15000.0m;

    public bool RestrictToWageCeiling { get; set; } = true;

    public decimal AdminChargesRate { get; set; } = 0.50m;

    public decimal EdliRate { get; set; } = 0.50m;
}

public class EsiSetting : OrgScopedEntity
{
    public long EsiSettingId { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public decimal EmployeeContributionRate { get; set; } = 0.75m;

    public decimal EmployerContributionRate { get; set; } = 3.25m;

    public decimal WageCeiling { get; set; } = 21000.0m;
}

public class ProfessionalTaxSlab : OrgScopedEntity
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

public class LwfSetting : OrgScopedEntity
{
    public long LwfSettingId { get; set; }

    public int StateId { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public decimal EmployeeContribution { get; set; }

    public decimal EmployerContribution { get; set; }

    [Required(ErrorMessage = "Deduction frequency is required.")]
    [MaxLength(20, ErrorMessage = "Deduction frequency cannot exceed 20 characters.")]
    public string DeductionFrequency { get; set; } = "Monthly"; // Monthly, HalfYearly, Yearly
}

public class GratuitySetting : OrgScopedEntity
{
    public long GratuitySettingId { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public decimal GratuityPercentage { get; set; } = 4.81m; // 15/26 / 12 ~ 4.81%
}

public class BonusSetting : OrgScopedEntity
{
    public long BonusSettingId { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public decimal MinBonusPercentage { get; set; } = 8.33m;

    public decimal MaxBonusPercentage { get; set; } = 20.0m;

    public decimal CalculationCeiling { get; set; } = 7000.0m;
}

public class StatutoryReturn : OrgScopedEntity
{
    public long StatutoryReturnId { get; set; }

    public DateOnly Month { get; set; }

    [Required(ErrorMessage = "Return type is required.")]
    [MaxLength(20, ErrorMessage = "Return type cannot exceed 20 characters.")]
    public string ReturnType { get; set; } = null!; // PF_ECR, ESI, PT

    [Required(ErrorMessage = "File content is required.")]
    public string FileContent { get; set; } = null!;

    public DateTimeOffset GeneratedAt { get; set; }

    [MaxLength(20, ErrorMessage = "Status cannot exceed 20 characters.")]
    public string Status { get; set; } = "Generated";
}
