namespace Reporting.Entity.Enums;

/// <summary>
/// Sales' <c>ChallanType</c>: why goods left on a delivery challan. Sales stores it by name, so these names must stay Sales';
/// the numbers are only this copy's (TK-213).
/// </summary>
public enum ChallanType
{
    Sale = 0,
    JobWork = 1,
    Approval = 2,
    BranchTransfer = 3,
}
