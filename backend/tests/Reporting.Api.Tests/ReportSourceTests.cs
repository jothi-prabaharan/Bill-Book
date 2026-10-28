using Microsoft.Extensions.Caching.Memory;
using Reporting.Api.Services;
using Reporting.Repository.Repositories;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The two template sources, checked for the things a copy of them could get
/// wrong — since every later report is a copy.
/// </summary>
public class ReportSourceTests
{
    private static readonly AccountMovementSourceRepository Movement = new(OfflineResolver());
    private static readonly AccountTransactionSourceRepository AccountTransaction = new();
    private static readonly TrialBalanceSourceRepository TrialBalance = new(OfflineResolver());
    private static readonly GeneralLedgerSummarySourceRepository GeneralLedgerSummary = new(new StubFinancialYear());
    private static readonly JournalReportSourceRepository JournalReport = new(OfflineResolver());
    private static readonly BankSummarySourceRepository BankSummary = new();
    private static readonly ReconciliationSourceRepository Reconciliation = new();
    private static readonly InventoryAgingSourceRepository InventoryAging = new();
    private static readonly ItemListSourceRepository ItemList = new(OfflineResolver());
    private static readonly ItemDetailSourceRepository ItemDetail = new();
    private static readonly ItemSummarySourceRepository ItemSummary = new();
    private static readonly BatchTrackingStatusSourceRepository BatchTrackingStatus = new();
    private static readonly BatchTrackingDetailSourceRepository BatchTrackingDetail = new();
    private static readonly SerialTrackingStatusSourceRepository SerialTrackingStatus = new();
    private static readonly SerialTrackingDetailSourceRepository SerialTrackingDetail = new();
    private static readonly WarehouseTrackingStatusSourceRepository WarehouseTrackingStatus = new(OfflineResolver());
    private static readonly WarehouseTrackingDetailSourceRepository WarehouseTrackingDetail = new();
    private static readonly SalesRegisterSourceRepository SalesRegister = new();
    private static readonly FxGainLossSourceRepository FxGainLoss = new(new FixedBaseCurrency("INR"));
    private static readonly FxGainLossDetailsSourceRepository FxGainLossDetails = new(new FixedBaseCurrency("INR"));
    private static readonly ArAgingSummarySourceRepository ArAgingSummary = new();
    private static readonly ApAgingSummarySourceRepository ApAgingSummary = new();
    private static readonly CustomerStatementSourceRepository CustomerStatement = new();
    private static readonly VendorStatementSourceRepository VendorStatement = new();
    private static readonly PurchaseRegisterSourceRepository PurchaseRegister = new();
    private static readonly Gstr1SummarySourceRepository Gstr1Summary = new();

    // The tracker and finance reports. They were written, registered nowhere,
    // and listed here nowhere either — which is why 239 tests passed over a
    // catalog that could not serve any of them.
    private static readonly BalanceSheetSourceRepository BalanceSheet = new(OfflineResolver());
    private static readonly ProfitAndLossSourceRepository ProfitAndLoss = new(OfflineResolver());
    private static readonly CashFlowSourceRepository CashFlow = new();
    private static readonly QuotationTrackSourceRepository QuotationTrack = new();
    private static readonly SalesOrderTrackSourceRepository SalesOrderTrack = new();
    private static readonly DeliveryOrderTrackSourceRepository DeliveryOrderTrack = new();
    private static readonly InvoiceTrackSourceRepository InvoiceTrack = new();
    private static readonly AgedReceivablesDetailSourceRepository AgedReceivablesDetail = new();
    private static readonly SalesAnalysisSourceRepository SalesAnalysis = new();
    private static readonly SalesAnalysisDetailSourceRepository SalesAnalysisDetail = new();
    private static readonly PurchaseOrderTrackSourceRepository PurchaseOrderTrack = new();
    private static readonly ReceiveOrderTrackSourceRepository ReceiveOrderTrack = new();
    private static readonly BillsTrackSourceRepository BillsTrack = new();
    private static readonly AgedPayablesDetailsSourceRepository AgedPayablesDetails = new();
    private static readonly PurchaseAnalysisSourceRepository PurchaseAnalysis = new();
    private static readonly ReceivableInvoiceDetailSourceRepository ReceivableInvoiceDetail = new();
    private static readonly ReceivableInvoiceSummarySourceRepository ReceivableInvoiceSummary = new();
    private static readonly InvoiceDnPaymentCollectionSourceRepository InvoiceDnPaymentCollection = new();
    private static readonly PayableInvoiceDetailSourceRepository PayableInvoiceDetail = new();
    private static readonly PayableInvoiceSummarySourceRepository PayableInvoiceSummary = new();
    private static readonly BillDnPaymentSourceRepository BillDnPayment = new();
    private static readonly PurchaseReceiveOrderDetailsSourceRepository PurchaseReceiveOrderDetails = new();
    private static readonly MsmeAgingSourceRepository MsmeAging = new();

