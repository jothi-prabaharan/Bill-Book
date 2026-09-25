using System.ComponentModel.DataAnnotations;
using Employee.Entity.Enums;

namespace Employee.Entity.Models;

/// <summary>
/// Creates or updates an employee with every child table in one request
/// (TK-48). On update the child lists replace what is stored.
///
/// <b>A masked value sent back means unchanged.</b> The detail screen shows PAN,
/// Aadhaar and bank numbers masked to a caller who may not see them, and saving
/// that screen sends them back masked; the service keeps what it holds rather
/// than storing the mask.
/// </summary>
public class SaveEmployeeRequest
{
    [Required(ErrorMessage = "First name is required.")]
    [MaxLength(100, ErrorMessage = "First name cannot exceed 100 characters.")]
    public string FirstName { get; set; } = null!;

    [MaxLength(100, ErrorMessage = "Middle name cannot exceed 100 characters.")]
    public string? MiddleName { get; set; }

    [MaxLength(100, ErrorMessage = "Last name cannot exceed 100 characters.")]
    public string? LastName { get; set; }

    [Required(ErrorMessage = "Date of birth is required.")]
    public DateOnly DateOfBirth { get; set; }

    public Gender Gender { get; set; }

    public MaritalStatus MaritalStatus { get; set; }

    [MaxLength(5, ErrorMessage = "Blood group cannot exceed 5 characters.")]
    public string? BloodGroup { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose a department.")]
    public long DepartmentId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose a designation.")]
    public long DesignationId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose a grade.")]
    public long GradeId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose a work location.")]
    public long WorkLocationId { get; set; }

    public long? CostCentreId { get; set; }

    public long? ReportsToEmployeeId { get; set; }

    [Required(ErrorMessage = "Joining date is required.")]
    public DateOnly JoiningDate { get; set; }

    public DateOnly? ProbationEndDate { get; set; }

    public DateOnly? ConfirmationDate { get; set; }

    /// <summary>Null takes the grade's notice period.</summary>
    [Range(0, 365, ErrorMessage = "Notice period must be between 0 and 365 days.")]
    public int? NoticePeriodDays { get; set; }

    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;

    public EmployeeStatus EmployeeStatus { get; set; } = EmployeeStatus.Active;

    public DateOnly? ExitDate { get; set; }

    /// <summary>The login to link, for self-service. Null unlinks.</summary>
    public Guid? UserId { get; set; }

    [EmailAddress(ErrorMessage = "Work email must be a valid email address.")]
    [MaxLength(255, ErrorMessage = "Work email cannot exceed 255 characters.")]
    public string? WorkEmail { get; set; }

    [EmailAddress(ErrorMessage = "Personal email must be a valid email address.")]
    [MaxLength(255, ErrorMessage = "Personal email cannot exceed 255 characters.")]
    public string? PersonalEmail { get; set; }

    [Required(ErrorMessage = "Phone is required.")]
    [MaxLength(20, ErrorMessage = "Phone cannot exceed 20 characters.")]
    public string Phone { get; set; } = null!;

    /// <summary>Format-checked by the service after a masked value is resolved.</summary>
    [MaxLength(10, ErrorMessage = "PAN cannot exceed 10 characters.")]
    public string? Pan { get; set; }

    [MaxLength(12, ErrorMessage = "Aadhaar cannot exceed 12 characters.")]
    public string? Aadhaar { get; set; }

    [RegularExpression("^[0-9]{12}$", ErrorMessage = "UAN must be 12 digits.")]
    public string? Uan { get; set; }

    [MaxLength(30, ErrorMessage = "PF number cannot exceed 30 characters.")]
    public string? PfNumber { get; set; }

    [MaxLength(30, ErrorMessage = "ESI number cannot exceed 30 characters.")]
    public string? EsiNumber { get; set; }

    public bool IsPfApplicable { get; set; } = true;

    public bool IsEsiApplicable { get; set; }

    public bool IsPtApplicable { get; set; } = true;

    public bool IsLwfApplicable { get; set; }

    /// <summary>
    /// When a change of department, designation, grade, location or manager
    /// takes effect, for the history row it writes. Null means today.
    /// </summary>
    public DateOnly? EffectiveDate { get; set; }

    [MaxLength(500, ErrorMessage = "Remarks cannot exceed 500 characters.")]
    public string? Remarks { get; set; }

    public List<EmployeeAddressModel> Addresses { get; set; } = [];

    public List<EmployeeContactModel> Contacts { get; set; } = [];

    public List<EmployeeFamilyMemberModel> FamilyMembers { get; set; } = [];

    public List<EmployeeNomineeModel> Nominees { get; set; } = [];

    public List<EmployeeEducationModel> Education { get; set; } = [];

    public List<PreviousEmploymentModel> PreviousEmployments { get; set; } = [];

    public List<EmployeeBankDetailModel> BankDetails { get; set; } = [];

    public List<EmployeeDocumentModel> Documents { get; set; } = [];

    public List<AssetIssueModel> AssetIssues { get; set; } = [];
}

public class EmployeeAddressModel
{
    public AddressKind AddressKind { get; set; }

