using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace SavingsScheme.Entity.TableEntities;

public class SchemeInstalment : OrgScopedEntity
{
    public long SchemeInstalmentId { get; set; }

    public long SchemeEnrolmentId { get; set; }
    public SchemeEnrolment SchemeEnrolment { get; set; } = null!;

    public DateOnly DueDate { get; set; }

    public DateOnly? PaidDate { get; set; }

    public decimal AmountDue { get; set; }

    public decimal AmountPaid { get; set; }

    /// <summary>Reference to the Accounting receipt/payment transaction.</summary>
    public long? PaymentVoucherId { get; set; }
}
