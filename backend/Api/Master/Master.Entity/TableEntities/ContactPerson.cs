using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Validation;

namespace Master.Entity.TableEntities;

/// <summary>
/// The people at a contact. Every contact needs at least one, and exactly one of
/// them is the default — that row is where contact-level email and phone resolve
/// from, which is why the contact itself carries neither.
/// </summary>
public class ContactPerson : OrgScopedEntity
{
    public long ContactPersonId { get; set; }

    public long ContactId { get; set; }

    public long ContactPersonRoleId { get; set; }

    [MaxLength(10, ErrorMessage = "Salutation cannot exceed 10 characters.")]
    public string? Salutation { get; set; }

    [Required(ErrorMessage = "First name is required.")]
    [MaxLength(100, ErrorMessage = "First name cannot exceed 100 characters.")]
    public string FirstName { get; set; } = null!;

    [MaxLength(100, ErrorMessage = "Last name cannot exceed 100 characters.")]
    public string? LastName { get; set; }

    /// <summary>The printed job title, free text — the role is what code filters on.</summary>
    [MaxLength(100, ErrorMessage = "Designation cannot exceed 100 characters.")]
    public string? Designation { get; set; }

    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [MaxLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
    public string? Email { get; set; }

    [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
    [Landline]
    public string? PhoneNumber { get; set; }

    [MaxLength(20, ErrorMessage = "Mobile number cannot exceed 20 characters.")]
    [Mobile]
    public string? MobileNumber { get; set; }

    [MaxLength(200, ErrorMessage = "Website cannot exceed 200 characters.")]
    public string? Website { get; set; }

    /// <summary>Exactly one per contact. The database enforces at most one.</summary>
    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;
}
