namespace Shared.Kernel.Entities;

/// <summary>
/// Base type for every table entity. Carries the four audit columns and the
/// Postgres xmin concurrency token. All four audit columns are nullable:
/// a row with <see cref="CreatedBy"/> null is system/seed master data,
/// written at provisioning by no user.
///
/// Values are written only by AuditSaveChangesInterceptor — never by hand.
/// </summary>
public abstract class AuditableEntity
{
    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    public Guid? ModifiedBy { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>
    /// Postgres system column <c>xmin</c>, mapped as the concurrency token in
    /// each DbContext (<c>.HasColumnName("xmin").IsRowVersion()</c>).
    /// </summary>
    public uint Version { get; set; }
}
