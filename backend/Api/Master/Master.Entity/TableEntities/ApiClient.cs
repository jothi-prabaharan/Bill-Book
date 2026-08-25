using System;
using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Master.Entity.TableEntities;

public class ApiClient : OrgScopedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Hashed API key is required.")]
    [MaxLength(200, ErrorMessage = "Hashed API key cannot exceed 200 characters.")]
    public string HashedApiKey { get; set; } = null!;

    public int RoleId { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public bool IsActive { get; set; } = true;
}