    // The fixed-asset register's four reports, over one roll-forward.
    private static readonly DepreciationScheduleSourceRepository DepreciationSchedule = new();
    private static readonly DisposalScheduleSourceRepository DisposalSchedule = new();
    private static readonly FixedAssetReconciliationSourceRepository FixedAssetReconciliation = new();
    private static readonly FixedAssetsScheduleSourceRepository FixedAssetsSchedule = new();

    // D-15: Xero-style KPI ratios.
    private static readonly BusinessPerformanceSourceRepository BusinessPerformance = new();

    // HRMS & Payroll reports (TK-59)
    private static readonly HeadcountSummarySource HeadcountSummary = new();
    private static readonly JoinersLeaversSource JoinersLeavers = new();
    private static readonly AttritionRateSource AttritionRate = new();
    private static readonly ProbationDueSource ProbationDue = new();
    private static readonly BirthdaysAnniversariesSource BirthdaysAnniversaries = new();
    private static readonly DocumentExpirySource DocumentExpiry = new();

    private static readonly DailyAttendanceSource DailyAttendance = new();
    private static readonly MonthlyMusterRollSource MonthlyMusterRoll = new();
    private static readonly LateEarlyOutSource LateEarlyOut = new();
    private static readonly OvertimeSummarySource OvertimeSummary = new();
    private static readonly AttendanceRegularisationsSource AttendanceRegularisations = new();

    private static readonly LeaveRegisterSource LeaveRegister = new();
    private static readonly LeaveBalancesSource LeaveBalances = new();
    private static readonly LeaveEncashmentSource LeaveEncashment = new();
    private static readonly TeamAvailabilitySource TeamAvailability = new();

    private static readonly SalaryRegisterSource SalaryRegister = new();
    private static readonly PayslipSummarySource PayslipSummary = new();
    private static readonly CtcReportSource CtcReport = new();
    private static readonly SalaryVarianceSource SalaryVariance = new();
    private static readonly BankAdviceSource BankAdvice = new();
    private static readonly HeldSalariesSource HeldSalaries = new();
    private static readonly SalaryArrearsSource SalaryArrears = new();
    private static readonly LoansOutstandingSource LoansOutstanding = new();

    private static readonly PfStatementSource PfStatement = new();
    private static readonly EsiStatementSource EsiStatement = new();
    private static readonly PtStatementSource PtStatement = new();
    private static readonly LwfStatementSource LwfStatement = new();
    private static readonly GratuityProvisionSource GratuityProvision = new();
    private static readonly BonusRegisterSource BonusRegister = new();
    private static readonly TdsSummarySource TdsSummary = new();

    private static readonly RecruitmentPipelineSource RecruitmentPipeline = new();
    private static readonly TimeToHireSource TimeToHire = new();
    private static readonly SourceEffectivenessSource SourceEffectiveness = new();
    private static readonly OfferAcceptanceSource OfferAcceptance = new();

    private static readonly ClaimsByCategorySource ClaimsByCategory = new();
    private static readonly ClaimsByEmployeeSource ClaimsByEmployee = new();
    private static readonly ClaimsPendingApprovalSource ClaimsPendingApproval = new();

    private static readonly ProjectProfitabilitySource ProjectProfitability = new();
    private static readonly ProjectBudgetSource ProjectBudget = new();
    private static readonly ProjectUnbilledSource ProjectUnbilled = new();
    private static readonly ProjectTimeByUserSource ProjectTimeByUser = new(OfflineResolver());

    private static readonly BudgetAgainstActualSource BudgetAgainstActual = new(new StubFinancialYear());
    private static readonly BudgetAgainstActualByProjectSource BudgetAgainstActualByProject = new(new StubFinancialYear());
    private static readonly BudgetMonthlyTrendSource BudgetMonthlyTrend = new(new StubFinancialYear());

