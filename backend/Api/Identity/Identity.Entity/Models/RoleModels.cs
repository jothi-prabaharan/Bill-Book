using System.ComponentModel.DataAnnotations;

namespace Identity.Entity.Models;

public class RoleListItem
{
    public int RoleId { get; set; }

    public string DisplayName { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsSystemRole { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Active user assignments — a role in use cannot be deleted.</summary>
    public int UserCount { get; set; }

    public int PermissionCount { get; set; }
}

public class RoleDetail : RoleListItem
{
    public IReadOnlyList<int> PermissionIds { get; set; } = new List<int>();
}

/// <summary>The permission matrix, grouped by module so the UI can render an accordion.</summary>
public class PermissionGroup
{
    public string Module { get; set; } = null!;

    public IReadOnlyList<PermissionItem> Permissions { get; set; } = new List<PermissionItem>();
}

public class PermissionItem
{
    public int PermissionId { get; set; }

    public string Code { get; set; } = null!;

    /// <summary>The action portion of {module}.{action}.</summary>
    public string Action { get; set; } = null!;

    public string? Description { get; set; }
}

public class SaveRoleRequest
{
    [Required(ErrorMessage = "Display name is required.")]
    [MaxLength(100, ErrorMessage = "Display name cannot exceed 100 characters.")]
    public string DisplayName { get; set; } = null!;

    [MaxLength(300, ErrorMessage = "Description cannot exceed 300 characters.")]
    public string? Description { get; set; }

    /// <summary>Ignored when updating a system role — their permissions are fixed.</summary>
    public IReadOnlyList<int> PermissionIds { get; set; } = new List<int>();
}
