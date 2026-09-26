using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// One online payment a contact started in the client portal (TK-98, design
/// "Client portal" → Online payment).
///
/// The money is recorded only on the gateway's verified callback, never on the
/// browser's return, and then as an ordinary Receive Money allocated to the
/// invoices the payer chose. <see cref="GatewayPaymentId"/> is unique, and the
/// move out of <see cref="OnlinePaymentStatus.Created"/> is guarded, so however
/// many callbacks arrive, one receipt is made.
/// </summary>
public class OnlinePayment : OrgScopedEntity
{
    public long OnlinePaymentId { get; set; }

    /// <summary>
    /// <c>op_{customer}_{org}_{id}</c>: what the gateway carries back, so an
    /// anonymous callback can name its tenant before a database is opened.
    /// </summary>
    [Required(ErrorMessage = "The reference is required.")]
    [MaxLength(100, ErrorMessage = "The reference cannot exceed 100 characters.")]
    public string Reference { get; set; } = null!;

    /// <summary>The paying contact, from the portal token only.</summary>
    public long ContactId { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "The amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "The currency is required.")]
    [MaxLength(3, ErrorMessage = "The currency must be a 3-letter code.")]
    public string CurrencyCode { get; set; } = null!;

    /// <summary>The invoices the payer chose and how much of each, as JSON; applied when the receipt is made.</summary>
    [Required(ErrorMessage = "The allocations are required.")]
    public string Allocations { get; set; } = "[]";

    /// <summary>The bank account the money lands in: the branch's online payment account when the payment began.</summary>
    public long BankAccountId { get; set; }

    public PaymentGatewayKind Gateway { get; set; } = PaymentGatewayKind.Sandbox;

    [MaxLength(64, ErrorMessage = "The gateway order id cannot exceed 64 characters.")]
    public string? GatewayOrderId { get; set; }

    /// <summary>The gateway's id for the payment: the idempotency key for its callback.</summary>
    [MaxLength(64, ErrorMessage = "The gateway payment id cannot exceed 64 characters.")]
    public string? GatewayPaymentId { get; set; }

    public OnlinePaymentStatus Status { get; set; } = OnlinePaymentStatus.Created;

    /// <summary>The receipt this became.</summary>
    public long? ReceiveMoneyId { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    /// <summary>
    /// Why the money was received but not allocated as chosen: it went in as an
    /// advance instead, or — when even that was refused — no receipt was made
    /// and staff must record it. Null when all went as asked.
    /// </summary>
    [MaxLength(500, ErrorMessage = "The note cannot exceed 500 characters.")]
    public string? Note { get; set; }
}
