using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;

namespace Accounting.Entity.Models;

/// <summary>A TDS section row as the settings screen shows it (TK-117).</summary>
public sealed class TdsSectionView
{
    public long TdsSectionId { get; set; }

    public string SectionCode { get; set; } = null!;

    public string NatureOfPayment { get; set; } = null!;

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public decimal RateIndividual { get; set; }

    public decimal RateOther { get; set; }

    public decimal RateNoPan { get; set; }

    public decimal? SingleThreshold { get; set; }

    public decimal? AggregateThreshold { get; set; }

    public string ThresholdBasis { get; set; } = null!;

    public bool IsActive { get; set; }

    /// <summary>Whether any register row names this code; its code is then fixed.</summary>
    public bool IsUsed { get; set; }
}

public sealed class SaveTdsSectionRequest
{
    [Required(ErrorMessage = "Section code is required.")]
    [MaxLength(10, ErrorMessage = "Section code cannot exceed 10 characters.")]
    public string SectionCode { get; set; } = null!;

    [Required(ErrorMessage = "Nature of payment is required.")]
    [MaxLength(200, ErrorMessage = "Nature of payment cannot exceed 200 characters.")]
    public string NatureOfPayment { get; set; } = null!;

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    [Range(0, 100, ErrorMessage = "Rate must be between 0 and 100 percent.")]
    public decimal RateIndividual { get; set; }

    [Range(0, 100, ErrorMessage = "Rate must be between 0 and 100 percent.")]
    public decimal RateOther { get; set; }

    [Range(0, 100, ErrorMessage = "Rate must be between 0 and 100 percent.")]
    public decimal RateNoPan { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "Threshold cannot be negative.")]
    public decimal? SingleThreshold { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "Threshold cannot be negative.")]
    public decimal? AggregateThreshold { get; set; }

    public TdsThresholdBasis ThresholdBasis { get; set; } = TdsThresholdBasis.WholeAmount;

    public bool IsActive { get; set; } = true;
}

public enum SaveTdsSectionOutcome
{
    Ok = 0,
    NotFound = 1,

    /// <summary>Another row of the same code starts on the same date.</summary>
    Duplicate = 2,

    /// <summary>The period ends before it starts, or overlaps another row of the code.</summary>
    BadPeriod = 3,

    /// <summary>The code of a row the register uses cannot change.</summary>
    CodeInUse = 4,
}

public sealed record SaveTdsSectionResult(SaveTdsSectionOutcome Outcome, long TdsSectionId = 0);

/// <summary>A register row for the register screen and the document screens.</summary>
public sealed class TdsEntryView
{
    public long TdsEntryId { get; set; }

    public string Direction { get; set; } = null!;

    public string Kind { get; set; } = null!;

    public string TransactionTypeCode { get; set; } = null!;

    public long TransactionId { get; set; }

    public string? DocumentNo { get; set; }

    public DateOnly DocumentDate { get; set; }

    public int FinancialYear { get; set; }

    public long ContactId { get; set; }

    public string? ContactName { get; set; }

    public string? SectionCode { get; set; }

    public string? Pan { get; set; }

    public string? Tan { get; set; }

    public decimal Amount { get; set; }

    public decimal TdsBase { get; set; }

    public decimal Rate { get; set; }

    public string? CertificateNo { get; set; }

    public decimal TdsAmount { get; set; }

    public long? ChallanSpendMoneyId { get; set; }

    public string? ChallanNo { get; set; }

    public string? Note { get; set; }
}

/// <summary>What is still undeposited for one section and month: what a challan has to pay.</summary>
public sealed class TdsOutstandingView
{
    public string SectionCode { get; set; } = null!;

    public DateOnly Month { get; set; }

    public decimal TdsAmount { get; set; }

    public int Deductions { get; set; }

    /// <summary>The 7th of the following month (30 April for March), when it falls due.</summary>
    public DateOnly DueDate { get; set; }
}

/// <summary>A Form 26AS import as the list shows it.</summary>
public class Form26asImportSummary
{
    public long Form26asImportId { get; set; }

    public int FinancialYear { get; set; }

    public string? Pan { get; set; }

    public string FileName { get; set; } = null!;

    public DateTimeOffset? ImportedAt { get; set; }

    public int MatchedCount { get; set; }

    public int DifferenceCount { get; set; }

    public int MissingInBooksCount { get; set; }

    public int MissingIn26asCount { get; set; }

    public decimal CreditAtRisk { get; set; }
}

public sealed class Form26asImportDetail : Form26asImportSummary
{
    public List<Form26asLineView> Lines { get; set; } = [];
}

public sealed class Form26asLineView
{
    public long Form26asLineId { get; set; }

    public string MatchState { get; set; } = null!;

    public string DeductorTan { get; set; } = null!;

    public string? DeductorName { get; set; }

    public string? SectionCode { get; set; }

    public DateOnly? TransactionDate { get; set; }

    public decimal? StatementAmountPaid { get; set; }

    public decimal? StatementTds { get; set; }

    public decimal? BooksTds { get; set; }

    public long? ReceiveMoneyId { get; set; }

    public string? DocumentNo { get; set; }

    public string? Note { get; set; }
}

public enum Form26asImportOutcome
{
    Ok = 0,

    /// <summary>Not a 26AS text file; the detail says why.</summary>
    Unreadable = 1,

    /// <summary>The statement is for a PAN other than the branch's.</summary>
    WrongPan = 2,
}

public sealed record Form26asImportResult(Form26asImportOutcome Outcome, long Form26asImportId = 0, string? Detail = null);
