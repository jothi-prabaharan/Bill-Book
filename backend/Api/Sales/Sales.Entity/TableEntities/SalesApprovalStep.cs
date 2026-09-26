using Shared.Kernel.Approvals;

namespace Sales.Entity.TableEntities;

/// <summary>
/// One level of a sales approval chain (TK-102): a credit note, or an
/// override on an invoice or sales order (past the credit limit, past the
/// discount limit), told apart by <see cref="ApprovalStepBase.RequestKind"/>.
/// <see cref="ApprovalStepBase.RequestId"/> is the credit note's id for a
/// credit note and the document's id for an override, which the document
/// type in <see cref="DocumentType"/> completes.
///
/// A submission is a <see cref="Round"/>: an edit mid-chain cancels the
/// round's open steps and they stay as history.
/// </summary>
public class SalesApprovalStep : ApprovalStepBase
{
    public long SalesApprovalStepId { get; set; }

    /// <summary>Which submission of the request this step belongs to, from 1.</summary>
    public int Round { get; set; } = 1;

    /// <summary>
    /// The document an override is on: <c>INV</c> or <c>SOR</c>. An invoice
    /// and an order can share an id, so the kind alone does not name one.
    /// <c>CRN</c> for a credit note.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Document type is required.")]
    [System.ComponentModel.DataAnnotations.MaxLength(3, ErrorMessage = "Document type must be a 3-letter code.")]
    public string DocumentType { get; set; } = null!;
}
