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

    /// <summary>Reports built from a dataset and saved (TK-113).</summary>
    public DbSet<CustomReport> CustomReports => Set<CustomReport>();

    /// <summary>Reports emailed on a schedule (TK-114).</summary>
    public DbSet<ReportSchedule> ReportSchedules => Set<ReportSchedule>();

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

    /// <summary>The purchase GST register (TK-115).</summary>
    public DbSet<PurchaseRegisterRead> PurchaseRegisters => Set<PurchaseRegisterRead>();

    public DbSet<TaxMasterRead> TaxMasters => Set<TaxMasterRead>();

    public DbSet<ContactLicenceRead> ContactLicences => Set<ContactLicenceRead>();

    public DbSet<InvoiceRead> Invoices => Set<InvoiceRead>();
    public DbSet<CreditNoteRead> CreditNotes => Set<CreditNoteRead>();
    public DbSet<CreditNoteDetailRead> CreditNoteDetails => Set<CreditNoteDetailRead>();
    public DbSet<BillRead> Bills => Set<BillRead>();
    public DbSet<DebitNoteRead> DebitNotes => Set<DebitNoteRead>();
    public DbSet<InvoiceDetailTaxRead> InvoiceDetailTaxes => Set<InvoiceDetailTaxRead>();

    public DbSet<SalesOrderRead> SalesOrders => Set<SalesOrderRead>();
    public DbSet<SalesOrderDetailRead> SalesOrderDetails => Set<SalesOrderDetailRead>();
    public DbSet<QuoteRead> Quotes => Set<QuoteRead>();
    public DbSet<DeliveryChallanRead> DeliveryChallans => Set<DeliveryChallanRead>();
    public DbSet<DeliveryChallanDetailRead> DeliveryChallanDetails => Set<DeliveryChallanDetailRead>();
    public DbSet<InvoiceDetailRead> InvoiceDetails => Set<InvoiceDetailRead>();
    public DbSet<PurchaseOrderRead> PurchaseOrders => Set<PurchaseOrderRead>();
    public DbSet<GoodsReceiptRead> GoodsReceipts => Set<GoodsReceiptRead>();
    public DbSet<BillDetailRead> BillDetails => Set<BillDetailRead>();
    public DbSet<BillDetailTaxRead> BillDetailTaxes => Set<BillDetailTaxRead>();
    public DbSet<ReceiveMoneyRead> ReceiveMoney => Set<ReceiveMoneyRead>();
    public DbSet<ReceiveMoneyDetailRead> ReceiveMoneyDetails => Set<ReceiveMoneyDetailRead>();
    public DbSet<SpendMoneyRead> SpendMoney => Set<SpendMoneyRead>();
    public DbSet<SpendMoneyDetailRead> SpendMoneyDetails => Set<SpendMoneyDetailRead>();
    public DbSet<TransferMoneyRead> TransferMoney => Set<TransferMoneyRead>();
    public DbSet<TransactionRatioRead> TransactionRatios => Set<TransactionRatioRead>();
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
    public DbSet<SalaryRevisionRead> SalaryRevisions => Set<SalaryRevisionRead>();
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

    // Project accounting (acc), for the project reports (TK-107).
    public DbSet<ProjectRead> Projects => Set<ProjectRead>();
    public DbSet<ProjectTaskRead> ProjectTasks => Set<ProjectTaskRead>();
    public DbSet<ProjectMemberRead> ProjectMembers => Set<ProjectMemberRead>();
    public DbSet<ProjectMilestoneRead> ProjectMilestones => Set<ProjectMilestoneRead>();
    public DbSet<TimeEntryRead> TimeEntries => Set<TimeEntryRead>();

    // Budgets (acc), for the budget reports (TK-109).
    public DbSet<BudgetRead> Budgets => Set<BudgetRead>();
    public DbSet<BudgetLineRead> BudgetLines => Set<BudgetLineRead>();

    // Restaurant (sal, inv), for the restaurant reports (TK-150).
    public DbSet<FloorRead> Floors => Set<FloorRead>();
    public DbSet<RestaurantTableRead> RestaurantTables => Set<RestaurantTableRead>();
    public DbSet<RestaurantOrderRead> RestaurantOrders => Set<RestaurantOrderRead>();
    public DbSet<RestaurantOrderLineRead> RestaurantOrderLines => Set<RestaurantOrderLineRead>();
    public DbSet<RestaurantBillRead> RestaurantBills => Set<RestaurantBillRead>();
    public DbSet<KotRead> Kots => Set<KotRead>();
    public DbSet<KotLineRead> KotLines => Set<KotLineRead>();
    public DbSet<InvoiceTenderRead> InvoiceTenders => Set<InvoiceTenderRead>();
    public DbSet<WastageEntryRead> WastageEntries => Set<WastageEntryRead>();

    // Site (sit, TK-170 to TK-176)
    public DbSet<SiteJobRead> SiteJobs => Set<SiteJobRead>();
    public DbSet<SiteTradeRead> SiteTrades => Set<SiteTradeRead>();
    public DbSet<SiteLaborerRead> SiteLaborers => Set<SiteLaborerRead>();
    public DbSet<SiteAssignmentRead> SiteAssignments => Set<SiteAssignmentRead>();
    public DbSet<SiteAttendanceRead> SiteAttendances => Set<SiteAttendanceRead>();
    public DbSet<SiteStockBalanceRead> SiteStockBalances => Set<SiteStockBalanceRead>();
    public DbSet<SiteBillingInvoiceRead> SiteBillingInvoices => Set<SiteBillingInvoiceRead>();
    public DbSet<SiteBillingInvoiceLineRead> SiteBillingInvoiceLines => Set<SiteBillingInvoiceLineRead>();
    public DbSet<SiteStockDocumentRead> SiteStockDocuments => Set<SiteStockDocumentRead>();
    public DbSet<SiteStockLineRead> SiteStockLines => Set<SiteStockLineRead>();

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

        modelBuilder.Entity<CustomReport>(b =>
        {
            b.HasKey(e => e.CustomReportId);

            b.Property(e => e.DefinitionJson).HasColumnType("jsonb");

            // A title once per owner, and once among the shared ones: two
            // "Sales by site" in one list cannot be told apart.
            b.HasIndex(e => new { e.OrgId, e.OwnerUserId, e.Title })
                .IsUnique()
                .AreNullsDistinct(false)
                .HasFilter("\"IsActive\" = true")
                .HasDatabaseName("IX_CustomReports_Title");

            b.HasIndex(e => new { e.OrgId, e.DatasetKey })
                .HasDatabaseName("IX_CustomReports_Dataset");
        });

        modelBuilder.Entity<ReportSchedule>(b =>
        {
            b.HasKey(e => e.ReportScheduleId);

            b.Property(e => e.LayoutJson).HasColumnType("jsonb");
            b.Property(e => e.Frequency).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.DayOfWeek).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.LastStatus).HasConversion<string>().HasMaxLength(20);

            // What the worker reads every tick: the active schedules now due.
            b.HasIndex(e => e.NextRunAt)
                .HasFilter("\"IsActive\" = true")
                .HasDatabaseName("IX_ReportSchedules_Due");

            b.HasIndex(e => new { e.OrgId, e.OwnerUserId })
                .HasDatabaseName("IX_ReportSchedules_Owner");
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
        MapRead<ItemStockRead>(modelBuilder, "ItemStock", "inv", e => e.ItemId);
        MapRead<ItemCategoryRead>(modelBuilder, "ItemCategories", "inv", e => e.ItemCategoryId);
        MapRead<StockMovementRead>(modelBuilder, "StockMovements", "inv", e => e.StockMovementId);
        MapRead<CostLayerRead>(modelBuilder, "CostLayers", "inv", e => e.CostLayerId);
        MapRead<ItemBatchRead>(modelBuilder, "ItemBatches", "inv", e => e.ItemBatchId);
        MapRead<ItemSerialRead>(modelBuilder, "ItemSerials", "inv", e => e.ItemSerialId);
        MapRead<WarehouseRead>(modelBuilder, "Warehouses", "inv", e => e.WarehouseId);
        MapRead<UnitOfMeasureRead>(modelBuilder, "UnitOfMeasures", "inv", e => e.UomId);
        MapRead<SalesRegisterRead>(modelBuilder, "SalesRegister", "sal", e => e.SalesRegisterId);
        MapRead<PurchaseRegisterRead>(modelBuilder, "PurchaseRegister", "pur", e => e.PurchaseRegisterId);

        MapRead<InvoiceRead>(modelBuilder, "Invoices", "sal", e => e.InvoiceId);
        MapRead<CreditNoteRead>(modelBuilder, "CreditNotes", "sal", e => e.CreditNoteId);
        MapRead<CreditNoteDetailRead>(modelBuilder, "CreditNoteDetails", "sal", e => e.CreditNoteDetailId);
        MapRead<BillRead>(modelBuilder, "Bills", "pur", e => e.BillId);
        MapRead<DebitNoteRead>(modelBuilder, "DebitNotes", "pur", e => e.DebitNoteId);
        MapRead<InvoiceDetailTaxRead>(modelBuilder, "InvoiceDetailTaxes", "sal", e => e.InvoiceDetailTaxId);
        MapRead<SalesOrderRead>(modelBuilder, "SalesOrders", "sal", e => e.SalesOrderId);
        MapRead<SalesOrderDetailRead>(modelBuilder, "SalesOrderDetails", "sal", e => e.SalesOrderDetailId);
        MapRead<QuoteRead>(modelBuilder, "Quotes", "sal", e => e.QuoteId);
        MapRead<DeliveryChallanRead>(modelBuilder, "DeliveryChallans", "sal", e => e.DeliveryChallanId);
        MapRead<DeliveryChallanDetailRead>(modelBuilder, "DeliveryChallanDetails", "sal", e => e.DeliveryChallanDetailId);
        MapRead<InvoiceDetailRead>(modelBuilder, "InvoiceDetails", "sal", e => e.InvoiceDetailId);
        MapRead<PurchaseOrderRead>(modelBuilder, "PurchaseOrders", "pur", e => e.PurchaseOrderId);
        MapRead<GoodsReceiptRead>(modelBuilder, "GoodsReceipts", "pur", e => e.GoodsReceiptId);
        MapRead<BillDetailRead>(modelBuilder, "BillDetails", "pur", e => e.BillDetailId);
        MapRead<BillDetailTaxRead>(modelBuilder, "BillDetailTaxes", "pur", e => e.BillDetailTaxId);
        MapRead<ReceiveMoneyRead>(modelBuilder, "ReceiveMoney", "acc", e => e.ReceiveMoneyId);
        MapRead<ReceiveMoneyDetailRead>(modelBuilder, "ReceiveMoneyDetails", "acc", e => e.ReceiveMoneyDetailId);
        MapRead<SpendMoneyRead>(modelBuilder, "SpendMoney", "acc", e => e.SpendMoneyId);
        MapRead<SpendMoneyDetailRead>(modelBuilder, "SpendMoneyDetails", "acc", e => e.SpendMoneyDetailId);
        MapRead<TransferMoneyRead>(modelBuilder, "TransferMoney", "acc", e => e.TransferMoneyId);
        MapRead<TransactionRatioRead>(modelBuilder, "TransactionRatios", "acc", e => e.TransactionRatioId);
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

        // A sales or purchase document's status is stored as its name
        // (DocumentModelConfiguration), so the read has to convert it too. Read
        // as the enum's integer, every report touching one of these statuses
        // failed on the first row (found by TK-95).
        modelBuilder.Entity<InvoiceRead>().Property(e => e.Status).HasConversion<string>().HasMaxLength(12);
        // The same for Accounting's money documents, statement lines and bank
        // accounts, which were read as integers too: every query touching one
        // of these columns failed on its first row (found by TK-202).
        modelBuilder.Entity<SpendMoneyRead>().Property(e => e.Status).HasConversion<string>().HasMaxLength(12);
        modelBuilder.Entity<ReceiveMoneyRead>().Property(e => e.Status).HasConversion<string>().HasMaxLength(12);
        modelBuilder.Entity<TransferMoneyRead>().Property(e => e.Status).HasConversion<string>().HasMaxLength(12);
        modelBuilder.Entity<BankStatementLineRead>().Property(e => e.Status).HasConversion<string>().HasMaxLength(12);
        modelBuilder.Entity<BankAccountRead>().Property(e => e.AccountType).HasConversion<string>().HasMaxLength(15);
        modelBuilder.Entity<CreditNoteRead>().Property(e => e.Status).HasConversion<string>().HasMaxLength(12);
        modelBuilder.Entity<CreditNoteRead>().Property(e => e.ReasonCode).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<QuoteRead>().Property(e => e.Status).HasConversion<string>().HasMaxLength(12);
        modelBuilder.Entity<QuoteRead>().Property(e => e.CustomerResponse).HasConversion<string>().HasMaxLength(10);
        modelBuilder.Entity<SalesOrderRead>().Property(e => e.Status).HasConversion<string>().HasMaxLength(12);
        modelBuilder.Entity<DeliveryChallanRead>().Property(e => e.Status).HasConversion<string>().HasMaxLength(12);
        modelBuilder.Entity<BillRead>().Property(e => e.Status).HasConversion<string>().HasMaxLength(12);
        modelBuilder.Entity<DebitNoteRead>().Property(e => e.Status).HasConversion<string>().HasMaxLength(12);
        modelBuilder.Entity<PurchaseOrderRead>().Property(e => e.Status).HasConversion<string>().HasMaxLength(12);
        modelBuilder.Entity<GoodsReceiptRead>().Property(e => e.Status).HasConversion<string>().HasMaxLength(12);

        // The owning services store these enums by name, so each is read as its
        // name. Read as an int, a comparison failed in SQL with "character
        // varying = integer" and a projection failed on the first row (TK-213).
        modelBuilder.Entity<StockMovementRead>(b =>
        {
            b.Property(e => e.MovementType).HasConversion<string>().HasMaxLength(15);
            b.Property(e => e.Direction).HasConversion<string>().HasMaxLength(3);
            b.Property(e => e.CostingStatus).HasConversion<string>().HasMaxLength(12);
            b.Property(e => e.LedgerStatus).HasConversion<string>().HasMaxLength(14);
        });
        modelBuilder.Entity<ItemRead>(b =>
        {
            b.Property(e => e.ItemProfile).HasConversion<string>().HasMaxLength(15);
            b.Property(e => e.ItemType).HasConversion<string>().HasMaxLength(10);
            b.Property(e => e.TaxPreference).HasConversion<string>().HasMaxLength(15);
            b.Property(e => e.CostingType).HasConversion<string>().HasMaxLength(25);
        });
        modelBuilder.Entity<ItemCategoryRead>(b =>
        {
            b.Property(e => e.DefaultItemProfile).HasConversion<string>().HasMaxLength(15);
            b.Property(e => e.DefaultCostingType).HasConversion<string>().HasMaxLength(20);
        });
        modelBuilder.Entity<ItemSerialRead>().Property(e => e.Status).HasConversion<string>().HasMaxLength(10);
        modelBuilder.Entity<WarehouseRead>(b =>
        {
            b.Property(e => e.WarehouseType).HasConversion<string>().HasMaxLength(15);
            b.Property(e => e.StorageType).HasConversion<string>().HasMaxLength(15);
        });
        modelBuilder.Entity<SubAccountRead>(b =>
        {
            b.Property(e => e.ReferenceType).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Purpose).HasConversion<string>().HasMaxLength(20);
        });
        modelBuilder.Entity<JournalRead>().Property(e => e.Status).HasConversion<string>().HasMaxLength(10);
        modelBuilder.Entity<SalesOrderRead>().Property(e => e.FulfilmentStatus).HasConversion<string>().HasMaxLength(16);
        modelBuilder.Entity<DeliveryChallanRead>().Property(e => e.ChallanType).HasConversion<string>().HasMaxLength(16);
        modelBuilder.Entity<PurchaseOrderRead>().Property(e => e.FulfilmentStatus).HasConversion<string>().HasMaxLength(16);
        modelBuilder.Entity<ReceiveMoneyRead>().Property(e => e.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<SpendMoneyRead>().Property(e => e.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<ContactLicenceRead>().Property(e => e.LicenceType).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<DepreciationScheduleRead>()
            .Property(e => e.ScheduleType).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<DepreciationScheduleRead>()
            .Property(e => e.DepreciationMethod).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<AssetTransactionRead>()
            .Property(e => e.TransactionType).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<InvoiceDetailRead>().Property(e => e.LineType).HasConversion<string>().HasMaxLength(10);
        modelBuilder.Entity<InvoiceDetailRead>().Property(e => e.TaxTreatment).HasConversion<string>().HasMaxLength(10);
        modelBuilder.Entity<BillDetailRead>().Property(e => e.LineType).HasConversion<string>().HasMaxLength(10);
        modelBuilder.Entity<BillDetailRead>().Property(e => e.TaxTreatment).HasConversion<string>().HasMaxLength(10);
        modelBuilder.Entity<SalesOrderDetailRead>().Property(e => e.LineType).HasConversion<string>().HasMaxLength(10);
        modelBuilder.Entity<SalesOrderDetailRead>().Property(e => e.TaxTreatment).HasConversion<string>().HasMaxLength(10);
        modelBuilder.Entity<GoodsReceiptDetailRead>().Property(e => e.LineType).HasConversion<string>().HasMaxLength(10);
        modelBuilder.Entity<GoodsReceiptDetailRead>().Property(e => e.TaxTreatment).HasConversion<string>().HasMaxLength(10);
        modelBuilder.Entity<InvoiceDetailTaxRead>().Property(e => e.TaxComponent).HasConversion<string>().HasMaxLength(6);
        modelBuilder.Entity<BillDetailTaxRead>().Property(e => e.TaxComponent).HasConversion<string>().HasMaxLength(6);

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
        MapRead<SalaryRevisionRead>(modelBuilder, "SalaryRevisions", "pay", e => e.SalaryRevisionId);
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

        // Projects (acc, TK-107)
        MapRead<ProjectRead>(modelBuilder, "Projects", "acc", e => e.ProjectId);
        MapRead<ProjectTaskRead>(modelBuilder, "ProjectTasks", "acc", e => e.ProjectTaskId);
        MapRead<ProjectMemberRead>(modelBuilder, "ProjectMembers", "acc", e => e.ProjectMemberId);
        MapRead<ProjectMilestoneRead>(modelBuilder, "ProjectMilestones", "acc", e => e.ProjectMilestoneId);
        MapRead<TimeEntryRead>(modelBuilder, "TimeEntries", "acc", e => e.TimeEntryId);

        // Budgets (acc, TK-109)
        MapRead<BudgetRead>(modelBuilder, "Budgets", "acc", e => e.BudgetId);
        MapRead<BudgetLineRead>(modelBuilder, "BudgetLines", "acc", e => e.BudgetLineId);

        // Restaurant (sal, inv, TK-150)
        MapRead<FloorRead>(modelBuilder, "Floors", "sal", e => e.FloorId);
        MapRead<RestaurantTableRead>(modelBuilder, "RestaurantTables", "sal", e => e.RestaurantTableId);
        MapRead<RestaurantOrderRead>(modelBuilder, "RestaurantOrders", "sal", e => e.RestaurantOrderId);
        MapRead<RestaurantOrderLineRead>(modelBuilder, "RestaurantOrderLines", "sal", e => e.RestaurantOrderLineId);
        MapRead<RestaurantBillRead>(modelBuilder, "RestaurantBills", "sal", e => e.RestaurantBillId);
        MapRead<KotRead>(modelBuilder, "Kots", "sal", e => e.KotId);
        MapRead<KotLineRead>(modelBuilder, "KotLines", "sal", e => e.KotLineId);
        MapRead<InvoiceTenderRead>(modelBuilder, "InvoiceTenders", "sal", e => e.InvoiceTenderId);
        MapRead<WastageEntryRead>(modelBuilder, "WastageEntries", "inv", e => e.WastageEntryId);

        // Site (sit)
        MapRead<SiteJobRead>(modelBuilder, "Sites", "sit", e => e.SiteId);
        MapRead<SiteTradeRead>(modelBuilder, "Trades", "sit", e => e.TradeId);
        MapRead<SiteLaborerRead>(modelBuilder, "Laborers", "sit", e => e.LaborerId);
        MapRead<SiteAssignmentRead>(modelBuilder, "SiteAssignments", "sit", e => e.AssignmentId);
        MapRead<SiteAttendanceRead>(modelBuilder, "SiteAttendance", "sit", e => e.AttendanceId);
        MapRead<SiteStockBalanceRead>(modelBuilder, "SiteStockBalances", "sit", e => e.BalanceId);
        MapRead<SiteBillingInvoiceRead>(modelBuilder, "SiteBillingInvoices", "sit", e => e.SiteBillingInvoiceId);
        MapRead<SiteBillingInvoiceLineRead>(modelBuilder, "SiteBillingInvoiceLines", "sit", e => e.SiteBillingInvoiceLineId);
        MapRead<SiteStockDocumentRead>(modelBuilder, "SiteStockDocuments", "sit", e => e.DocumentId);
        MapRead<SiteStockLineRead>(modelBuilder, "SiteStockLines", "sit", e => e.LineId);
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



