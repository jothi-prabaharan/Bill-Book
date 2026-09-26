using Shared.Kernel.Approvals;

namespace Sales.Entity.TableEntities;

/// <summary>
/// The two overrides a sale can ask for (TK-102): past the customer's credit
/// limit, and past the line discount limit (D-29). Each is its own approval
/// chain in <c>sal.ApprovalSteps</c>, keyed to this one document, and its
/// status is kept here. Null when the document never needed one.
/// </summary>
public interface ISalesOverrides
{
    ApprovalStatus? CreditOverrideStatus { get; set; }

    ApprovalStatus? DiscountOverrideStatus { get; set; }
}