    // The restaurant reports (TK-150).
    private static readonly RestaurantDayEndSource RestaurantDayEnd = new();
    private static readonly RestaurantItemSalesSource RestaurantItemSales = new();
    private static readonly RestaurantCategorySalesSource RestaurantCategorySales = new();
    private static readonly RestaurantWaiterSalesSource RestaurantWaiterSales = new(OfflineResolver());
    private static readonly RestaurantTableTurnoverSource RestaurantTableTurnover = new();
    private static readonly RestaurantFoodCostSource RestaurantFoodCost = new();
    private static readonly RestaurantWastageSource RestaurantWastage = new();
    private static readonly RestaurantKotCancellationsSource RestaurantKotCancellations = new(OfflineResolver());

    // The site reports (TK-176).
    private static readonly Reporting.Repository.Repositories.Sources.Site.SiteLaborerAttendanceSource SiteLaborerAttendance = new();
    private static readonly Reporting.Repository.Repositories.Sources.Site.SiteMaterialReconciliationSource SiteMaterialReconciliation = new();
    private static readonly Reporting.Repository.Repositories.Sources.Site.SiteProfitSummarySource SiteProfitSummary = new();

    // The datasets (TK-113), with no custom field definitions: their seeded columns only.
    private static readonly SalesLinesDataset SalesLines = new();
    private static readonly PurchaseLinesDataset PurchaseLines = new();
    private static readonly LedgerRowsDataset LedgerRows = new();
    private static readonly StockMovementsDataset StockMovements = new();
    private static readonly ItemsDataset ItemsData = new();
    private static readonly ContactsDataset ContactsData = new();

    /// <summary>
    /// A resolver these tests never call. They read <c>Columns</c> only, which is
    /// declared without touching Master — so the client is here to satisfy the
    /// constructor, and a test that made it reach the network would fail loudly
    /// rather than quietly resolve a name.
    /// </summary>
    private static BatchedNameResolverRepository OfflineResolver() =>
        new(
            new HttpClient { BaseAddress = new Uri("http://reporting.tests.invalid") },
            new MemoryCache(new MemoryCacheOptions()));

    public static TheoryData<IReportSourceRepository> Sources =>
    [
        Movement,
        AccountTransaction,
        TrialBalance,
        GeneralLedgerSummary,
        JournalReport,
        BankSummary,
        Reconciliation,
        InventoryAging,
        ItemList,
        ItemDetail,
        ItemSummary,
        BatchTrackingStatus,
        BatchTrackingDetail,
        SerialTrackingStatus,
        SerialTrackingDetail,
        WarehouseTrackingStatus,
        WarehouseTrackingDetail,
        SalesRegister,
        FxGainLoss,
        FxGainLossDetails,
        ArAgingSummary,
        ApAgingSummary,
        CustomerStatement,
        VendorStatement,
        PurchaseRegister,
        Gstr1Summary,
        BalanceSheet,
        ProfitAndLoss,
        CashFlow,
        QuotationTrack,
        SalesOrderTrack,
        DeliveryOrderTrack,
        InvoiceTrack,
        AgedReceivablesDetail,
        SalesAnalysis,
        SalesAnalysisDetail,
        PurchaseOrderTrack,
        ReceiveOrderTrack,
        BillsTrack,
        AgedPayablesDetails,
        PurchaseAnalysis,
        ReceivableInvoiceDetail,
        ReceivableInvoiceSummary,
        InvoiceDnPaymentCollection,
        PayableInvoiceDetail,
        PayableInvoiceSummary,
        BillDnPayment,
        PurchaseReceiveOrderDetails,
        MsmeAging,
        DepreciationSchedule,
        DisposalSchedule,
        FixedAssetReconciliation,
        FixedAssetsSchedule,
        BusinessPerformance,
        HeadcountSummary,
        JoinersLeavers,
        AttritionRate,
        ProbationDue,
        BirthdaysAnniversaries,
        DocumentExpiry,
        DailyAttendance,
        MonthlyMusterRoll,
        LateEarlyOut,
        OvertimeSummary,
        AttendanceRegularisations,
        LeaveRegister,
        LeaveBalances,
        LeaveEncashment,
        TeamAvailability,
        SalaryRegister,
        PayslipSummary,
        CtcReport,
        SalaryVariance,
        BankAdvice,
        HeldSalaries,
        SalaryArrears,
        LoansOutstanding,
        PfStatement,
        EsiStatement,
        PtStatement,
        LwfStatement,
        GratuityProvision,
        BonusRegister,
        TdsSummary,
        RecruitmentPipeline,
        TimeToHire,
        SourceEffectiveness,
        OfferAcceptance,
        ClaimsByCategory,
        ClaimsByEmployee,
        ClaimsPendingApproval,
        ProjectProfitability,
        ProjectBudget,
        ProjectUnbilled,
        ProjectTimeByUser,
        BudgetAgainstActual,
        BudgetAgainstActualByProject,
        BudgetMonthlyTrend,
        RestaurantDayEnd,
        RestaurantItemSales,
        RestaurantCategorySales,
        RestaurantWaiterSales,
        RestaurantTableTurnover,
        RestaurantFoodCost,
        RestaurantWastage,
        RestaurantKotCancellations,
        SalesLines,
        PurchaseLines,
        LedgerRows,
        StockMovements,
        ItemsData,
        ContactsData,
        SiteLaborerAttendance,
        SiteMaterialReconciliation,
        SiteProfitSummary,
    ];

