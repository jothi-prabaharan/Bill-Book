using System.ComponentModel.DataAnnotations;
using Sales.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Sales.Entity.TableEntities;

/// <summary>
/// One document's registration at the Invoice Registration Portal (TK-91).
///
/// Its own row rather than columns on the invoice: it has a life of its own
/// (pending, registered, failed, cancelled) and a retry history, and columns
/// would be null on every B2C sale. <see cref="SourceType"/> and
/// <see cref="SourceId"/> are unique together: one e-invoice per document, ever,
/// because the IRP never registers the same number twice.
/// </summary>
public class EInvoice : OrgScopedEntity
{
    public long EInvoiceId { get; set; }

    public EInvoiceSource SourceType { get; set; }

    public long SourceId { get; set; }

    public EInvoiceStatus Status { get; set; } = EInvoiceStatus.Pending;

    /// <summary>The 64-character hash the IRP returns. Unique where set.</summary>
    [MaxLength(64, ErrorMessage = "IRN cannot exceed 64 characters.")]
    public string? Irn { get; set; }

    [MaxLength(20, ErrorMessage = "Acknowledgement number cannot exceed 20 characters.")]
    public string? AckNo { get; set; }

    /// <summary>The 24-hour cancel window runs from here.</summary>
    public DateTimeOffset? AckDate { get; set; }

    /// <summary>The signed QR payload, printed as a QR image.</summary>
    public string? SignedQrCode { get; set; }

    /// <summary>The signed invoice as the IRP returned it. Kept for audit, never re-signed.</summary>
    public string? SignedInvoice { get; set; }

    /// <summary>Every call to the IRP adds one.</summary>
    public int Attempts { get; set; }

    /// <summary>The IRP's own code, e.g. <c>2150</c> for a duplicate, or a local validation code.</summary>
    [MaxLength(20, ErrorMessage = "Error code cannot exceed 20 characters.")]
    public string? LastErrorCode { get; set; }

    /// <summary>For the operator; never shown to a buyer.</summary>
    [MaxLength(1000, ErrorMessage = "Error message cannot exceed 1000 characters.")]
    public string? LastErrorMessage { get; set; }

    /// <summary>When the retry worker may try again.</summary>
    public DateTimeOffset? NextAttemptAt { get; set; }

    public EInvoiceCancelReason? CancelReason { get; set; }

    [MaxLength(100, ErrorMessage = "Cancel remark cannot exceed 100 characters.")]
    public string? CancelRemark { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
}

/// <summary>
/// An e-way bill for goods leaving on an invoice, a delivery challan or a return
/// (TK-91). Beside the e-invoice, so one place records what the portal said; a
/// bill made outside the product is recorded with <see cref="Origin"/> Manual.
/// </summary>
public class EwayBill : OrgScopedEntity
{
    public long EwayBillId { get; set; }

    public EwayBillSource SourceType { get; set; }

    public long SourceId { get; set; }

    public EwayBillOrigin Origin { get; set; } = EwayBillOrigin.Standalone;

    public EwayBillStatus Status { get; set; } = EwayBillStatus.Pending;

    [MaxLength(12, ErrorMessage = "E-way bill number cannot exceed 12 characters.")]
    public string? EwbNo { get; set; }

    public DateTimeOffset? EwbDate { get; set; }

    public DateTimeOffset? ValidUntil { get; set; }

    public TransportMode TransportMode { get; set; } = TransportMode.Road;

    /// <summary>Part B. May follow later, when the vehicle is known.</summary>
    [MaxLength(20, ErrorMessage = "Vehicle number cannot exceed 20 characters.")]
    public string? VehicleNo { get; set; }

    /// <summary>The transporter's GSTIN or enrolment id.</summary>
    [MaxLength(15, ErrorMessage = "Transporter id cannot exceed 15 characters.")]
    public string? TransporterId { get; set; }

    [MaxLength(100, ErrorMessage = "Transporter name cannot exceed 100 characters.")]
    public string? TransporterName { get; set; }

    /// <summary>As sent. The portal can work it out from the two PIN codes when this is zero.</summary>
    [Range(0, 4000, ErrorMessage = "Distance must be between 0 and 4000 km.")]
    public int DistanceKm { get; set; }

    public int Attempts { get; set; }

    [MaxLength(20, ErrorMessage = "Error code cannot exceed 20 characters.")]
    public string? LastErrorCode { get; set; }

    [MaxLength(1000, ErrorMessage = "Error message cannot exceed 1000 characters.")]
    public string? LastErrorMessage { get; set; }

    public DateTimeOffset? NextAttemptAt { get; set; }

    public EwayBillCancelReason? CancelReason { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
}
