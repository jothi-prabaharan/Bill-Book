namespace Performance.Entity.Enums;

public enum CycleKind
{
    Annual = 1,
    HalfYearly = 2,
    Quarterly = 3,
    Probation = 4,
}

public enum CycleStatus
{
    Draft = 1,
    GoalSetting = 2,
    SelfEvaluation = 3,
    InReview = 4,
    Calibration = 5,
    Released = 6,
    Closed = 7,
}

public enum ReviewStatus
{
    NotStarted = 1,
    SelfEvaluationDraft = 2,
    SelfEvaluationSubmitted = 3,
    InApproval = 4,
    SentBack = 5,
    Calibration = 6,
    Released = 7,
    Acknowledged = 8,
    Closed = 9,
}

public enum GoalStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4,
}

public enum LevelDecision
{
    Approved = 1,
    SentBack = 2,
    Rejected = 3,
}
