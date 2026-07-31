using Microsoft.EntityFrameworkCore;

namespace Shared.Kernel.Numbering;

/// <summary>
/// One mapping for a table several services read. Every service calls this so
/// the model is identical everywhere; exactly one passes
/// <paramref name="ownsMigration"/> true, and the rest map the same shape while
/// excluding it from their own migrations. Two services generating CREATE TABLE
/// for one table is the failure this prevents.
/// </summary>
public static class NumberingSeriesModel
{
    public const string Schema = "acc";

    public static void ConfigureNumberingSeries(this ModelBuilder modelBuilder, bool ownsMigration)
    {
        modelBuilder.Entity<NumberingSeries>(b =>
        {
            b.ToTable("NumberingSeries", Schema, table =>
            {
                if (!ownsMigration)
                {
                    table.ExcludeFromMigrations();
                }

                // A document series must run consecutively, so a hand-typed
                // number is refused at the database as well as in the service.
                table.HasCheckConstraint(
                    "chk_numbering_manual_override",
                    "\"SeriesFor\" = 'Master' OR \"AllowManualOverride\" = false");

                table.HasCheckConstraint(
                    "chk_numbering_counter",
                    "\"NextNumber\" >= \"StartNumber\" AND \"NumberLength\" BETWEEN 1 AND 12");

                // Rendering a branch code that is not stored would silently drop
                // the segment and produce colliding numbers across branches.
                table.HasCheckConstraint(
                    "chk_numbering_branch_code",
                    "\"IncludeBranchCode\" = false OR \"BranchCode\" IS NOT NULL");
            });

            b.HasKey(e => e.NumberingSeriesId);

            b.Property(e => e.SeriesFor).HasConversion<string>().HasMaxLength(10);
            b.Property(e => e.FinancialYearFormat).HasConversion<string>().HasMaxLength(15);
            b.Property(e => e.ResetFrequency).HasConversion<string>().HasMaxLength(10);

            // No HasDefaultValue anywhere here, deliberately. EF omits a property
            // whose value equals the CLR default when the column has a store
            // default, so a store default of true on IsActive would quietly
            // reactivate a series saved as inactive. The property initialisers on
            // the entity carry the defaults instead.
            b.HasIndex(e => new { e.OrgId, e.SeriesName }).IsUnique();

            // The lookup the generator runs on every save.
            b.HasIndex(e => new { e.OrgId, e.SeriesCode, e.BranchId })
                .HasDatabaseName("IX_NumberingSeries_Lookup");

            // One default per code and branch, enforced by the database rather
            // than by whoever remembers to clear the old one.
            b.HasIndex(e => new { e.OrgId, e.SeriesCode, e.BranchId })
                .IsUnique()
                .HasFilter("\"IsDefault\" = true")
                .HasDatabaseName("IX_NumberingSeries_Default");

            b.HasIndex(e => new { e.OrgId, e.SeriesSystemName })
                .IsUnique()
                .HasFilter("\"SeriesSystemName\" IS NOT NULL")
                .HasDatabaseName("IX_NumberingSeries_SystemName");

            b.HasIndex(e => new { e.OrgId, e.DisplayOrder, e.SeriesName })
                .HasDatabaseName("IX_NumberingSeries_Order");
        });
    }
}