    [Required(ErrorMessage = "Address line 1 is required.")]
    [MaxLength(200, ErrorMessage = "Address line 1 cannot exceed 200 characters.")]
    public string AddressLine1 { get; set; } = null!;

    [MaxLength(200, ErrorMessage = "Address line 2 cannot exceed 200 characters.")]
    public string? AddressLine2 { get; set; }

    [MaxLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string? City { get; set; }

    public int? StateId { get; set; }

    [MaxLength(10, ErrorMessage = "Postal code cannot exceed 10 characters.")]
    public string? PostalCode { get; set; }
}

public class EmployeeContactModel
{
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string Name { get; set; } = null!;

    public Relationship Relationship { get; set; }

    [Required(ErrorMessage = "Phone is required.")]
    [MaxLength(20, ErrorMessage = "Phone cannot exceed 20 characters.")]
    public string Phone { get; set; } = null!;

    public bool IsPrimary { get; set; }
}

public class EmployeeFamilyMemberModel
{
    /// <summary>Set on the detail view; ignored on save, where the list replaces what is stored.</summary>
    public long? EmployeeFamilyMemberId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string Name { get; set; } = null!;

    public Relationship Relationship { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public bool IsDependent { get; set; }

    public bool IsEsiCovered { get; set; }
}

/// <summary>A nomination. The family member is named by its position in the request's family list.</summary>
public class EmployeeNomineeModel
{
    [Range(0, 100, ErrorMessage = "Choose the family member being nominated.")]
    public int FamilyMemberIndex { get; set; }

    public NominationKind NominationKind { get; set; }

    [Range(typeof(decimal), "0.01", "100", ErrorMessage = "A share must be more than 0 and at most 100 percent.")]
    public decimal SharePercent { get; set; }
}

public class EmployeeEducationModel
{
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

public class PreviousEmploymentModel
{
    [Required(ErrorMessage = "Employer is required.")]
    [MaxLength(200, ErrorMessage = "Employer cannot exceed 200 characters.")]
    public string Employer { get; set; } = null!;

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    [MaxLength(100, ErrorMessage = "Last designation cannot exceed 100 characters.")]
    public string? LastDesignation { get; set; }
}

public class EmployeeBankDetailModel
{
    /// <summary>The stored row this edits, so a masked account number sent back keeps its value.</summary>
    public long? EmployeeBankDetailId { get; set; }

    [Required(ErrorMessage = "Account holder is required.")]
    [MaxLength(200, ErrorMessage = "Account holder cannot exceed 200 characters.")]
    public string AccountHolder { get; set; } = null!;

    [Required(ErrorMessage = "Account number is required.")]
    [MaxLength(30, ErrorMessage = "Account number cannot exceed 30 characters.")]
    public string AccountNo { get; set; } = null!;

