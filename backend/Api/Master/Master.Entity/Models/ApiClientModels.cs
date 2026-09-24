using System.ComponentModel.DataAnnotations;

namespace Master.Entity.Models;

/// <summary>A new API key for this branch, acting with one role's permissions (TK-29).</summary>
public class CreateApiClientRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Choose the role the key acts as.")]
    public int RoleId { get; set; }
}

/// <summary>Changes the role an existing key acts as (TK-29).</summary>
public class SetApiClientRoleRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Choose the role the key acts as.")]
    public int RoleId { get; set; }
}

/// <summary>One key as the list shows it. The key itself is shown once, at creation, and never again.</summary>
public sealed record ApiClientDto(Guid Id, string Name, int RoleId, string? RoleName, bool IsActive, DateTime? LastUsedAt);

/// <summary>A role a key may be given.</summary>
public sealed record ApiClientRoleOption(int RoleId, string DisplayName, bool IsSystemRole);

/// <summary>What creating a key returns: the plain key, once.</summary>
public sealed record CreatedApiClient(string ApiKey, ApiClientDto ApiClient);
