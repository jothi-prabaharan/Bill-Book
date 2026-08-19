using Microsoft.Extensions.Caching.Memory;
using Reporting.Api.Services;
using Reporting.Api.Services.Sources;
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
    private static readonly TrialBalanceSource TrialBalance = new();
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
    private static readonly WarehouseTrackingStatusSource WarehouseTrackingStatus = new();
    private static readonly WarehouseTrackingDetailSource WarehouseTrackingDetail = new();
    private static readonly SalesRegisterSource SalesRegister = new();
    private static readonly FxGainLossSource FxGainLoss = new();
    private static readonly FxGainLossDetailsSource FxGainLossDetails = new();

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
    public void Only_money_columns_carry_an_aggregate(IReportSource source)
    {
        // Summing a code, a date or a reference produces a footer figure that looks
        // like an answer and is not one.
        foreach (ReportColumn column in source.Columns.Where(c => c.IsAggregatable))
        {
            Assert.Equal(Entity.Enums.ColumnDataType.Money, column.DataType);
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
