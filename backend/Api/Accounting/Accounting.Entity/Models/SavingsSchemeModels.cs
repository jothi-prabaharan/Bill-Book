using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;

namespace Accounting.Entity.Models;

/// <summary>A savings scheme as the screen sends it (TK-141).</summary>
public class SaveSavingsSchemeRequest
{
    [Required(ErrorMessage = "Give the scheme a code.")]
    [MaxLength(20, ErrorMessage = "The code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name the scheme.")]
    [MaxLength(100, ErrorMessage = "The name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [Range(typeof(decimal), "1", "10000000", ErrorMessage = "The instalment must be between 1 and 1,00,00,000.")]
    public decimal InstalmentAmount { get; set; }

    [Range(1, 60, ErrorMessage = "A scheme runs 1 to 60 months.")]
    public int Months { get; set; } = 11;

    [EnumDataType(typeof(SchemeBonusType), ErrorMessage = "Choose how the bonus is worked out.")]
    public SchemeBonusType BonusType { get; set; } = SchemeBonusType.Instalments;

    [Range(typeof(decimal), "0", "10000000", ErrorMessage = "The bonus cannot be negative.")]
    public decimal BonusValue { get; set; } = 1m;

    [Range(0, 365, ErrorMessage = "Grace days must be between 0 and 365.")]
    public int GraceDays { get; set; } = 30;

    public bool IsActive { get; set; } = true;
}

public class SavingsSchemeView
{
    public long SavingsSchemeId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal InstalmentAmount { get; set; }
    public int Months { get; set; }
    public string BonusType { get; set; } = null!;
    public decimal BonusValue { get; set; }

    /// <summary>What the rule pays on this scheme's own instalment and months.</summary>
    public decimal BonusAmount { get; set; }

    public int GraceDays { get; set; }
    public bool IsActive { get; set; }
    public int ActiveEnrolments { get; set; }
}

public class EnrolRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose the scheme.")]
    public long SavingsSchemeId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose the customer.")]
    public long ContactId { get; set; }

    public DateOnly StartDate { get; set; }
}

public class SchemeEnrolmentListItem
{
    public long SchemeEnrolmentId { get; set; }
    public string? EnrolmentNo { get; set; }
    public long SavingsSchemeId { get; set; }
    public string SchemeName { get; set; } = null!;
    public long ContactId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly MaturityDate { get; set; }
    public decimal InstalmentAmount { get; set; }
    public int Months { get; set; }
    public int InstalmentsPaid { get; set; }
    public decimal PaidPrincipal { get; set; }
    public decimal BonusAmount { get; set; }
    public DateOnly? NextDueDate { get; set; }
    public string Status { get; set; } = null!;
}

public class SchemeEnrolmentView : SchemeEnrolmentListItem
{
    public int GraceDays { get; set; }
    public DateOnly? MaturedOn { get; set; }
    public DateOnly? LapsedOn { get; set; }
    public long? RedeemedInvoiceId { get; set; }
    public DateOnly? RedeemedOn { get; set; }
    public decimal PrincipalRedeemed { get; set; }
    public decimal BonusRedeemed { get; set; }
    public long? BonusJournalId { get; set; }
    public long? RefundSpendMoneyId { get; set; }
    public DateOnly? RefundedOn { get; set; }

    /// <summary>What was paid and not yet spent or refunded: what a refund would return now.</summary>
    public decimal PrincipalAvailable { get; set; }

    public List<SchemeInstalmentView> Instalments { get; set; } = [];
}

public class SchemeInstalmentView
{
    public long SchemeInstalmentId { get; set; }
    public int InstalmentNumber { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Amount { get; set; }
    public long? ReceiveMoneyId { get; set; }
    public DateOnly? PaidOn { get; set; }
}

/// <summary>Taking the next instalment: a receipt into one of the branch's bank or cash accounts.</summary>
public class PayInstalmentRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose the account the money went into.")]
    public long BankAccountId { get; set; }

    public DateOnly PaymentDate { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [MaxLength(50, ErrorMessage = "The reference cannot exceed 50 characters.")]
    public string? ReferenceNo { get; set; }
}

/// <summary>Spending the enrolment on a posted invoice for the same customer.</summary>
public class RedeemEnrolmentRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose the invoice.")]
    public long InvoiceId { get; set; }

    public DateOnly RedemptionDate { get; set; }

    /// <summary>Redeem before maturity, giving up the bonus. Refused without it.</summary>
    public bool CloseEarly { get; set; }
}

/// <summary>Giving back what was paid.</summary>
public class RefundEnrolmentRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose the account the money leaves from.")]
    public long BankAccountId { get; set; }

    public DateOnly RefundDate { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [MaxLength(50, ErrorMessage = "The reference cannot exceed 50 characters.")]
    public string? ReferenceNo { get; set; }
}

public enum SavingsSchemeOutcome
{
    Ok = 0,
    NotFound = 1,
    Invalid = 2,
    Conflict = 3,

    /// <summary>The receipt, refund, journal or allocation underneath was refused; <c>Detail</c> says why.</summary>
    PostingRefused = 4,

    /// <summary>Raced another write. Retry.</summary>
    Retry = 5,
}

public sealed record SavingsSchemeResult(SavingsSchemeOutcome Outcome, long Id = 0, string? Detail = null);

/// <summary>The worker's daily call: lapse what is overdue and say who to remind.</summary>
public class RunSavingsSchemesRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public DateOnly? AsOf { get; set; }
}

public class RunSavingsSchemesResponse
{
    public int Lapsed { get; set; }

    public List<SchemeReminder> Reminders { get; set; } = [];
}

/// <summary>One instalment to remind a customer of.</summary>
public class SchemeReminder
{
    public long SchemeInstalmentId { get; set; }
    public long ContactId { get; set; }
    public string? EnrolmentNo { get; set; }
    public string SchemeName { get; set; } = null!;
    public int InstalmentNumber { get; set; }
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }

    /// <summary>"Due" before the date, "Overdue" after it. Each is sent once per instalment.</summary>
    public string Kind { get; set; } = null!;
}

/// <summary>A bank or cash account an instalment can be taken into or a refund paid from.</summary>
public class SchemeBankAccountOption
{
    public long BankAccountId { get; set; }
    public string AccountName { get; set; } = null!;
    public bool IsDefault { get; set; }
}

/// <summary>A posted invoice of the enrolled customer that still owes something.</summary>
public class SchemeInvoiceOption
{
    public long InvoiceId { get; set; }
    public string? DocumentNo { get; set; }
    public DateOnly DocumentDate { get; set; }
    public decimal Outstanding { get; set; }
}
