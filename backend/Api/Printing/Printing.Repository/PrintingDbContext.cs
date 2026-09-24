using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Printing.Entity.Models;
using Printing.Entity.TableEntities;
using Shared.Kernel.Printing;
using Shared.Kernel.Tenancy;

namespace Printing.Repository;

/// <summary>
/// The prt schema, in the shared tenant database. The base class supplies the
/// CustomerId/OrgId query filter, the insert-time stamp, xmin concurrency and
/// the ErrorLogs table hard rule 14 writes to, so nothing here restates them.
///
/// <para>
/// One table. That is the whole service's persistence: printing reads a
/// template and renders a payload the caller pushed, so it holds no document
/// and reads nobody else's tables — which is the property the split in
/// <c>docs/Printing.md</c> was argued on.
/// </para>
/// </summary>
public class PrintingDbContext : TenantDbContext
{
    public PrintingDbContext(DbContextOptions<PrintingDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<PrintTemplate> PrintTemplates => Set<PrintTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("prt");

        modelBuilder.Entity<PrintTemplate>(b =>
        {
            b.HasKey(e => e.PrintTemplateId);

            b.Property(e => e.DocumentTypeCode).HasMaxLength(3).IsFixedLength();

            // The list screen's only query: this branch's active templates for
            // one document type.
            b.HasIndex(e => new { e.OrgId, e.DocumentTypeCode, e.IsActive });

            // One default per branch per document type. A filtered unique index
            // rather than application code, so two concurrent requests setting
            // a default cannot both win — one of them is refused by Postgres.
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.DocumentTypeCode })
                .IsUnique()
                .HasFilter("\"IsDefault\" AND \"IsActive\"")
                .HasDatabaseName("IX_PrintTemplates_Default");

            // Names are unique among active templates only: a soft-deleted
            // template must not hold its name hostage forever.
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.DocumentTypeCode, e.TemplateName })
                .IsUnique()
                .HasFilter("\"IsActive\"")
                .HasDatabaseName("IX_PrintTemplates_Name");

            b.Property(e => e.Settings)
                .HasColumnType("jsonb")
                .HasConversion(JsonConverter<PrintSettings>(), JsonComparer<PrintSettings>());

            b.Property(e => e.Content)
                .HasColumnType("jsonb")
                .HasConversion(JsonConverter<PrintContent>(), JsonComparer<PrintContent>());
        });

        // Base class applies query filters, OrgId indexes and xmin last so it
        // sees every entity configured above.
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Stores a typed value object as jsonb through PrintJson, so the column and
    /// the API agree about how an enum is written.
    /// </summary>
    private static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<T, string>
        JsonConverter<T>() where T : new() =>
        new(value => PrintJson.Serialize(value), json => PrintJson.Deserialize<T>(json));

    /// <summary>
    /// Change tracking for a mutable value object behind a converter. Without
    /// this EF compares by reference, so editing a segment's HTML in place
    /// produces no UPDATE and the save silently does nothing.
    /// </summary>
    private static ValueComparer<T> JsonComparer<T>() where T : new() =>
        new(
            (left, right) => PrintJson.Serialize(left) == PrintJson.Serialize(right),
            value => PrintJson.Serialize(value).GetHashCode(StringComparison.Ordinal),
            value => PrintJson.Deserialize<T>(PrintJson.Serialize(value)));
}
