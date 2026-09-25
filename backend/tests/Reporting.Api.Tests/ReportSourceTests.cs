using Microsoft.Extensions.Caching.Memory;
using Reporting.Api.Services;
using Reporting.Api.Services.Sources;
using Reporting.Api.Services.Sources.Hrms;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The two template sources, checked for the things a copy of them could get
/// wrong — since every later report is a copy.
/// </summary>
public class ReportSourceTests
{
    private static readonly AccountMovementSource Movement = new(OfflineResolver());
    private static readonly AccountTransactionSource AccountTransaction = new();
    private static readonly TrialBalanceSource TrialBalance = new(OfflineResolver());
    private static readonly GeneralLedgerSummarySource GeneralLedgerSummary = new();
    private static readonly JournalReportSource JournalReport = new(OfflineResolver());
    private static readonly BankSummarySource BankSummary = new(OfflineResolver());
    private static readonly ReconciliationSource Reconciliation = new();
    private static readonly InventoryAgingSource InventoryAging = new();
    private static readonly ItemListSource ItemList = new(OfflineResolver());
    private static readonly ItemDetailSource ItemDetail = new();
    private static readonly ItemSummarySource ItemSummary = new();
    private static readonly BatchTrackingStatusSource BatchTrackingStatus = new();
    private static readonly BatchTrackingDetailSource BatchTrackingDetail = new();
    private static readonly SerialTrackingStatusSource SerialTrackingStatus = new();
    private static readonly SerialTrackingDetailSource SerialTrackingDetail = new();
    private static readonly WarehouseTrackingStatusSource WarehouseTrackingStatus = new(OfflineResolver());
    private static readonly WarehouseTrackingDetailSource WarehouseTrackingDetail = new();
    private static readonly SalesRegisterSource SalesRegister = new();
    private static readonly FxGainLossSource FxGainLoss = new();
    private static readonly FxGainLossDetailsSource FxGainLossDetails = new();
    private static readonly ArAgingSummarySource ArAgingSummary = new();
    private static readonly ApAgingSummarySource ApAgingSummary = new();
    private static readonly CustomerStatementSource CustomerStatement = new();
    private static readonly VendorStatementSource VendorStatement = new();
    private static readonly PurchaseRegisterSource PurchaseRegister = new();
    private static readonly Gstr1SummarySource Gstr1Summary = new();

    // The tracker and finance reports. They were written, registered nowhere,
    // and listed here nowhere either — which is why 239 tests passed over a
    // catalog that could not serve any of them.
    private static readonly BalanceSheetSource BalanceSheet = new(OfflineResolver());
    private static readonly ProfitAndLossSource ProfitAndLoss = new(OfflineResolver());
    private static readonly CashFlowSource CashFlow = new();
    private static readonly QuotationTrackSource QuotationTrack = new();
    private static readonly SalesOrderTrackSource SalesOrderTrack = new();
    private static readonly DeliveryOrderTrackSource DeliveryOrderTrack = new();
    private static readonly InvoiceTrackSource InvoiceTrack = new();
    private static readonly AgedReceivablesDetailSource AgedReceivablesDetail = new();
    private static readonly SalesAnalysisSource SalesAnalysis = new();
    private static readonly SalesAnalysisDetailSource SalesAnalysisDetail = new();
    private static readonly PurchaseOrderTrackSource PurchaseOrderTrack = new();
    private static readonly ReceiveOrderTrackSource ReceiveOrderTrack = new();
    private static readonly BillsTrackSource BillsTrack = new();
    private static readonly AgedPayablesDetailsSource AgedPayablesDetails = new();
    private static readonly PurchaseAnalysisSource PurchaseAnalysis = new();
    private static readonly ReceivableInvoiceDetailSource ReceivableInvoiceDetail = new();
    private static readonly ReceivableInvoiceSummarySource ReceivableInvoiceSummary = new();
    private static readonly InvoiceDnPaymentCollectionSource InvoiceDnPaymentCollection = new();
    private static readonly PayableInvoiceDetailSource PayableInvoiceDetail = new();
    private static readonly PayableInvoiceSummarySource PayableInvoiceSummary = new();
    private static readonly BillDnPaymentSource BillDnPayment = new();
    private static readonly PurchaseReceiveOrderDetailsSource PurchaseReceiveOrderDetails = new();

    // The fixed-asset register's four reports, over one roll-forward.
    private static readonly DepreciationScheduleSource DepreciationSchedule = new();
    private static readonly DisposalScheduleSource DisposalSchedule = new();
    private static readonly FixedAssetReconciliationSource FixedAssetReconciliation = new();
    private static readonly FixedAssetsScheduleSource FixedAssetsSchedule = new();

    // D-15: Xero-style KPI ratios.
    private static readonly BusinessPerformanceSource BusinessPerformance = new();

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

    /// <summary>
    /// A resolver these tests never call. They read <c>Columns</c> only, which is
    /// declared without touching Master — so the client is here to satisfy the
    /// constructor, and a test that made it reach the network would fail loudly
    /// rather than quietly resolve a name.
    /// </summary>
    private static BatchedNameResolver OfflineResolver() =>
        new(
            new HttpClient { BaseAddress = new Uri("http://reporting.tests.invalid") },
            new MemoryCache(new MemoryCacheOptions()));

    public static TheoryData<IReportSource> Sources =>
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
    ];

    [Theory]
    [MemberData(nameof(Sources))]
    public void Every_column_key_is_unique(IReportSource source)
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
    public void Only_money_and_quantity_columns_carry_an_aggregate(IReportSource source)
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
    public void Every_groupable_column_is_text(IReportSource source) =>
        Assert.All(
            source.Columns.Where(c => c.IsGroupable),
            column => Assert.Equal(typeof(string), column.ValueType));

    [Theory]
    [MemberData(nameof(Sources))]
    public void Every_boolean_valued_column_is_typed_boolean(IReportSource source)
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
    public void A_report_declares_a_permission_beyond_reporting_view(IReportSource source)
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
