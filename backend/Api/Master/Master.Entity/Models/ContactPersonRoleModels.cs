using System.ComponentModel.DataAnnotations;

namespace Master.Entity.Models;

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

public enum SaveRoleOutcome
{
    Ok = 0,
    NotFound = 1,
    DuplicateName = 2,
    SystemRoleUndeletable = 3,
    RoleInUse = 4,
    DefaultRoleMustStayActive = 5,
}
