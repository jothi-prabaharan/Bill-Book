using System.ComponentModel.DataAnnotations;

namespace Contacts.Entity.Models;

public class ContactPersonRoleListItem
{
    public long ContactPersonRoleId { get; set; }

    public string? RoleSystemName { get; set; }

    public string RoleName { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public bool IsDefault { get; set; }

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; }

    /// <summary>How many contact people hold this role — what makes it undeletable.</summary>
    public int UsageCount { get; set; }
}

public class SaveContactPersonRoleRequest
{
    [Required(ErrorMessage = "Role name is required.")]
    [MaxLength(50, ErrorMessage = "Role name cannot exceed 50 characters.")]
    public string RoleName { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// A drag-and-drop drop, described by its neighbours rather than a computed
/// position, so the server picks the value.
/// </summary>
public class ReorderRequest
{
    public long MovedId { get; set; }

    public long? PreviousId { get; set; }

    public long? NextId { get; set; }
}

public enum SaveRoleOutcome
{
    Ok = 0,
    NotFound = 1,
    DuplicateName = 2,
    SystemRoleUndeletable = 3,
    RoleInUse = 4,
    DefaultRoleMustStayActive = 5,
}
