using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Master.Entity.TableEntities;

/// <summary>
/// The role picker behind contact information — Accounts, Purchase, Dispatch.
/// A master rather than an enum because customers add their own, and it is
/// maintained from a popup on the contact list rather than its own page.
/// </summary>
public class ContactPersonRole : OrgScopedEntity
{
    public long ContactPersonRoleId { get; set; }

    /// <summary>Seeded rows only: the immutable canonical name. Null for user-created roles.</summary>
    [MaxLength(30, ErrorMessage = "Role system name cannot exceed 30 characters.")]
    public string? RoleSystemName { get; set; }

    /// <summary>Display name — editable even on seeded roles.</summary>
    [Required(ErrorMessage = "Role name is required.")]
    [MaxLength(50, ErrorMessage = "Role name cannot exceed 50 characters.")]
    public string RoleName { get; set; } = null!;

    public int DisplayOrder { get; set; }

    /// <summary>Preselected on a new contact-person row. At most one; zero is fine.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Seeded roles: renamable, never deletable.</summary>
    public bool IsSystem { get; set; }

    public bool IsActive { get; set; } = true;
}
