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

    // DatabaseName was here, [Required] over a NOT NULL column, and it is gone.
    //
    // It named the customer's own physical database under the model reversed on
    // 25 August 2026, when every customer moved into one shared tenant database
    // isolated by CustomerId, a query filter, an RLS policy and a set_config
    // call. CustomerDatabase, ITenantConnectionResolver and ProvisioningWorker's
    // CREATE DATABASE all went with that decision; this column was missed.
    //
    // Nothing read it or wrote it afterwards — SignupService never set it — so
    // every POST /api/customers/signup died on a not-null violation, and there
    // was no way to create a customer through the product at all. Dropping it
    // finishes a decision already taken rather than making a new one, which is
    // why it is a repair and not a tenancy question.
}
