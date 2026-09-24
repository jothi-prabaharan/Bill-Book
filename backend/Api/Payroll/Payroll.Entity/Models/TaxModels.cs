using System.ComponentModel.DataAnnotations;

namespace Payroll.Entity.Models;

public sealed class SaveTaxDeclarationRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Employee ID is required.")]
    public long EmployeeId { get; set; }

    [Required(ErrorMessage = "Financial year is required.")]
    [MaxLength(10, ErrorMessage = "Financial year cannot exceed 10 characters.")]
    public string FinancialYear { get; set; } = "2026-2027";

    [Required(ErrorMessage = "Regime is required.")]
    [MaxLength(10, ErrorMessage = "Regime cannot exceed 10 characters.")]
    public string Regime { get; set; } = "New";

    public List<SaveTaxDeclarationLineRequest> Lines { get; set; } = [];

    public List<SaveRentDetailRequest> RentDetails { get; set; } = [];
}

public sealed class SaveTaxDeclarationLineRequest
{
    [Required(ErrorMessage = "Section code is required.")]
    [MaxLength(20, ErrorMessage = "Section code cannot exceed 20 characters.")]
    public string Section { get; set; } = null!;

    public decimal DeclaredAmount { get; set; }

    public decimal ProofAmount { get; set; }

    public decimal VerifiedAmount { get; set; }

    [MaxLength(200, ErrorMessage = "Remarks cannot exceed 200 characters.")]
    public string? Remarks { get; set; }
}

public sealed class SaveRentDetailRequest
{
    public DateOnly Month { get; set; }

    public decimal RentAmount { get; set; }

    [MaxLength(20, ErrorMessage = "Landlord PAN cannot exceed 20 characters.")]
    public string? LandlordPan { get; set; }

    [MaxLength(100, ErrorMessage = "Landlord Name cannot exceed 100 characters.")]
    public string? LandlordName { get; set; }

    [MaxLength(200, ErrorMessage = "Landlord Address cannot exceed 200 characters.")]
    public string? LandlordAddress { get; set; }
}

public sealed class SavePreviousEmployerIncomeRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Employee ID is required.")]
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

public sealed class Form16PartBModel
{
    public long EmployeeId { get; set; }
    public string FinancialYear { get; set; } = "2026-2027";
    public string Regime { get; set; } = "New";
    public decimal GrossSalary { get; set; }
    public decimal TotalExemptions { get; set; }
    public decimal StandardDeduction { get; set; }
    public decimal ChapterVIADeductions { get; set; }
    public decimal TotalTaxableIncome { get; set; }
    public decimal TaxPayable { get; set; }
    public decimal Relief87A { get; set; }
    public decimal Cess { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TdsDeducted { get; set; }
}

public sealed class Form12BaModel
{
    public long EmployeeId { get; set; }
    public string FinancialYear { get; set; } = "2026-2027";
    public List<PerquisiteItemModel> Perquisites { get; set; } = [];
    public decimal TotalPerquisites { get; set; }
}

public sealed class PerquisiteItemModel
{
    public string NatureOfPerquisite { get; set; } = null!;
    public decimal ValueOfPerquisite { get; set; }
    public decimal AmountRecovered { get; set; }
    public decimal TaxableAmount { get; set; }
}

public sealed class Quarter24QDataModel
{
    public string FinancialYear { get; set; } = "2026-2027";
    public int Quarter { get; set; }
    public List<Quarter24QEmployeeLine> EmployeeLines { get; set; } = [];
}

public sealed class Quarter24QEmployeeLine
{
    public long EmployeeId { get; set; }
    public string Pan { get; set; } = string.Empty;
    public decimal TotalAmountCredited { get; set; }
    public decimal TotalTdsDeducted { get; set; }
    public decimal TotalTdsDeposited { get; set; }
    public string DateOfPayment { get; set; } = string.Empty;
}
