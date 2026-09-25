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

    public DbSet<InvoiceRead> Invoices => Set<InvoiceRead>();
    public DbSet<BillRead> Bills => Set<BillRead>();
    public DbSet<InvoiceDetailTaxRead> InvoiceDetailTaxes => Set<InvoiceDetailTaxRead>();

    public DbSet<SalesOrderRead> SalesOrders => Set<SalesOrderRead>();
    public DbSet<SalesOrderDetailRead> SalesOrderDetails => Set<SalesOrderDetailRead>();
    public DbSet<QuoteRead> Quotes => Set<QuoteRead>();
    public DbSet<DeliveryChallanRead> DeliveryChallans => Set<DeliveryChallanRead>();
    public DbSet<InvoiceDetailRead> InvoiceDetails => Set<InvoiceDetailRead>();
    public DbSet<PurchaseOrderRead> PurchaseOrders => Set<PurchaseOrderRead>();
    public DbSet<GoodsReceiptRead> GoodsReceipts => Set<GoodsReceiptRead>();
    public DbSet<BillDetailRead> BillDetails => Set<BillDetailRead>();
    public DbSet<ReceiveMoneyRead> ReceiveMoney => Set<ReceiveMoneyRead>();
    public DbSet<ReceiveMoneyDetailRead> ReceiveMoneyDetails => Set<ReceiveMoneyDetailRead>();
    public DbSet<SpendMoneyRead> SpendMoney => Set<SpendMoneyRead>();
    public DbSet<SpendMoneyDetailRead> SpendMoneyDetails => Set<SpendMoneyDetailRead>();
    public DbSet<GoodsReceiptDetailRead> GoodsReceiptDetails => Set<GoodsReceiptDetailRead>();

    public DbSet<FixedAssetRead> FixedAssets => Set<FixedAssetRead>();

    public DbSet<FixedAssetCategoryRead> FixedAssetCategories => Set<FixedAssetCategoryRead>();

    public DbSet<DepreciationScheduleRead> DepreciationSchedules => Set<DepreciationScheduleRead>();

    public DbSet<AssetTransactionRead> AssetTransactions => Set<AssetTransactionRead>();

    // HRMS and Payroll read models (TK-59)
    public DbSet<EmployeeRecordRead> Employees => Set<EmployeeRecordRead>();
    public DbSet<DepartmentRead> Departments => Set<DepartmentRead>();
    public DbSet<DesignationRead> Designations => Set<DesignationRead>();
    public DbSet<GradeRead> Grades => Set<GradeRead>();
    public DbSet<WorkLocationRead> WorkLocations => Set<WorkLocationRead>();
    public DbSet<CostCentreRead> CostCentres => Set<CostCentreRead>();
    public DbSet<EmployeeDocumentRead> EmployeeDocuments => Set<EmployeeDocumentRead>();
    public DbSet<EmployeeBankDetailRead> EmployeeBankDetails => Set<EmployeeBankDetailRead>();
    public DbSet<SeparationRead> Separations => Set<SeparationRead>();

    public DbSet<DailyAttendanceRead> DailyAttendances => Set<DailyAttendanceRead>();
    public DbSet<ShiftRead> Shifts => Set<ShiftRead>();
    public DbSet<OvertimeRequestRead> OvertimeRequests => Set<OvertimeRequestRead>();
    public DbSet<RegularisationRequestRead> RegularisationRequests => Set<RegularisationRequestRead>();
    public DbSet<LeaveTypeRead> LeaveTypes => Set<LeaveTypeRead>();
    public DbSet<LeaveBalanceRead> LeaveBalances => Set<LeaveBalanceRead>();
    public DbSet<LeaveApplicationRead> LeaveApplications => Set<LeaveApplicationRead>();
    public DbSet<LeaveEncashmentRead> LeaveEncashments => Set<LeaveEncashmentRead>();

    public DbSet<PayrollRunRead> PayrollRuns => Set<PayrollRunRead>();
    public DbSet<PayslipRead> Payslips => Set<PayslipRead>();
    public DbSet<PayslipLineRead> PayslipLines => Set<PayslipLineRead>();
    public DbSet<PayGroupRead> PayGroups => Set<PayGroupRead>();
    public DbSet<SalaryComponentRead> SalaryComponents => Set<SalaryComponentRead>();
    public DbSet<SalaryStructureRead> SalaryStructures => Set<SalaryStructureRead>();
    public DbSet<EmployeeSalaryRead> EmployeeSalaries => Set<EmployeeSalaryRead>();
    public DbSet<SalaryHoldRead> SalaryHolds => Set<SalaryHoldRead>();
    public DbSet<OneTimePaymentRead> OneTimePayments => Set<OneTimePaymentRead>();
    public DbSet<EmployeeLoanRead> EmployeeLoans => Set<EmployeeLoanRead>();
    public DbSet<TaxDeclarationRead> TaxDeclarations => Set<TaxDeclarationRead>();

    public DbSet<JobRequisitionRead> JobRequisitions => Set<JobRequisitionRead>();
    public DbSet<JobOpeningRead> JobOpenings => Set<JobOpeningRead>();
    public DbSet<CandidateRead> Candidates => Set<CandidateRead>();
    public DbSet<ApplicationRead> Applications => Set<ApplicationRead>();
    public DbSet<OfferRead> Offers => Set<OfferRead>();

    public DbSet<ClaimCategoryRead> ClaimCategories => Set<ClaimCategoryRead>();
    public DbSet<ExpenseClaimRead> ExpenseClaims => Set<ExpenseClaimRead>();
    public DbSet<ExpenseClaimLineRead> ExpenseClaimLines => Set<ExpenseClaimLineRead>();
    public DbSet<ApprovalStepRead> ApprovalSteps => Set<ApprovalStepRead>();

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

        MapRead<InvoiceRead>(modelBuilder, "Invoices", "sal", e => e.InvoiceId);
        MapRead<BillRead>(modelBuilder, "Bills", "pur", e => e.BillId);
        MapRead<InvoiceDetailTaxRead>(modelBuilder, "InvoiceDetailTaxes", "sal", e => e.InvoiceDetailTaxId);
        MapRead<SalesOrderRead>(modelBuilder, "SalesOrders", "sal", e => e.SalesOrderId);
        MapRead<SalesOrderDetailRead>(modelBuilder, "SalesOrderDetails", "sal", e => e.SalesOrderDetailId);
        MapRead<QuoteRead>(modelBuilder, "Quotes", "sal", e => e.QuoteId);
        MapRead<DeliveryChallanRead>(modelBuilder, "DeliveryChallans", "sal", e => e.DeliveryChallanId);
        MapRead<InvoiceDetailRead>(modelBuilder, "InvoiceDetails", "sal", e => e.InvoiceDetailId);
        MapRead<PurchaseOrderRead>(modelBuilder, "PurchaseOrders", "pur", e => e.PurchaseOrderId);
        MapRead<GoodsReceiptRead>(modelBuilder, "GoodsReceipts", "pur", e => e.GoodsReceiptId);
        MapRead<BillDetailRead>(modelBuilder, "BillDetails", "pur", e => e.BillDetailId);
        MapRead<ReceiveMoneyRead>(modelBuilder, "ReceiveMoney", "acc", e => e.ReceiveMoneyId);
        MapRead<ReceiveMoneyDetailRead>(modelBuilder, "ReceiveMoneyDetails", "acc", e => e.ReceiveMoneyDetailId);
        MapRead<SpendMoneyRead>(modelBuilder, "SpendMoney", "acc", e => e.SpendMoneyId);
        MapRead<SpendMoneyDetailRead>(modelBuilder, "SpendMoneyDetails", "acc", e => e.SpendMoneyDetailId);
        MapRead<GoodsReceiptDetailRead>(modelBuilder, "GoodsReceiptDetails", "pur", e => e.GoodsReceiptDetailId);

        // The fixed-asset register. Accounting stores its four enums by name, so
        // each is read through the same string conversion it is written with —
        // left as the default int mapping, every comparison against a status
        // would be a type error at the first query rather than at compile time.
        MapRead<FixedAssetRead>(modelBuilder, "FixedAssets", "acc", e => e.FixedAssetId);
        MapRead<FixedAssetCategoryRead>(
            modelBuilder, "FixedAssetCategories", "acc", e => e.FixedAssetCategoryId);
        MapRead<DepreciationScheduleRead>(
            modelBuilder, "DepreciationSchedules", "acc", e => e.DepreciationScheduleId);
        MapRead<AssetTransactionRead>(
            modelBuilder, "AssetTransactions", "acc", e => e.AssetTransactionId);

        modelBuilder.Entity<FixedAssetRead>()
            .Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<DepreciationScheduleRead>()
            .Property(e => e.ScheduleType).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<DepreciationScheduleRead>()
            .Property(e => e.DepreciationMethod).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<AssetTransactionRead>()
            .Property(e => e.TransactionType).HasConversion<string>().HasMaxLength(20);

        // HRMS (hrm)
        MapRead<EmployeeRecordRead>(modelBuilder, "Employees", "hrm", e => e.EmployeeId);
        MapRead<DepartmentRead>(modelBuilder, "Departments", "hrm", e => e.DepartmentId);
        MapRead<DesignationRead>(modelBuilder, "Designations", "hrm", e => e.DesignationId);
        MapRead<GradeRead>(modelBuilder, "Grades", "hrm", e => e.GradeId);
        MapRead<WorkLocationRead>(modelBuilder, "WorkLocations", "hrm", e => e.WorkLocationId);
        MapRead<CostCentreRead>(modelBuilder, "CostCentres", "hrm", e => e.CostCentreId);
        MapRead<EmployeeDocumentRead>(modelBuilder, "EmployeeDocuments", "hrm", e => e.EmployeeDocumentId);
        MapRead<EmployeeBankDetailRead>(modelBuilder, "EmployeeBankDetails", "hrm", e => e.EmployeeBankDetailId);
        MapRead<SeparationRead>(modelBuilder, "Separations", "hrm", e => e.SeparationId);

        // Time and Leave (tla)
        MapRead<DailyAttendanceRead>(modelBuilder, "DailyAttendances", "tla", e => e.DailyAttendanceId);
        MapRead<ShiftRead>(modelBuilder, "Shifts", "tla", e => e.ShiftId);
        MapRead<OvertimeRequestRead>(modelBuilder, "OvertimeRequests", "tla", e => e.OvertimeRequestId);
        MapRead<RegularisationRequestRead>(modelBuilder, "RegularisationRequests", "tla", e => e.RegularisationRequestId);
        MapRead<LeaveTypeRead>(modelBuilder, "LeaveTypes", "tla", e => e.LeaveTypeId);
        MapRead<LeaveBalanceRead>(modelBuilder, "LeaveBalances", "tla", e => e.LeaveBalanceId);
        MapRead<LeaveApplicationRead>(modelBuilder, "LeaveApplications", "tla", e => e.LeaveApplicationId);
        MapRead<LeaveEncashmentRead>(modelBuilder, "LeaveEncashments", "tla", e => e.LeaveEncashmentId);

        // Payroll (pay)
        MapRead<PayrollRunRead>(modelBuilder, "PayrollRuns", "pay", e => e.PayrollRunId);
        MapRead<PayslipRead>(modelBuilder, "Payslips", "pay", e => e.PayslipId);
        MapRead<PayslipLineRead>(modelBuilder, "PayslipLines", "pay", e => e.PayslipLineId);
        MapRead<PayGroupRead>(modelBuilder, "PayGroups", "pay", e => e.PayGroupId);
        MapRead<SalaryComponentRead>(modelBuilder, "SalaryComponents", "pay", e => e.SalaryComponentId);
        MapRead<SalaryStructureRead>(modelBuilder, "SalaryStructures", "pay", e => e.SalaryStructureId);
        MapRead<EmployeeSalaryRead>(modelBuilder, "EmployeeSalaries", "pay", e => e.EmployeeSalaryId);
        MapRead<SalaryHoldRead>(modelBuilder, "SalaryHolds", "pay", e => e.SalaryHoldId);
        MapRead<OneTimePaymentRead>(modelBuilder, "OneTimePayments", "pay", e => e.OneTimePaymentId);
        MapRead<EmployeeLoanRead>(modelBuilder, "EmployeeLoans", "pay", e => e.EmployeeLoanId);
        MapRead<TaxDeclarationRead>(modelBuilder, "TaxDeclarations", "pay", e => e.TaxDeclarationId);

        // Recruitment (rec)
        MapRead<JobRequisitionRead>(modelBuilder, "JobRequisitions", "rec", e => e.JobRequisitionId);
        MapRead<JobOpeningRead>(modelBuilder, "JobOpenings", "rec", e => e.JobOpeningId);
        MapRead<CandidateRead>(modelBuilder, "Candidates", "rec", e => e.CandidateId);
        MapRead<ApplicationRead>(modelBuilder, "Applications", "rec", e => e.ApplicationId);
        MapRead<OfferRead>(modelBuilder, "Offers", "rec", e => e.OfferId);

        // Claims (clm)
        MapRead<ClaimCategoryRead>(modelBuilder, "ClaimCategories", "clm", e => e.ClaimCategoryId);
        MapRead<ExpenseClaimRead>(modelBuilder, "ExpenseClaims", "clm", e => e.ExpenseClaimId);
        MapRead<ExpenseClaimLineRead>(modelBuilder, "ExpenseClaimLines", "clm", e => e.ExpenseClaimLineId);
        MapRead<ApprovalStepRead>(modelBuilder, "ApprovalSteps", "clm", e => e.ApprovalStepId);
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



