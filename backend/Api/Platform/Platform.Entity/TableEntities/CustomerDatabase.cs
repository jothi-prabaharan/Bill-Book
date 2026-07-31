using System.ComponentModel.DataAnnotations;
using Platform.Entity.Enums;
using Shared.Kernel.Entities;

namespace Platform.Entity.TableEntities;

/// <summary>Tenant directory: maps a Customer to its physical database. One-to-one with Customer.</summary>
public class CustomerDatabase : AuditableEntity
{
    public Guid CustomerId { get; set; }

    [Required(ErrorMessage = "Database name is required.")]
    [MaxLength(63, ErrorMessage = "Database name cannot exceed 63 characters (Postgres identifier limit).")]
    public string DatabaseName { get; set; } = null!;

    /// <summary>Key Vault reference — never the raw connection string.</summary>
    [Required(ErrorMessage = "Connection secret reference is required.")]
    [MaxLength(200, ErrorMessage = "Connection secret reference cannot exceed 200 characters.")]
    public string ConnectionSecretRef { get; set; } = null!;

    public ProvisioningStatus Status { get; set; } = ProvisioningStatus.Provisioning;

    public DateTimeOffset? ProvisionedAt { get; set; }
}
