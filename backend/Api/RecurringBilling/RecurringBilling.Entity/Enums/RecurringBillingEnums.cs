namespace RecurringBilling.Entity.Enums;

public enum ScheduleDocumentType
{
    SalesInvoice = 1,
    PurchaseBill = 2,
    RetainerInvoice = 3
}

public enum ScheduleInterval
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Yearly = 4
}

public enum ScheduleStatus
{
    Active = 1,
    Paused = 2,
    Ended = 3
}

public enum ScheduleRunStatus
{
    Pending = 1,
    InProgress = 2
}
