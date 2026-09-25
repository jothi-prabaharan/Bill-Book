namespace TimeLeave.Entity.Enums;

public enum WeeklyOffKind
{
    Working = 1,
    Off = 2,
    AlternateOff = 3,
    HalfDay = 4
}

public enum PunchSource
{
    Biometric = 1,
    Mobile = 2,
    Web = 3,
    Import = 4
}

public enum AttendanceStatus
{
    Present = 1,
    Absent = 2,
    HalfDay = 3,
    OnLeave = 4,
    Holiday = 5,
    WeeklyOff = 6,
    OnDuty = 7,
    CompOff = 8
}

public enum AttendanceSource
{
    Derived = 1,
    Manual = 2,
    Regularised = 3
}

public enum LeaveHalf
{
    Full = 1,
    FirstHalf = 2,
    SecondHalf = 3
}

public enum AccrualKind
{
    Upfront = 1,
    Monthly = 2,
    Quarterly = 3
}

public enum CarryForwardKind
{
    Lapse = 1,
    CarryForward = 2,
    Encash = 3
}

public enum LeaveStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Cancelled = 5
}

public enum EncashmentStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Paid = 5
}

public enum ApprovalStatus
{
    Draft = 1,
    InApproval = 2,
    Approved = 3,
    Rejected = 4
}

public enum ApprovalStepStatus
{
    Waiting = 1,
    Pending = 2,
    Approved = 3,
    Rejected = 4,
    SentBack = 5,
    Skipped = 6,
    Escalated = 7
}

public enum RequestKind
{
    Leave = 1,
    Regularisation = 2,
    Overtime = 3,
    CompOff = 4,
    LeaveEncashment = 5,
    Appraisal = 6,
    SalaryRevision = 7,
    Loan = 8,
    JobRequisition = 9,
    Offer = 10,
    Claim = 11,
    Separation = 12,
    FullAndFinal = 13
}

public enum ApproverKind
{
    ReportingChain = 1,
    Relationship = 2,
    DepartmentHead = 3,
    RoleHolder = 4,
    NamedEmployee = 5
}

public enum Gender
{
    Male = 1,
    Female = 2,
    Other = 3,
    NotStated = 4
}
