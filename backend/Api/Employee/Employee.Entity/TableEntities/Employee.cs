using System.ComponentModel.DataAnnotations;
using Employee.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Employee.Entity.TableEntities;

/// <summary>
/// The shared employee master (H1, TK-48): one row per person employed by the
/// branch, used by HRMS, Payroll and School alike. <b>An employee is not a
/// contact</b>: salary, PAN, bank details, family and date of birth are not trade
/// data, and contacts are shown across the sales and purchase screens.
///
/// PAN, Aadhaar and bank numbers are masked on every list (TK-48), and shown in
/// full only to a holder of <c>payroll.view</c> or to the employee.
/// </summary>
public class EmployeeRecord : OrgScopedEntity
{
    public long EmployeeId { get; set; }

    /// <summary>From the <c>EMP</c> numbering series. Unique per branch, never reused.</summary>
    [Required(ErrorMessage = "Employee code is required.")]
    [MaxLength(30, ErrorMessage = "Employee code cannot exceed 30 characters.")]
    public string EmployeeCode { get; set; } = null!;

    [Required(ErrorMessage = "First name is required.")]
    [MaxLength(100, ErrorMessage = "First name cannot exceed 100 characters.")]
    public string FirstName { get; set; } = null!;

    [MaxLength(100, ErrorMessage = "Middle name cannot exceed 100 characters.")]
    public string? MiddleName { get; set; }

    [MaxLength(100, ErrorMessage = "Last name cannot exceed 100 characters.")]
    public string? LastName { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public Gender Gender { get; set; }

    public MaritalStatus MaritalStatus { get; set; }

    [MaxLength(5, ErrorMessage = "Blood group cannot exceed 5 characters.")]
    public string? BloodGroup { get; set; }

    public long DepartmentId { get; set; }

    public long DesignationId { get; set; }

    public long GradeId { get; set; }

    public long WorkLocationId { get; set; }

    public long? CostCentreId { get; set; }

    /// <summary>The manager. No cycles, checked in C#.</summary>
    public long? ReportsToEmployeeId { get; set; }

    public DateOnly JoiningDate { get; set; }

    public DateOnly? ProbationEndDate { get; set; }

    public DateOnly? ConfirmationDate { get; set; }

    [Range(0, 365, ErrorMessage = "Notice period must be between 0 and 365 days.")]
    public int NoticePeriodDays { get; set; }

    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;

    public EmployeeStatus EmployeeStatus { get; set; } = EmployeeStatus.Active;

    /// <summary>Required when the employee has exited.</summary>
    public DateOnly? ExitDate { get; set; }

    /// <summary>The login, for self-service. Unenforced — <c>mst.Users</c>. Unique per branch when set.</summary>
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

    [RegularExpression("^[A-Z]{5}[0-9]{4}[A-Z]$", ErrorMessage = "PAN must be five letters, four digits and a letter.")]
    [MaxLength(10, ErrorMessage = "PAN cannot exceed 10 characters.")]
    public string? Pan { get; set; }

    [RegularExpression("^[0-9]{12}$", ErrorMessage = "Aadhaar must be 12 digits.")]
    [MaxLength(12, ErrorMessage = "Aadhaar cannot exceed 12 characters.")]
    public string? Aadhaar { get; set; }

    /// <summary>PF Universal Account Number.</summary>
    [RegularExpression("^[0-9]{12}$", ErrorMessage = "UAN must be 12 digits.")]
    [MaxLength(12, ErrorMessage = "UAN cannot exceed 12 characters.")]
    public string? Uan { get; set; }

    [MaxLength(30, ErrorMessage = "PF number cannot exceed 30 characters.")]
    public string? PfNumber { get; set; }

    [MaxLength(30, ErrorMessage = "ESI number cannot exceed 30 characters.")]
    public string? EsiNumber { get; set; }

    public bool IsPfApplicable { get; set; } = true;

    public bool IsEsiApplicable { get; set; }

    public bool IsPtApplicable { get; set; } = true;

    public bool IsLwfApplicable { get; set; }

    /// <summary>Unenforced — <c>pay.PayGroups</c>, once Payroll exists.</summary>
    public long? PayGroupId { get; set; }

    /// <summary>A file storage key.</summary>
    [MaxLength(500, ErrorMessage = "Photo key cannot exceed 500 characters.")]
    public string? PhotoAttachmentKey { get; set; }

    public ICollection<EmployeeAddress> Addresses { get; set; } = [];

    public ICollection<EmployeeContact> Contacts { get; set; } = [];

    public ICollection<EmployeeFamilyMember> FamilyMembers { get; set; } = [];

    public ICollection<EmployeeNominee> Nominees { get; set; } = [];

    public ICollection<EmployeeEducation> Education { get; set; } = [];

    public ICollection<PreviousEmployment> PreviousEmployments { get; set; } = [];

    public ICollection<EmployeeBankDetail> BankDetails { get; set; } = [];

    public ICollection<EmployeeDocument> Documents { get; set; } = [];

    public ICollection<AssetIssue> AssetIssues { get; set; } = [];
}
