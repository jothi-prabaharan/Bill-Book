namespace Reporting.Entity.Enums;

/// <summary>
/// Accounting's <c>SubAccountPurpose</c>: a contact's trade balance or one of its two advances. Accounting stores it by name, so these names must stay Accounting's;
/// the numbers are only this copy's (TK-213).
/// </summary>
public enum SubAccountPurpose
{
    Primary = 0,
    PrepaymentAdvance = 1,
    OverpaymentAdvance = 2,
}
