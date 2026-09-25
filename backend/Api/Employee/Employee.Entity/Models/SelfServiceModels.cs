using System.ComponentModel.DataAnnotations;
using Employee.Entity.Enums;

namespace Employee.Entity.Models;

public class MyProfileDto
{
    public long EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string FullName { get; set; } = null!;
    public DateOnly DateOfBirth { get; set; }
    public string Gender { get; set; } = null!;
    public string MaritalStatus { get; set; } = null!;
    public string? BloodGroup { get; set; }
    public string? Phone { get; set; }
    public string? WorkEmail { get; set; }
    public string? PersonalEmail { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? Pan { get; set; }
    public string? Aadhaar { get; set; }
    public string? Uan { get; set; }
    public long DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public long DesignationId { get; set; }
    public string? DesignationName { get; set; }
    public long GradeId { get; set; }
    public string? GradeName { get; set; }
    public long WorkLocationId { get; set; }
    public string? WorkLocationName { get; set; }
    public long? ReportsToEmployeeId { get; set; }
    public string? ReportsToName { get; set; }
    public DateOnly JoiningDate { get; set; }
    public DateOnly? ConfirmationDate { get; set; }
    public string EmploymentType { get; set; } = null!;
    public string EmployeeStatus { get; set; } = null!;
    public List<EmployeeAddressModel> Addresses { get; set; } = [];
    public List<EmployeeContactModel> Contacts { get; set; } = [];
    public List<EmployeeFamilyMemberModel> FamilyMembers { get; set; } = [];
    public List<EmployeeEducationModel> Education { get; set; } = [];
    public List<PreviousEmploymentModel> PreviousEmployments { get; set; } = [];
    public List<EmployeeBankDetailModel> BankDetails { get; set; } = [];
    public List<EmployeeDocumentModel> Documents { get; set; } = [];
}

public class UpdateMyProfileRequest
{
    [MaxLength(20, ErrorMessage = "Phone cannot exceed 20 characters.")]
    public string? Phone { get; set; }

    [MaxLength(100, ErrorMessage = "Personal email cannot exceed 100 characters.")]
    [EmailAddress(ErrorMessage = "Enter a valid personal email.")]
    public string? PersonalEmail { get; set; }

    [MaxLength(5, ErrorMessage = "Blood group cannot exceed 5 characters.")]
    public string? BloodGroup { get; set; }

    public MaritalStatus? MaritalStatus { get; set; }

    [MaxLength(100, ErrorMessage = "Emergency contact name cannot exceed 100 characters.")]
    public string? EmergencyContactName { get; set; }

    [MaxLength(20, ErrorMessage = "Emergency contact phone cannot exceed 20 characters.")]
    public string? EmergencyContactPhone { get; set; }
}

public class TeamMemberDto
{
    public long EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? DepartmentName { get; set; }
    public string? DesignationName { get; set; }
    public string? WorkLocationName { get; set; }
    public string? Phone { get; set; }
    public string? WorkEmail { get; set; }
    public DateOnly JoiningDate { get; set; }
    public int Level { get; set; } // 1 = Direct report, 2+ = Indirect report
    public long? ReportsToEmployeeId { get; set; }
    public string? ReportsToName { get; set; }
    public string Status { get; set; } = null!;
}

public class TeamSummaryDto
{
    public int TotalMembers { get; set; }
    public int DirectReports { get; set; }
    public int IndirectReports { get; set; }
}
