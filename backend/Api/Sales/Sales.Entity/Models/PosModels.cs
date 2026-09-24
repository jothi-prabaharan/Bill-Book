using System.ComponentModel.DataAnnotations;
using Sales.Entity.Enums;

namespace Sales.Entity.Models;

/// <summary>
/// A till sale, made and posted in one call (TK-39): the lines, and how it was
/// paid. The till sends the walk-in customer (TK-17) unless a customer was
/// chosen.
/// </summary>
public class PosSaleRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose the till.")]
    public long TillId { get; set; }

    /// <summary>Today, in the till's calendar, when not sent.</summary>
    public DateOnly? DocumentDate { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose the customer.")]
    public long ContactId { get; set; }

    [MaxLength(15, ErrorMessage = "GSTIN must be 15 characters.")]
    public string? ContactGstin { get; set; }

    [MaxLength(2, ErrorMessage = "Place of supply must be a 2-digit state code.")]
    public string? PlaceOfSupplyStateCode { get; set; }

    public string? Notes { get; set; }

    [MinLength(1, ErrorMessage = "A sale needs at least one line.")]
    public List<SaveInvoiceLineRequest> Lines { get; set; } = [];

    [MinLength(1, ErrorMessage = "A sale needs at least one tender.")]
    public List<PosTenderRequest> Tenders { get; set; } = [];
}

public class PosTenderRequest
{
    [EnumDataType(typeof(PosTenderMode), ErrorMessage = "A tender is cash, card or UPI.")]
    public PosTenderMode Mode { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "A tender amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose the account the money went into.")]
    public long BankAccountId { get; set; }

    [MaxLength(50, ErrorMessage = "Reference cannot exceed 50 characters.")]
    public string? Reference { get; set; }
}

/// <summary>A posted till sale: its number, total and the change to hand back.</summary>
public sealed record PosSaleResult(
    PosSaleOutcome Outcome,
    long InvoiceId = 0,
    string? DocumentNo = null,
    decimal TotalAmount = 0,
    decimal ChangeAmount = 0,
    string? Detail = null,
    InvoiceOutcome? Refusal = null);

/// <summary>Why a till sale was refused. The invoice outcomes pass through; two are the till's own.</summary>
public enum PosSaleOutcome
{
    Ok = 0,

    /// <summary>The tenders add up to less than the total.</summary>
    TenderShort = 1,

    /// <summary>A card or UPI tender is more than what was left to pay. Only cash gives change.</summary>
    TenderOverpaid = 2,

    /// <summary>The invoice itself was refused; <c>InvoiceOutcome</c> says why.</summary>
    InvoiceRefused = 3,
}
