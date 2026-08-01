using System.ComponentModel.DataAnnotations;
using Contacts.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Contacts.Entity.TableEntities;

/// <summary>
/// Where a vendor is paid. Deliberately separate from the organization's own
/// bank accounts in the bnk schema: those are accounts we hold and reconcile,
/// this is an account we send money to and never see a statement for.
/// </summary>
public class ContactBankDetail : OrgScopedEntity
{
    public long ContactBankDetailId { get; set; }

    public long ContactId { get; set; }

    /// <summary>As printed on the passbook — payments are rejected when it does not match.</summary>
    [Required(ErrorMessage = "Account holder name is required.")]
    [MaxLength(100, ErrorMessage = "Account holder name cannot exceed 100 characters.")]
    public string AccountHolderName { get; set; } = null!;

    [Required(ErrorMessage = "Bank name is required.")]
    [MaxLength(100, ErrorMessage = "Bank name cannot exceed 100 characters.")]
    public string BankName { get; set; } = null!;

    [Required(ErrorMessage = "Account number is required.")]
    [MaxLength(30, ErrorMessage = "Account number cannot exceed 30 characters.")]
    public string AccountNumber { get; set; } = null!;

    [MaxLength(11, ErrorMessage = "IFSC must be 11 characters.")]
    [RegularExpression(@"^[A-Z]{4}0[A-Z0-9]{6}$", ErrorMessage = "IFSC is not in a valid format.")]
    public string? Ifsc { get; set; }

    [MaxLength(100, ErrorMessage = "Branch name cannot exceed 100 characters.")]
    public string? BranchName { get; set; }

    public BankAccountKind AccountKind { get; set; } = BankAccountKind.Current;

    /// <summary>A UPI id is often the only thing a small vendor gives you.</summary>
    [MaxLength(100, ErrorMessage = "UPI id cannot exceed 100 characters.")]
    public string? UpiId { get; set; }

    /// <summary>At most one per contact — which account a payment defaults to.</summary>
    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;
}
