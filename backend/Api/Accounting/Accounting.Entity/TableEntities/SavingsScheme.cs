using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// A jewellery savings scheme (TK-141): the customer pays a fixed instalment
/// every month for a set number of months, and at maturity spends the total,
/// plus the jeweller's bonus, on an invoice.
///
/// The terms an enrolment runs on are copied onto it when the customer joins, so
/// changing a scheme later changes nobody's promise.
/// </summary>
public class SavingsScheme : OrgScopedEntity
{
    public long SavingsSchemeId { get; set; }

    [Required(ErrorMessage = "Give the scheme a code.")]
    [MaxLength(20, ErrorMessage = "The code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name the scheme.")]
    [MaxLength(100, ErrorMessage = "The name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [Range(typeof(decimal), "1", "10000000", ErrorMessage = "The instalment must be between 1 and 1,00,00,000.")]
    public decimal InstalmentAmount { get; set; }

    [Range(1, 60, ErrorMessage = "A scheme runs 1 to 60 months.")]
    public int Months { get; set; }

    public SchemeBonusType BonusType { get; set; }

    /// <summary>The amount, the number of instalments, or the percentage, as <see cref="BonusType"/> says.</summary>
    [Range(typeof(decimal), "0", "10000000", ErrorMessage = "The bonus cannot be negative.")]
    public decimal BonusValue { get; set; }

    /// <summary>How many days an instalment may run late before the enrolment lapses.</summary>
    [Range(0, 365, ErrorMessage = "Grace days must be between 0 and 365.")]
    public int GraceDays { get; set; } = 30;

    public bool IsActive { get; set; } = true;
}

/// <summary>One customer in one scheme (TK-141). The scheme's terms are copied here at enrolment.</summary>
public class SchemeEnrolment : OrgScopedEntity
{
    public long SchemeEnrolmentId { get; set; }

    public long SavingsSchemeId { get; set; }

    /// <summary>The scheme's code and a running number, set once the row has its id.</summary>
    [MaxLength(40, ErrorMessage = "The enrolment number cannot exceed 40 characters.")]
    public string? EnrolmentNo { get; set; }

    /// <summary>The customer, in Master's <c>con</c>. Unenforced across the database boundary; checked through the contact's advance sub-account.</summary>
    public long ContactId { get; set; }

    public DateOnly StartDate { get; set; }

    /// <summary>The day after the last instalment's month: <c>StartDate</c> plus the scheme's months.</summary>
    public DateOnly MaturityDate { get; set; }

    public decimal InstalmentAmount { get; set; }

    public int Months { get; set; }

    /// <summary>The bonus earned at maturity, worked out at enrolment from the scheme's rule.</summary>
    public decimal BonusAmount { get; set; }

    public int GraceDays { get; set; }

    public EnrolmentStatus Status { get; set; } = EnrolmentStatus.Active;

    /// <summary>The instalments paid so far, in the branch's currency.</summary>
    public decimal PaidPrincipal { get; set; }

    public DateOnly? MaturedOn { get; set; }

    public DateOnly? LapsedOn { get; set; }

    public long? RedeemedInvoiceId { get; set; }

    public DateOnly? RedeemedOn { get; set; }

    /// <summary>How much of what was paid went onto the invoice. Any rest stays on the customer's advance.</summary>
    public decimal PrincipalRedeemed { get; set; }

    /// <summary>How much of the bonus went onto the invoice. Bonus beyond the invoice is forfeited.</summary>
    public decimal BonusRedeemed { get; set; }

    /// <summary>The system journal that carried the bonus: Dr Savings Scheme Bonus / Cr the customer's receivable.</summary>
    public long? BonusJournalId { get; set; }

    public long? RefundSpendMoneyId { get; set; }

    public DateOnly? RefundedOn { get; set; }
}

/// <summary>One month's instalment (TK-141). Paid once, by one receipt.</summary>
public class SchemeInstalment : OrgScopedEntity
{
    public long SchemeInstalmentId { get; set; }

    public long SchemeEnrolmentId { get; set; }

    public int InstalmentNumber { get; set; }

    public DateOnly DueDate { get; set; }

    public decimal Amount { get; set; }

    /// <summary>The receipt that paid it: a customer advance, posted to the contact's prepayment advance sub-account.</summary>
    public long? ReceiveMoneyId { get; set; }

    public DateOnly? PaidOn { get; set; }
}
