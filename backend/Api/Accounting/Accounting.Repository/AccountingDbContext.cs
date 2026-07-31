using Accounting.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;

namespace Accounting.Repository;

/// <summary>
/// The acc schema, in a per-customer database. The base class supplies the
/// OrgId query filter, the insert-time OrgId stamp and xmin concurrency, so
/// nothing here needs to remember them.
/// </summary>
public class AccountingDbContext : TenantDbContext
{
    public AccountingDbContext(DbContextOptions<AccountingDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<SubAccount> SubAccounts => Set<SubAccount>();

    public DbSet<TaxMaster> TaxMasters => Set<TaxMaster>();

    /// <summary>
    /// Shared with every service that generates a code. Accounting owns the
    /// migration; the others map the same entity and exclude it from theirs.
    /// </summary>
    public DbSet<NumberingSeries> NumberingSeries => Set<NumberingSeries>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("acc");

        modelBuilder.ConfigureNumberingSeries(ownsMigration: true);

        modelBuilder.Entity<Account>(b =>
        {
            b.HasKey(e => e.AccountId);
            b.HasIndex(e => new { e.OrgId, e.AccountCode }).IsUnique();
            b.HasIndex(e => new { e.OrgId, e.AccountTypeId });

            // Filtered indexes for the pickers that scan these flags.
            b.HasIndex(e => e.OrgId).HasFilter("\"IsBank\" = true").HasDatabaseName("IX_Accounts_Bank");
            b.HasIndex(e => e.OrgId).HasFilter("\"IsSales\" = true").HasDatabaseName("IX_Accounts_Sales");
            b.HasIndex(e => e.OrgId).HasFilter("\"IsPurchase\" = true").HasDatabaseName("IX_Accounts_Purchase");

            b.HasOne<Account>()
                .WithMany()
                .HasForeignKey(e => e.ParentAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SubAccount>(b =>
        {
            b.HasKey(e => e.SubAccountId);

            // The component completes the key, so CGST/SGST/IGST can share a
            // parent account and a tax rate.
            b.HasIndex(e => new { e.AccountId, e.ReferenceType, e.ReferenceId, e.TaxComponent })
                .IsUnique();
            b.HasIndex(e => new { e.OrgId, e.ReferenceType, e.ReferenceId });

            b.Property(e => e.ReferenceType).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.TaxComponent).HasConversion<string>().HasMaxLength(10);

            b.HasOne<Account>()
                .WithMany()
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaxMaster>(b =>
        {
            b.HasKey(e => e.TaxMasterId);
            b.HasIndex(e => new { e.OrgId, e.TaxGroupId, e.EffectiveFrom });
            b.HasIndex(e => new { e.OrgId, e.EffectiveFrom, e.EffectiveTo });
            b.HasIndex(e => e.OrgId).HasFilter("\"IsSales\" = true").HasDatabaseName("IX_TaxMasters_Sales");
            b.HasIndex(e => e.OrgId).HasFilter("\"IsPurchase\" = true").HasDatabaseName("IX_TaxMasters_Purchase");

            foreach (string rate in new[] { "TotalRate", "CgstRate", "SgstRate", "IgstRate", "CessRate" })
            {
                b.Property(rate).HasColumnType("decimal(5,2)");
            }

            // The split invariant, enforced in the database so it cannot drift:
            // CGST equals SGST, the two sum to the total, and IGST is the total.
            b.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "chk_tax_split",
                    "\"CgstRate\" = \"SgstRate\" AND \"CgstRate\" + \"SgstRate\" = \"TotalRate\" "
                        + "AND \"IgstRate\" = \"TotalRate\"");

                // A rate usable on neither document is dead data.
                table.HasCheckConstraint(
                    "chk_tax_applicability",
                    "\"IsSales\" = true OR \"IsPurchase\" = true");

                table.HasCheckConstraint(
                    "chk_tax_effective_range",
                    "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
            });
        });

        // Base class applies query filters, OrgId indexes and xmin last so it
        // sees every entity configured above.
        base.OnModelCreating(modelBuilder);
    }
}
