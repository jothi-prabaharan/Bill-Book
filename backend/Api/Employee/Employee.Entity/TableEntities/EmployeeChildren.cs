using System.ComponentModel.DataAnnotations;
using Employee.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Employee.Entity.TableEntities;

// The employee's child tables (H1, TK-48). Each belongs to one employee, is
// saved with it, and carries the branch's CustomerId and OrgId like every
// tenant row.

/// <summary>One per kind: a current and a permanent address.</summary>
public class EmployeeAddress : OrgScopedEntity
{
    public long EmployeeAddressId { get; set; }

    public long EmployeeId { get; set; }

    public AddressKind AddressKind { get; set; }

    [Required(ErrorMessage = "Address line 1 is required.")]
    [MaxLength(200, ErrorMessage = "Address line 1 cannot exceed 200 characters.")]
    public string AddressLine1 { get; set; } = null!;

    [MaxLength(200, ErrorMessage = "Address line 2 cannot exceed 200 characters.")]
    public string? AddressLine2 { get; set; }

    [MaxLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string? City { get; set; }

    /// <summary>Unenforced — <c>mst.States</c>.</summary>
    public int? StateId { get; set; }

    [MaxLength(10, ErrorMessage = "Postal code cannot exceed 10 characters.")]
    public string? PostalCode { get; set; }
}

/// <summary>An emergency contact.</summary>
public class EmployeeContact : OrgScopedEntity
{
    public long EmployeeContactId { get; set; }

    public long EmployeeId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string Name { get; set; } = null!;

    public Relationship Relationship { get; set; }

    [Required(ErrorMessage = "Phone is required.")]
    [MaxLength(20, ErrorMessage = "Phone cannot exceed 20 characters.")]
    public string Phone { get; set; } = null!;

    public bool IsPrimary { get; set; }
}

public class EmployeeFamilyMember : OrgScopedEntity
{
    public long EmployeeFamilyMemberId { get; set; }

    public long EmployeeId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string Name { get; set; } = null!;

    public Relationship Relationship { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public bool IsDependent { get; set; }

    public bool IsEsiCovered { get; set; }
}

/// <summary>A nomination of a family member. The shares for one kind add up to 100.</summary>
public class EmployeeNominee : OrgScopedEntity
{
    public long EmployeeNomineeId { get; set; }

    public long EmployeeId { get; set; }

    public long FamilyMemberId { get; set; }

    /// <summary>The nominated family member, of the same employee.</summary>
    public EmployeeFamilyMember? FamilyMember { get; set; }

    public NominationKind NominationKind { get; set; }

    [Range(typeof(decimal), "0.01", "100", ErrorMessage = "A share must be more than 0 and at most 100 percent.")]
    public decimal SharePercent { get; set; }
}

public class EmployeeEducation : OrgScopedEntity
{
    public long EmployeeEducationId { get; set; }

    public long EmployeeId { get; set; }

    [Required(ErrorMessage = "Qualification is required.")]
    [MaxLength(100, ErrorMessage = "Qualification cannot exceed 100 characters.")]
    public string Qualification { get; set; } = null!;

    [Required(ErrorMessage = "Institution is required.")]
    [MaxLength(200, ErrorMessage = "Institution cannot exceed 200 characters.")]
    public string Institution { get; set; } = null!;

    [Range(1900, 2100, ErrorMessage = "Year of passing must be between 1900 and 2100.")]
    public int YearOfPassing { get; set; }

    [MaxLength(20, ErrorMessage = "Grade cannot exceed 20 characters.")]
    public string? Grade { get; set; }
}

public class PreviousEmployment : OrgScopedEntity
{
    public long PreviousEmploymentId { get; set; }

    public long EmployeeId { get; set; }

    [Required(ErrorMessage = "Employer is required.")]
    [MaxLength(200, ErrorMessage = "Employer cannot exceed 200 characters.")]
    public string Employer { get; set; } = null!;

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    [MaxLength(100, ErrorMessage = "Last designation cannot exceed 100 characters.")]
    public string? LastDesignation { get; set; }
}

/// <summary>A bank account. Exactly one is primary, and salary goes there.</summary>
public class EmployeeBankDetail : OrgScopedEntity
{
    public long EmployeeBankDetailId { get; set; }

    public long EmployeeId { get; set; }

    [Required(ErrorMessage = "Account holder is required.")]
    [MaxLength(200, ErrorMessage = "Account holder cannot exceed 200 characters.")]
    public string AccountHolder { get; set; } = null!;

    [Required(ErrorMessage = "Account number is required.")]
    [MaxLength(30, ErrorMessage = "Account number cannot exceed 30 characters.")]
    public string AccountNo { get; set; } = null!;

    [Required(ErrorMessage = "IFSC is required.")]
    [RegularExpression("^[A-Z]{4}0[A-Z0-9]{6}$", ErrorMessage = "IFSC must be four letters, a zero and six letters or digits.")]
    [MaxLength(11, ErrorMessage = "IFSC cannot exceed 11 characters.")]
    public string Ifsc { get; set; } = null!;

    [Required(ErrorMessage = "Bank name is required.")]
    [MaxLength(100, ErrorMessage = "Bank name cannot exceed 100 characters.")]
    public string BankName { get; set; } = null!;

    public bool IsPrimary { get; set; }
}

/// <summary>
/// The employee's past: appended at joining and at every change of department,
/// designation, grade, location or manager, never updated.
/// </summary>
public class EmploymentHistory : OrgScopedEntity
{
    public long EmploymentHistoryId { get; set; }

    public long EmployeeId { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public EmploymentChangeKind ChangeKind { get; set; }

    public long DepartmentId { get; set; }

    public long DesignationId { get; set; }

    public long GradeId { get; set; }

    public long WorkLocationId { get; set; }

    public long? ReportsToEmployeeId { get; set; }

    [MaxLength(500, ErrorMessage = "Remarks cannot exceed 500 characters.")]
    public string? Remarks { get; set; }
}

/// <summary>A document on file. An expiring one is reminded 30 days out.</summary>
public class EmployeeDocument : OrgScopedEntity
{
    public long EmployeeDocumentId { get; set; }

    public long EmployeeId { get; set; }

    public EmployeeDocumentKind DocumentKind { get; set; }

    [Required(ErrorMessage = "The file is required.")]
    [MaxLength(500, ErrorMessage = "File key cannot exceed 500 characters.")]
    public string AttachmentKey { get; set; } = null!;

    public DateOnly? ValidUntil { get; set; }
}

/// <summary>Something issued to the employee. An unreturned asset blocks exit clearance.</summary>
public class AssetIssue : OrgScopedEntity
{
    public long AssetIssueId { get; set; }

    public long EmployeeId { get; set; }

    [Required(ErrorMessage = "Asset name is required.")]
    [MaxLength(200, ErrorMessage = "Asset name cannot exceed 200 characters.")]
    public string AssetName { get; set; } = null!;

    [MaxLength(30, ErrorMessage = "Asset tag cannot exceed 30 characters.")]
    public string? AssetTag { get; set; }

    public DateOnly IssuedDate { get; set; }

    public DateOnly? ReturnedDate { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "Recovery cannot be negative.")]
    public decimal? RecoveryAmount { get; set; }
}