    [Required(ErrorMessage = "IFSC is required.")]
    [RegularExpression("^[A-Z]{4}0[A-Z0-9]{6}$", ErrorMessage = "IFSC must be four letters, a zero and six letters or digits.")]
    public string Ifsc { get; set; } = null!;

    [Required(ErrorMessage = "Bank name is required.")]
    [MaxLength(100, ErrorMessage = "Bank name cannot exceed 100 characters.")]
    public string BankName { get; set; } = null!;

    public bool IsPrimary { get; set; }
}

public class EmployeeDocumentModel
{
    public EmployeeDocumentKind DocumentKind { get; set; }

    [Required(ErrorMessage = "The file is required.")]
    [MaxLength(500, ErrorMessage = "File key cannot exceed 500 characters.")]
    public string AttachmentKey { get; set; } = null!;

    public DateOnly? ValidUntil { get; set; }
}

public class AssetIssueModel
{
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

/// <summary>An employee on the list. PAN and Aadhaar are always masked here.</summary>
public class EmployeeListItem
{
    public long EmployeeId { get; set; }

    public string EmployeeCode { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string DepartmentName { get; set; } = null!;

    public string DesignationName { get; set; } = null!;

    public string WorkLocationName { get; set; } = null!;

    public DateOnly JoiningDate { get; set; }

    public string EmployeeStatus { get; set; } = null!;

    public string EmploymentType { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string? MaskedPan { get; set; }

    public string? MaskedAadhaar { get; set; }

    /// <summary>Whether a login is linked, never which one.</summary>
    public bool HasLogin { get; set; }
}

public class EmployeeListPage
{
    public IReadOnlyList<EmployeeListItem> Items { get; set; } = [];

    public int Total { get; set; }
}

/// <summary>
/// An employee with every child table and the employment history. Sensitive
/// numbers are in full only when <see cref="SensitiveShown"/> is true.
/// </summary>
public class EmployeeDetail : SaveEmployeeRequest
{
    public long EmployeeId { get; set; }

    public string EmployeeCode { get; set; } = null!;

    /// <summary>True when the caller holds <c>payroll.view</c> or is this employee.</summary>
    public bool SensitiveShown { get; set; }

    public List<EmploymentHistoryRow> History { get; set; } = [];
}

public class EmploymentHistoryRow
{
    public DateOnly EffectiveDate { get; set; }

    public string ChangeKind { get; set; } = null!;

    public long DepartmentId { get; set; }

    public long DesignationId { get; set; }

    public long GradeId { get; set; }

    public long WorkLocationId { get; set; }

    public long? ReportsToEmployeeId { get; set; }

    public string? Remarks { get; set; }
}

public enum EmployeeOutcome
{
    Ok = 1,
    NotFound = 2,

    /// <summary>A department, designation, grade, location, cost centre or manager not in this branch.</summary>
    InvalidReference = 3,

    /// <summary>The manager chain would come back to this employee.</summary>
    ManagerCycle = 4,

    /// <summary>Younger than 14 on the joining date.</summary>
    TooYoung = 5,

    /// <summary>The nominee shares for one kind do not add up to 100.</summary>
    NomineeShares = 6,

    /// <summary>Bank accounts given but not exactly one primary.</summary>
    PrimaryBank = 7,

    /// <summary>Two addresses of one kind.</summary>
    DuplicateAddress = 8,

    /// <summary>The login is already linked to another employee of this branch.</summary>
    UserAlreadyLinked = 9,

    /// <summary>Exited without an exit date.</summary>
    ExitDateRequired = 10,

    /// <summary>A nominee names a family member that is not in the request.</summary>
    NomineeFamilyMember = 11,

    /// <summary>A PAN or Aadhaar in the wrong format.</summary>
    InvalidIdentityNumber = 12,
}

public sealed record EmployeeResult(EmployeeOutcome Outcome, long EmployeeId = 0, string? EmployeeCode = null);
