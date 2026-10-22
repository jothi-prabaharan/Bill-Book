namespace Reporting.Entity.Enums;

/// <summary>
/// Accounting's <c>SubAccountReferenceType</c>: what a sub-account is for. Accounting stores it by name, so these names must stay Accounting's;
/// the numbers are only this copy's (TK-213).
/// </summary>
public enum SubAccountReferenceType
{
    Contact = 1,
    Item = 2,
    Tax = 3,
}
