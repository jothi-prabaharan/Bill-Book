namespace Payroll.Entity.Enums;

public enum FnfStatus
{
    Draft = 1,
    Approved = 2,
    Posted = 3,
    Paid = 4,
}

public enum FnfLineKind
{
    SalaryToLwd = 1,
    LeaveEncashment = 2,
    Gratuity = 3,
    Bonus = 4,
    Arrears = 5,
    Reimbursement = 6,
    NoticeRecovery = 7,
    LoanRecovery = 8,
    AssetRecovery = 9,
    Tds = 10,
    Other = 11,
}
