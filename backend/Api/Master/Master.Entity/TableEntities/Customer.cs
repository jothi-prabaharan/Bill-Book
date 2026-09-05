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

    /// <summary>
    /// The physical database this customer's books live in.
    ///
    /// <b>Load-bearing, and it very nearly got deleted for looking vestigial.</b>
    /// It belonged to the one-database-per-customer model recorded as reversed
    /// on 25 August 2026 — and then the sharded-tenancy work reinstated a
    /// customer-to-database mapping, so the column is what
    /// <c>TenantDatabaseResolver</c> reads to pick the connection for a request.
    /// It reads it in <b>raw SQL</b>, which is why nothing in the compiler or
    /// the test suite objects when this property goes away: the break is at run
    /// time, on the first request of every signed-in user.
    ///
    /// The reason it looked dead is that <b>nothing ever assigned it</b>. The
    /// sharded work built the registry (<c>mst.TenantDatabases</c>) and the
    /// resolver and never wrote the step in between, so every signup died on the
    /// not-null constraint and no customer row was ever created carrying one.
    /// <c>ITenantDatabaseAllocator</c> is that step.
    /// </summary>
    [Required(ErrorMessage = "Database name is required.")]
    [MaxLength(50, ErrorMessage = "Database name cannot exceed 50 characters.")]
    public string DatabaseName { get; set; } = null!;
}
