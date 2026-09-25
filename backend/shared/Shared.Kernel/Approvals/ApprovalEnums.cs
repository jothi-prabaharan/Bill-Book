namespace Shared.Kernel.Approvals;

// The one approval engine (D-26, TK-99): what can be approved, how an approver
// is found, and where a step and a request stand. Shared by Master, which
// configures and resolves chains, and by every service that stores steps.

/// <summary>What kind of request a chain approves. RetailErp's documents and HRMS's requests alike.</summary>
public enum ApprovalRequestKind
{
    // HRMS and Payroll (the HRMS design).
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
    FullAndFinal = 13,

    // RetailErp (TK-33's design).
    PurchaseOrder = 101,
    PurchaseBill = 102,
    DebitNote = 103,
    SpendMoney = 104,
    ManualJournal = 105,
    CreditNote = 106,
    SalesDiscountOverride = 107,
    CreditLimitOverride = 108,
    StockAdjustment = 109,
}

/// <summary>How a level's approver is found.</summary>
public enum ApproverKind
{
    /// <summary>Walks the employee's managers up <c>ReportingDepth</c> steps. Employee answers.</summary>
    ReportingChain = 1,

    /// <summary>The person named for the employee under a relationship type ("Lead"). Employee answers.</summary>
    Relationship = 2,

    /// <summary>The head of the employee's department. Employee answers.</summary>
    DepartmentHead = 3,

    /// <summary>Anyone holding a role in the branch; the first to act takes the step. Master answers.</summary>
    RoleHolder = 4,

    /// <summary>One fixed employee. Employee answers.</summary>
    NamedEmployee = 5,

    /// <summary>One fixed user, for a customer with no employees (RetailErp). Master answers.</summary>
    NamedUser = 6,
}

/// <summary>Where one step stands. One step is Pending at a time; the rest wait.</summary>
public enum ApprovalStepStatus
{
    Waiting = 1,
    Pending = 2,
    Approved = 3,
    Rejected = 4,
    SentBack = 5,
    Skipped = 6,
    Escalated = 7,

    /// <summary>The request was edited or withdrawn while the step was open; kept as history.</summary>
    Cancelled = 8,
}

/// <summary>The summary a request row keeps for its lists.</summary>
public enum ApprovalStatus
{
    Draft = 1,
    InApproval = 2,
    Approved = 3,
    Rejected = 4,
}
