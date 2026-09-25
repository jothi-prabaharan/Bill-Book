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
