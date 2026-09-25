using Sales.Entity.Enums;

namespace Sales.Entity.Models;

/// <summary>
/// A document's registration at the IRP, as the screens see it (TK-92). A
/// class rather than a record because the registration that runs after the
/// posting commits fills it in before the response is written.
/// </summary>
public sealed class EInvoiceStateView
{
    public long EInvoiceId { get; set; }

    public EInvoiceStatus Status { get; set; }

    public string? Irn { get; set; }

    public string? AckNo { get; set; }

    public DateTimeOffset? AckDate { get; set; }

    public int Attempts { get; set; }

    /// <summary>What went wrong, in words for the operator; null when nothing did.</summary>
    public string? Message { get; set; }

    /// <summary>When the retry worker tries again, while it is still pending.</summary>
    public DateTimeOffset? NextAttemptAt { get; set; }
}

/// <summary>Part B, and what a person asks for when generating an e-way bill (TK-93).</summary>
public sealed class GenerateEwayBillRequest
{
    public TransportMode TransportMode { get; set; } = TransportMode.Road;

    /// <summary>Zero lets the portal work it out from the two PIN codes.</summary>
    [System.ComponentModel.DataAnnotations.Range(0, 4000, ErrorMessage = "Distance must be between 0 and 4000 km.")]
    public int DistanceKm { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(20, ErrorMessage = "Vehicle number cannot exceed 20 characters.")]
    public string? VehicleNo { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(15, ErrorMessage = "Transporter id cannot exceed 15 characters.")]
    public string? TransporterId { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(100, ErrorMessage = "Transporter name cannot exceed 100 characters.")]
    public string? TransporterName { get; set; }
}

/// <summary>A vehicle change on a live e-way bill.</summary>
public sealed class UpdatePartBRequest
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Give the new vehicle number.")]
    [System.ComponentModel.DataAnnotations.MaxLength(20, ErrorMessage = "Vehicle number cannot exceed 20 characters.")]
    public string VehicleNo { get; set; } = null!;

    public TransportMode TransportMode { get; set; } = TransportMode.Road;

    /// <summary>Where the goods are when the vehicle changes.</summary>
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Say where the goods are now.")]
    [System.ComponentModel.DataAnnotations.MaxLength(50, ErrorMessage = "Place cannot exceed 50 characters.")]
    public string FromPlace { get; set; } = null!;

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Give the state the goods are in now.")]
    [System.ComponentModel.DataAnnotations.RegularExpression("^[0-9]{2}$", ErrorMessage = "The state must be a 2-digit state code.")]
    public string FromStateCode { get; set; } = null!;

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Say why the vehicle changed.")]
    [System.ComponentModel.DataAnnotations.MaxLength(50, ErrorMessage = "Reason cannot exceed 50 characters.")]
    public string Reason { get; set; } = null!;
}

public sealed class CancelEwayBillRequest
{
    public EwayBillCancelReason Reason { get; set; } = EwayBillCancelReason.Other;

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Say why the e-way bill is being cancelled.")]
    [System.ComponentModel.DataAnnotations.MaxLength(100, ErrorMessage = "Remark cannot exceed 100 characters.")]
    public string Remark { get; set; } = null!;
}

/// <summary>A document's e-way bill, as the screens see it (TK-93).</summary>
public sealed class EwayBillView
{
    public long EwayBillId { get; set; }

    public EwayBillOrigin Origin { get; set; }

    public EwayBillStatus Status { get; set; }

    public string? EwbNo { get; set; }

    public DateTimeOffset? EwbDate { get; set; }

    public DateTimeOffset? ValidUntil { get; set; }

    public TransportMode TransportMode { get; set; }

    public string? VehicleNo { get; set; }

    public string? TransporterId { get; set; }

    public string? TransporterName { get; set; }

    public int DistanceKm { get; set; }

    public string? Message { get; set; }
}

/// <summary>How an e-way bill action came out.</summary>
public enum EwayBillOutcome
{
    Ok = 0,
    NotFound = 1,

    /// <summary>The document or the branch is not in a state that allows it. <c>Detail</c> says why.</summary>
    Refused = 2,

    /// <summary>The request failed the checks made before anything is sent. <c>Detail</c> lists them.</summary>
    Invalid = 3,
}

public sealed record EwayBillResult(EwayBillOutcome Outcome, string? Detail = null, EwayBillView? EwayBill = null);
