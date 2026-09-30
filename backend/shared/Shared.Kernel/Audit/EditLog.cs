using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Audit;

/// <summary>
/// One insert, update or delete of an audited row (TK-118): the audit trail the
/// Companies (Accounts) Rules require of accounting software, recording every
/// change with its date, which cannot be switched off.
///
/// <b>Append-only in the database.</b> Every tenant schema carries one, mapped by
/// <see cref="TenantDbContext"/> like the error log, and a trigger refuses
/// <c>UPDATE</c>, <c>DELETE</c> and <c>TRUNCATE</c> on it — the application
/// itself cannot alter a row once written. RLS scopes it like any tenant table.
///
/// <b>Written in the same transaction as the change</b>, by
/// <see cref="EditLogWriter"/> inside <c>SaveChanges</c> and by
/// <see cref="EditLogBulk"/> beside each bulk statement, so a change that rolls
/// back leaves no row and a change that commits always has one.
/// </summary>
public class EditLog : OrgScopedEntity
{
    public long EditLogId { get; set; }

    /// <summary>The table changed — <c>Invoices</c>, <c>InvoiceDetails</c>.</summary>
    [Required(ErrorMessage = "Entity name is required.")]
    [MaxLength(100, ErrorMessage = "Entity name cannot exceed 100 characters.")]
    public string EntityName { get; set; } = null!;

    /// <summary>The row's primary key; a composite key's parts joined by <c>|</c>.</summary>
    [Required(ErrorMessage = "Entity key is required.")]
    [MaxLength(200, ErrorMessage = "Entity key cannot exceed 200 characters.")]
    public string EntityKey { get; set; } = null!;

    /// <summary>
    /// The document or master the row belongs to — an invoice for one of its
    /// lines, the row itself for a header — so a History tab reads one key.
    /// </summary>
    [Required(ErrorMessage = "Root entity name is required.")]
    [MaxLength(100, ErrorMessage = "Root entity name cannot exceed 100 characters.")]
    public string RootEntityName { get; set; } = null!;

    [Required(ErrorMessage = "Root entity key is required.")]
    [MaxLength(200, ErrorMessage = "Root entity key cannot exceed 200 characters.")]
    public string RootEntityKey { get; set; } = null!;

    public EditOperation Operation { get; set; }

    /// <summary>
    /// The changed columns, <c>{ "UnitPrice": { "old": 100, "new": 120 } }</c>. An
    /// insert carries only <c>new</c>, a delete only <c>old</c>.
    /// </summary>
    [Required(ErrorMessage = "Changes are required.")]
    public JsonDocument Changes { get; set; } = null!;

    /// <summary>Who, from <c>mst.Users</c>; null for a system write (a worker, a seed).</summary>
    public Guid? UserId { get; set; }

    /// <summary>Their name as it stood when they made the change, so the log reads without a lookup.</summary>
    [MaxLength(200, ErrorMessage = "User name cannot exceed 200 characters.")]
    public string? UserName { get; set; }

    public DateTimeOffset ChangedAt { get; set; }

    /// <summary>The request's trace id, which ties the rows one request wrote together.</summary>
    [MaxLength(64, ErrorMessage = "Request id cannot exceed 64 characters.")]
    public string? RequestId { get; set; }

    /// <summary>
    /// How the row was written: <c>Tracked</c> through <c>SaveChanges</c>, or the
    /// name of the bulk statement that wrote it.
    /// </summary>
    [MaxLength(40, ErrorMessage = "Source cannot exceed 40 characters.")]
    public string? Source { get; set; }
}

public enum EditOperation
{
    Insert = 1,
    Update = 2,
    Delete = 3,
}

/// <summary>
/// Leaves a table, or one column, out of the edit log (TK-118). For what the
/// log itself or its own record already covers — a raw file kept beside its
/// parsed rows — never to hide a business change.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, Inherited = true)]
public sealed class NotAuditedAttribute : Attribute
{
}
