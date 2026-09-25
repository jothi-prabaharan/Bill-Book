namespace Claims.Entity.Enums;

public enum ClaimStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Paid = 5
}

public enum PayoutMode
{
    Payroll = 1,
    Direct = 2
}

public enum LimitPeriod
{
    PerClaim = 1,
    Monthly = 2,
    Yearly = 3
}

public enum ApprovalStatus
{
    Draft = 1,
    InApproval = 2,
    Approved = 3,
    Rejected = 4,
    SentBack = 5
}

public enum ApprovalStepStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    SentBack = 4,
    Skipped = 5
}

public enum ApproverKind
{
    ReportingChain = 1,
    SpecificRole = 2,
    SpecificEmployee = 3,
    Relationship = 4
}

public enum ClaimRequestKind
{
    Claim = 1
}
