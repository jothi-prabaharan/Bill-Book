namespace Recruitment.Entity.Enums;

public enum EmploymentType
{
    Permanent = 1,
    Probation = 2,
    Contract = 3,
    PartTime = 4,
    Intern = 5,
    Consultant = 6,
}

public enum OpeningStatus
{
    Draft = 1,
    Open = 2,
    OnHold = 3,
    Closed = 4,
    Filled = 5,
}

public enum CandidateSource
{
    Portal = 1,
    Referral = 2,
    Agency = 3,
    CareersPage = 4,
    WalkIn = 5,
}

public enum ApplicationStage
{
    Applied = 1,
    Screening = 2,
    Interview = 3,
    Offer = 4,
    Hired = 5,
    Rejected = 6,
    Withdrawn = 7,
}

public enum RoundKind
{
    Telephonic = 1,
    Technical = 2,
    Hr = 3,
    Managerial = 4,
}

public enum InterviewOutcome
{
    Pending = 1,
    Pass = 2,
    Fail = 3,
    NoShow = 4,
}

public enum OfferStatus
{
    Draft = 1,
    Approved = 2,
    Sent = 3,
    Accepted = 4,
    Declined = 5,
    Revoked = 6,
}

public enum ApprovalStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
}
