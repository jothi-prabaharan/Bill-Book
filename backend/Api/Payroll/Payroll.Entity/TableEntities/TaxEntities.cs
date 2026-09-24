using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Payroll.Entity.TableEntities;

public class TaxSlab : OrgScopedEntity
{
    public long TaxSlabId { get; set; }

    [Required(ErrorMessage = "Financial year is required.")]
    [MaxLength(10, ErrorMessage = "Financial year cannot exceed 10 characters.")]
    public string FinancialYear { get; set; } = "2026-2027";

    [Required(ErrorMessage = "Regime is required.")]
    [MaxLength(10, ErrorMessage = "Regime cannot exceed 10 characters.")]
    public string Regime { get; set; } = "New"; // "Old" or "New"

    public decimal MinIncome { get; set; }

    public decimal MaxIncome { get; set; }

    public decimal TaxRate { get; set; }

    public decimal CessRate { get; set; } = 4.0m;
}

public class TaxRule : OrgScopedEntity
{
    public long TaxRuleId { get; set; }

    [Required(ErrorMessage = "Financial year is required.")]
    [MaxLength(10, ErrorMessage = "Financial year cannot exceed 10 characters.")]
    public string FinancialYear { get; set; } = "2026-2027";

    [Required(ErrorMessage = "Regime is required.")]
    [MaxLength(10, ErrorMessage = "Regime cannot exceed 10 characters.")]
    public string Regime { get; set; } = "All"; // "Old", "New", "All"

    [Required(ErrorMessage = "Section code is required.")]
    [MaxLength(20, ErrorMessage = "Section code cannot exceed 20 characters.")]
    public string Section { get; set; } = null!; // e.g. 80C, 80D, StandardDeduction, HRA

    public decimal MaxLimit { get; set; }

    [MaxLength(200, ErrorMessage = "Description cannot exceed 200 characters.")]
    public string? Description { get; set; }
}

public class TaxDeclaration : OrgScopedEntity
{
    public long TaxDeclarationId { get; set; }

    public long EmployeeId { get; set; }

    [Required(ErrorMessage = "Financial year is required.")]
    [MaxLength(10, ErrorMessage = "Financial year cannot exceed 10 characters.")]
    public string FinancialYear { get; set; } = "2026-2027";

    [Required(ErrorMessage = "Regime is required.")]
    [MaxLength(10, ErrorMessage = "Regime cannot exceed 10 characters.")]
    public string Regime { get; set; } = "New";

    public bool IsLocked { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    public List<TaxDeclarationLine> Lines { get; set; } = [];

    public List<RentDetail> RentDetails { get; set; } = [];
}

public class TaxDeclarationLine : OrgScopedEntity
{
    public long TaxDeclarationLineId { get; set; }

    public long TaxDeclarationId { get; set; }

    public TaxDeclaration Declaration { get; set; } = null!;

    [Required(ErrorMessage = "Section is required.")]
    [MaxLength(20, ErrorMessage = "Section cannot exceed 20 characters.")]
    public string Section { get; set; } = null!; // 80C, 80D, 80CCD_1B, 24B

    public decimal DeclaredAmount { get; set; }

    public decimal ProofAmount { get; set; }

    public decimal VerifiedAmount { get; set; }

    [MaxLength(200, ErrorMessage = "Remarks cannot exceed 200 characters.")]
    public string? Remarks { get; set; }
}

public class RentDetail : OrgScopedEntity
{
    public long RentDetailId { get; set; }

    public long TaxDeclarationId { get; set; }

    public TaxDeclaration Declaration { get; set; } = null!;

    public DateOnly Month { get; set; }

    public decimal RentAmount { get; set; }

    [MaxLength(20, ErrorMessage = "Landlord PAN cannot exceed 20 characters.")]
    public string? LandlordPan { get; set; }

    [MaxLength(100, ErrorMessage = "Landlord Name cannot exceed 100 characters.")]
    public string? LandlordName { get; set; }

    [MaxLength(200, ErrorMessage = "Landlord Address cannot exceed 200 characters.")]
    public string? LandlordAddress { get; set; }
}

public class PreviousEmployerIncome : OrgScopedEntity
{
    public long PreviousEmployerIncomeId { get; set; }

    public long EmployeeId { get; set; }

    [Required(ErrorMessage = "Financial year is required.")]
    [MaxLength(10, ErrorMessage = "Financial year cannot exceed 10 characters.")]
    public string FinancialYear { get; set; } = "2026-2027";

    public decimal GrossIncome { get; set; }

    public decimal Exemptions { get; set; }

    public decimal ProfessionalTax { get; set; }

    public decimal ProvidentFund { get; set; }

    public decimal TotalTdsDeducted { get; set; }

    [MaxLength(100, ErrorMessage = "Employer name cannot exceed 100 characters.")]
    public string? EmployerName { get; set; }
}
