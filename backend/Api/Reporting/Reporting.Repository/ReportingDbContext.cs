using Microsoft.EntityFrameworkCore;
using Reporting.Entity.TableEntities;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository;

/// <summary>
/// The rpt schema, in a per-customer database. The base class supplies the OrgId
/// query filter, the insert-time OrgId stamp and xmin concurrency, so nothing
/// here needs to remember them.
///
/// <b>This context owns three tables and reads many more.</b> The three below are
/// its own — the report catalog, its column metadata and saved layouts. The
/// reports themselves read acc, inv and con, and those come in as read-only
/// models mapped with <c>ExcludeFromMigrations</c> in R0.2: a recorded exception
/// to the rule against crossing a service boundary, argued in Reporting.md §2,
/// and specific to reporting. <b>Nothing in this context ever writes to another
/// schema.</b>
/// </summary>
public class ReportingDbContext : TenantDbContext
{
    public ReportingDbContext(DbContextOptions<ReportingDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<Report> Reports => Set<Report>();

    public DbSet<ReportDetail> ReportDetails => Set<ReportDetail>();

    public DbSet<ReportView> ReportViews => Set<ReportView>();

    public DbSet<ReportMaster> ReportMasters => Set<ReportMaster>();

    public DbSet<ReportColumn> ReportColumns => Set<ReportColumn>();

    // ---- Read-only, from other services' schemas. Never written to. ----
    //
    // The recorded exception of Reporting.md §2. A report engine has to join across
    // acc, inv and con and then page the result; over HTTP there is no join and no
    // server-side paging, and a Postgres view would be raw SQL, which hard rule 1
    // forbids. So Reporting maps its own read models over those tables with
    // ExcludeFromMigrations: it owns no migration for them and never writes to them.
    //
    // Each inherits OrgScopedEntity, which is the deliberate part — the base
    // context applies the OrgId query filter by reflection, so a read model cannot
    // be added without one. That is gate G2, enforced by the type system rather
    // than by remembering.

    public DbSet<JournalLedgerRead> Ledger => Set<JournalLedgerRead>();

    public DbSet<AccountRead> Accounts => Set<AccountRead>();

    public DbSet<SubAccountRead> SubAccounts => Set<SubAccountRead>();

    public DbSet<JournalRead> Journals => Set<JournalRead>();

    public DbSet<JournalDetailRead> JournalDetails => Set<JournalDetailRead>();

    public DbSet<BankAccountRead> BankAccounts => Set<BankAccountRead>();

    public DbSet<BankRead> Banks => Set<BankRead>();

    public DbSet<BankStatementRead> BankStatements => Set<BankStatementRead>();

    public DbSet<BankStatementLineRead> BankStatementLines => Set<BankStatementLineRead>();

    public DbSet<ContactRead> Contacts => Set<ContactRead>();
    public DbSet<ItemRead> Items => Set<ItemRead>();
    public DbSet<ItemStockRead> ItemStocks => Set<ItemStockRead>();
    public DbSet<ItemCategoryRead> ItemCategories => Set<ItemCategoryRead>();
    public DbSet<StockMovementRead> StockMovements => Set<StockMovementRead>();
    public DbSet<CostLayerRead> CostLayers => Set<CostLayerRead>();
    public DbSet<ItemBatchRead> ItemBatches => Set<ItemBatchRead>();
    public DbSet<ItemSerialRead> ItemSerials => Set<ItemSerialRead>();
    public DbSet<WarehouseRead> Warehouses => Set<WarehouseRead>();
    public DbSet<UnitOfMeasureRead> UnitsOfMeasure => Set<UnitOfMeasureRead>();
    public DbSet<SalesRegisterRead> SalesRegisters => Set<SalesRegisterRead>();

    public DbSet<TaxMasterRead> TaxMasters => Set<TaxMasterRead>();

    public DbSet<ContactLicenceRead> ContactLicences => Set<ContactLicenceRead>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("rpt");

        modelBuilder.Entity<Report>(b =>
        {
            b.HasKey(e => e.ReportId);

            // The key is how every route names a report, so it has to be unique
            // within the branch. Across branches it repeats — each has its own
            // copy of the catalog.
            b.HasIndex(e => new { e.OrgId, e.ReportKey }).IsUnique();

            b.HasIndex(e => new { e.OrgId, e.Module, e.SortOrder })
                .HasDatabaseName("IX_Reports_Order");
        });

        modelBuilder.Entity<ReportDetail>(b =>
        {
            b.HasKey(e => e.ReportDetailId);

            b.HasIndex(e => new { e.OrgId, e.ReportId, e.ColumnKey }).IsUnique();

            b.HasIndex(e => new { e.OrgId, e.ReportId, e.SortOrder })
                .HasDatabaseName("IX_ReportDetails_Order");

            // Cascade: a column's presentation has no meaning without its report,
            // and leaving orphans behind would fail the startup check that
            // compares seeded columns against source columns.
            b.HasOne<Report>()
                .WithMany()
                .HasForeignKey(e => e.ReportId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReportView>(b =>
        {
            b.HasKey(e => e.ReportViewId);

            b.Property(e => e.LayoutJson).HasColumnType("jsonb");

            b.HasIndex(e => new { e.OrgId, e.ReportId, e.OwnerUserId, e.ViewName })
                .IsUnique()
                .HasDatabaseName("IX_ReportViews_Name");

            // One default per user per report. A filtered unique index rather
            // than a C# check, because two requests setting a default at once
            // both pass a C# check and only one can pass this.
            b.HasIndex(e => new { e.OrgId, e.ReportId, e.OwnerUserId })
                .IsUnique()
                .HasFilter("\"IsDefault\" = true")
                .HasDatabaseName("IX_ReportViews_Default");

            b.HasOne<Report>()
                .WithMany()
                .HasForeignKey(e => e.ReportId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        ConfigureReadModels(modelBuilder);

        modelBuilder.ApplyConfiguration(new Reporting.Repository.Configurations.ReportMasterConfiguration());
        modelBuilder.ApplyConfiguration(new Reporting.Repository.Configurations.ReportColumnConfiguration());

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Maps the other schemas' tables read-only.
    ///
    /// Three properties hold for every one of them, and all three matter:
    /// <list type="number">
    /// <item><b><c>ExcludeFromMigrations</c></b> — the owning service migrates its
    /// own tables. A Reporting migration that created or altered one of these
    /// would be two services writing one schema, and the second one to run
    /// wins.</item>
    /// <item><b>No navigation to anything writable.</b> A report joins by id in
    /// the query rather than by a navigation property, so there is no path from a
    /// read model to a tracked graph somebody could save.</item>
    /// <item><b>The OrgId query filter comes from the base context</b>, because
    /// each read model inherits <see cref="OrgScopedEntity"/>. Adding one without
    /// a filter would take deliberate effort.</item>
    /// </list>
    ///
    /// <c>ToTable</c> names are the owning services' table names exactly. When one
    /// of those is renamed this breaks at compile time or at the first query,
    /// which is the cost of the §2 exception and was accepted knowingly.
    /// </summary>
    private static void ConfigureReadModels(ModelBuilder modelBuilder)
    {
        MapRead<JournalLedgerRead>(modelBuilder, "JournalLedger", "acc", e => e.LedgerId);
        MapRead<AccountRead>(modelBuilder, "Accounts", "acc", e => e.AccountId);
        MapRead<SubAccountRead>(modelBuilder, "SubAccounts", "acc", e => e.SubAccountId);
        MapRead<JournalRead>(modelBuilder, "Journals", "acc", e => e.JournalId);
        MapRead<JournalDetailRead>(modelBuilder, "JournalDetails", "acc", e => e.JournalDetailId);
        MapRead<BankAccountRead>(modelBuilder, "BankAccounts", "acc", e => e.BankAccountId);
        MapRead<BankRead>(modelBuilder, "Banks", "acc", e => e.BankId);
        MapRead<BankStatementRead>(modelBuilder, "BankStatements", "acc", e => e.BankStatementId);
        MapRead<BankStatementLineRead>(
            modelBuilder, "BankStatementLines", "acc", e => e.BankStatementLineId);
        MapRead<TaxMasterRead>(modelBuilder, "TaxMasters", "acc", e => e.TaxMasterId);
        MapRead<ContactRead>(modelBuilder, "Contacts", "con", e => e.ContactId);
        MapRead<ContactLicenceRead>(
            modelBuilder, "ContactLicences", "con", e => e.ContactLicenceId);
        MapRead<ItemRead>(modelBuilder, "Items", "inv", e => e.ItemId);
        MapRead<ItemStockRead>(modelBuilder, "ItemStocks", "inv", e => e.ItemId);
        MapRead<ItemCategoryRead>(modelBuilder, "ItemCategories", "inv", e => e.ItemCategoryId);
        MapRead<StockMovementRead>(modelBuilder, "StockMovements", "inv", e => e.StockMovementId);
        MapRead<CostLayerRead>(modelBuilder, "CostLayers", "inv", e => e.CostLayerId);
        MapRead<ItemBatchRead>(modelBuilder, "ItemBatches", "inv", e => e.ItemBatchId);
        MapRead<ItemSerialRead>(modelBuilder, "ItemSerials", "inv", e => e.ItemSerialId);
        MapRead<WarehouseRead>(modelBuilder, "Warehouses", "inv", e => e.WarehouseId);
        MapRead<UnitOfMeasureRead>(modelBuilder, "UnitsOfMeasure", "inv", e => e.UomId);
        MapRead<SalesRegisterRead>(modelBuilder, "SalesRegisters", "sal", e => e.SalesRegisterId);
    }

    private static void MapRead<TEntity>(
        ModelBuilder modelBuilder,
        string table,
        string schema,
        System.Linq.Expressions.Expression<Func<TEntity, object?>> key)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>(b =>
        {
            b.ToTable(table, schema, t => t.ExcludeFromMigrations());
            b.HasKey(key);
        });
    }
}



