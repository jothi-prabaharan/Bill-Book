using System.ComponentModel.DataAnnotations;
using Master.Entity.Enums;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

/// <summary>The account/billing entity. One Customer = one physical database.</summary>
public class Customer : AuditableEntity
{
    public Guid CustomerId { get; set; }

    /// <summary>10-digit sequential, zero-padded (D10), generated in C#.</summary>
    [Required(ErrorMessage = "Customer code is required.")]
    [MaxLength(10, ErrorMessage = "Customer code cannot exceed 10 characters.")]
    public string CustomerCode { get; set; } = null!;

    [Required(ErrorMessage = "Country prefix is required.")]
    [MaxLength(2, ErrorMessage = "Country prefix must be 2 characters.")]
    public string CountryPrefix { get; set; } = "IN";

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Billing email is required.")]
    [EmailAddress(ErrorMessage = "Billing email must be a valid email address.")]
    [MaxLength(200, ErrorMessage = "Billing email cannot exceed 200 characters.")]
    public string BillingEmail { get; set; } = null!;

    public TenantStatus Status { get; set; } = TenantStatus.Provisioning;

    [Required(ErrorMessage = "Plan tier is required.")]
    [MaxLength(30, ErrorMessage = "Plan tier cannot exceed 30 characters.")]
    public string PlanTier { get; set; } = "Standard";

    [Required(ErrorMessage = "Database name is required.")]
    [MaxLength(50, ErrorMessage = "Database name cannot exceed 50 characters.")]
    public string DatabaseName { get; set; } = null!;
}
