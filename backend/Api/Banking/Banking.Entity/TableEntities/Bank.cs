using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Banking.Entity.TableEntities;

/// <summary>
/// The institution — HDFC Bank, State Bank of India. Separate from the account
/// so the name is entered once and reused, and so balances can be reported by
/// institution rather than by whatever each account was called.
/// </summary>
public class Bank : OrgScopedEntity
{
    public long BankId { get; set; }

    [Required(ErrorMessage = "Bank code is required.")]
    [MaxLength(20, ErrorMessage = "Bank code cannot exceed 20 characters.")]
    public string BankCode { get; set; } = null!;

    [Required(ErrorMessage = "Bank name is required.")]
    [MaxLength(100, ErrorMessage = "Bank name cannot exceed 100 characters.")]
    public string BankName { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; } = true;
}
