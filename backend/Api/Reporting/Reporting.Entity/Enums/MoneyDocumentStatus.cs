namespace Reporting.Entity.Enums;

/// <summary>
/// A spend money, receive money or transfer document's status, as Accounting
/// stores it: by name, so these names must stay Accounting's (TK-202).
/// </summary>
public enum MoneyDocumentStatus
{
    Draft = 0,
    Posted = 1,
    Void = 2,
}
