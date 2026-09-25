namespace Employee.Entity.Enums;

// The fixed sets of the employee master (H1, TK-48), from the HRMS design's
// Columns section. Stored by name.

public enum Gender
{
    NotStated = 0,
    Male = 1,
    Female = 2,
    Other = 3,
}

public enum MaritalStatus
{
    NotStated = 0,
    Single = 1,
    Married = 2,
    Widowed = 3,
    Divorced = 4,
}

public enum EmploymentType
{
    Permanent = 1,
    Probation = 2,
    Contract = 3,
    PartTime = 4,
    Intern = 5,
    Consultant = 6,
}

/// <summary>Onboarding → Active → OnNotice → Exited. Exited needs an exit date.</summary>
public enum EmployeeStatus
{
    Onboarding = 1,
    Active = 2,
    OnNotice = 3,
    Exited = 4,
}

public enum AddressKind
{
    Current = 1,
    Permanent = 2,
}

/// <summary>How a family member or an emergency contact is related to the employee.</summary>
public enum Relationship
{
    Spouse = 1,
    Child = 2,
    Father = 3,
    Mother = 4,
    Sibling = 5,
    Friend = 6,
    Other = 7,
}

/// <summary>What a nomination is for. The shares for one kind add up to 100.</summary>
public enum NominationKind
{
    Pf = 1,
    Gratuity = 2,
    Insurance = 3,
}

/// <summary>Why an employment history row was written. Appended, never updated.</summary>
public enum EmploymentChangeKind
{
    Joined = 1,
    Confirmed = 2,
    Promotion = 3,
    Transfer = 4,
    Redesignation = 5,
    GradeChange = 6,
    ManagerChange = 7,
    Exit = 8,
}

public enum EmployeeDocumentKind
{
    Pan = 1,
    Aadhaar = 2,
    Passport = 3,
    Resume = 4,
    OfferLetter = 5,
    Certificate = 6,
    Other = 7,
}

/// <summary>Who an announcement is for; the id it names is in <c>AudienceRefId</c>.</summary>
public enum AnnouncementAudience
{
    Everyone = 1,
    Department = 2,
    Location = 3,
    Grade = 4,
}

public enum ChecklistKind
{
    Onboarding = 1,
    Exit = 2,
}

public enum ChecklistOwnerRole
{
    Hr = 1,
    Manager = 2,
    It = 3,
    Finance = 4,
    Admin = 5,
}

public enum SeparationKind
{
    Resignation = 1,
    Termination = 2,
    Retirement = 3,
    Death = 4,
    EndOfContract = 5,
    Absconding = 6,
}

public enum SeparationStatus
{
    Submitted = 1,
    Approved = 2,
    ClearancePending = 3,
    Cleared = 4,
    Settled = 5,
    Withdrawn = 6,
}