    [Theory]
    [MemberData(nameof(Sources))]
    public void Every_column_key_is_unique(IReportSourceRepository source)
    {
        // The column map is keyed on these. A duplicate would silently shadow the
        // earlier column, and the shadowed one would be unfilterable in a way no
        // error explains.
        Assert.Equal(
            source.Columns.Select(c => c.Key).Distinct().Count(),
            source.Columns.Count);
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void Only_money_and_quantity_columns_carry_an_aggregate(IReportSourceRepository source)
    {
        // Summing a code, a date or a reference produces a footer figure that looks
        // like an answer and is not one.
        foreach (ReportColumn column in source.Columns.Where(c => c.IsAggregatable))
        {
            Assert.True(
                column.DataType == Entity.Enums.ColumnDataType.Money || 
                column.DataType == Entity.Enums.ColumnDataType.Quantity, 
                $"Column {column.Key} is aggregatable but its type is {column.DataType}");
        }
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void Every_groupable_column_is_text(IReportSourceRepository source) =>
        Assert.All(
            source.Columns.Where(c => c.IsGroupable),
            column => Assert.Equal(typeof(string), column.ValueType));

    [Theory]
    [MemberData(nameof(Sources))]
    public void Every_boolean_valued_column_is_typed_boolean(IReportSourceRepository source)
    {
        // A C# bool declared as anything else — Enum was the mistake found here —
        // renders as the literal words "True"/"False" instead of "Yes"/"No", and
        // offers the wrong filter operators: a multi-select built for a fixed set
        // of named values instead of the three-state Equals/IsNull/IsNotNull a
        // boolean actually needs. There is exactly one correct type for a bool.
        foreach (ReportColumn column in source.Columns.Where(c => c.ValueType == typeof(bool)))
        {
            Assert.Equal(Entity.Enums.ColumnDataType.Boolean, column.DataType);
        }
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void A_report_declares_a_permission_beyond_reporting_view(IReportSourceRepository source)
    {
        // reports.view gets you the catalog. Reading the general ledger through a
        // report must still need accounting.view, or the engine becomes a way round
        // the permission on the screens it reports from.
        Assert.False(string.IsNullOrWhiteSpace(source.RequiredPermission));
        Assert.NotEqual("reports.view", source.RequiredPermission);
    }

    [Fact]
    public void Account_movement_totals_only_debit_and_credit()
    {
        List<string> aggregated =
            [.. Movement.Columns.Where(c => c.IsAggregatable).Select(c => c.Key)];

        Assert.Equal(["debit", "credit"], aggregated);
    }

    [Fact]
    public void Trial_balance_totals_its_balance_column()
    {
        // The sum of this column across every account is the number that says the
        // books balance. Leaving it unaggregated would hide the one figure the
        // report exists to produce.
        ReportColumn balance = TrialBalance.Columns.Single(c => c.Key == "currentBalance");

        Assert.True(balance.IsAggregatable);
    }

    [Fact]
    public void The_internal_account_key_is_not_filterable()
    {
        // Carried so a row can link through to the account ledger, and seeded
        // hidden. Offering it as a filter would put a raw id in front of somebody.
        ReportColumn key = TrialBalance.Columns.Single(c => c.Key == "accountId");

        Assert.False(key.IsFilterable);
    }
}
