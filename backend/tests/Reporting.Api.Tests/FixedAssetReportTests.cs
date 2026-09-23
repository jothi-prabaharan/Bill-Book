using Reporting.Api.Services;
using Reporting.Api.Services.Sources;
using Reporting.Entity.Enums;
using Reporting.Repository.ReadModels;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The fixed-asset roll-forward and the reconciliation, over lists.
///
/// <b>What this proves and what it does not.</b> It proves the arithmetic: which
/// asset is opening and which an addition, what a disposal takes off the register,
/// how a gain splits from a capital gain, which ledger lines count on which side.
/// It does not prove the query translates — LINQ to Objects will run anything —
/// so the four reports still need running against a real <c>acc</c> schema.
/// </summary>
public sealed class FixedAssetReportTests
{
    private static readonly DateOnly Start = new(2026, 4, 1);
    private static readonly DateOnly End = new(2027, 3, 31);

    private const long PlantAccount = 10;
    private const long PlantAccumDep = 11;
    private const long DepreciationExpense = 12;
    private const long VehicleAccount = 20;
    private const long VehicleAccumDep = 21;
    private const long Bank = 99;

    private const long Plant = 1;
    private const long Vehicles = 2;

    // ---- the roll-forward ------------------------------------------------------

    [Fact]
    public void An_asset_held_all_period_rolls_its_cost_and_depreciation_forward()
    {
        Register register = new();
        register.Asset(1, Plant, new DateOnly(2025, 1, 10), 12_000m);
        register.Charge(1, new DateOnly(2025, 12, 31), 1_000m);
        register.Charge(1, new DateOnly(2026, 3, 31), 1_000m);
        register.Charge(1, new DateOnly(2026, 6, 30), 1_000m);
        register.Charge(1, new DateOnly(2026, 9, 30), 1_000m);
        register.Charge(1, new DateOnly(2027, 6, 30), 1_000m);

        FixedAssetPeriodRow row = Assert.Single(register.Rows(Start, End));

        Assert.Equal(12_000m, row.OpeningCost);
        Assert.Equal(0m, row.AdditionCost);
        Assert.Equal(0m, row.DisposalCost);
        Assert.Equal(12_000m, row.ClosingCost);
        Assert.Equal(2_000m, row.OpeningAccumDep);
        Assert.Equal(2_000m, row.Depreciation);
        Assert.Equal(0m, row.DisposalAccumDep);
        Assert.Equal(4_000m, row.ClosingAccumDep);
        Assert.Equal(10_000m, row.OpeningNbv);
        Assert.Equal(8_000m, row.ClosingNbv);
        Assert.Null(row.DisposalDate);
        Assert.Null(row.SaleProceeds);
        Assert.Null(row.GainOnDisposal);
        Assert.Null(row.LossOnDisposal);
        Assert.Null(row.CapitalGain);
    }

    [Fact]
    public void An_asset_bought_within_the_period_is_an_addition_not_an_opening_balance()
    {
        Register register = new();
        register.Asset(1, Plant, Start, 5_000m);

        FixedAssetPeriodRow row = Assert.Single(register.Rows(Start, End));

        Assert.Equal(0m, row.OpeningCost);
        Assert.Equal(5_000m, row.AdditionCost);
        Assert.Equal(5_000m, row.ClosingCost);
        Assert.Equal(0m, row.OpeningNbv);
    }

    [Fact]
    public void Without_a_start_nothing_is_opening_and_everything_is_movement()
    {
        Register register = new();
        register.Asset(1, Plant, new DateOnly(2025, 1, 10), 12_000m);
        register.Charge(1, new DateOnly(2025, 12, 31), 1_000m);
        register.Charge(1, new DateOnly(2027, 6, 30), 1_000m);

        FixedAssetPeriodRow bounded = Assert.Single(register.Rows(null, End));
        FixedAssetPeriodRow open = Assert.Single(register.Rows(null, null));

        Assert.Equal(0m, bounded.OpeningCost);
        Assert.Equal(12_000m, bounded.AdditionCost);
        Assert.Equal(0m, bounded.OpeningAccumDep);
        Assert.Equal(1_000m, bounded.Depreciation);
        Assert.Equal(2_000m, open.Depreciation);
    }

    [Fact]
    public void Draft_assets_future_assets_and_assets_already_gone_are_not_on_the_register()
    {
        Register register = new();
        register.Asset(1, Plant, new DateOnly(2025, 1, 10), 1_000m, FixedAssetStatus.Draft);
        register.Asset(2, Plant, End.AddDays(1), 1_000m);
        register.Asset(3, Plant, new DateOnly(2024, 1, 1), 1_000m, FixedAssetStatus.Disposed);
        register.Dispose(3, Start.AddDays(-1), 100m);
        register.Asset(4, Plant, new DateOnly(2024, 1, 1), 1_000m);

        FixedAssetPeriodRow row = Assert.Single(register.Rows(Start, End));

        Assert.Equal(4, row.FixedAssetId);
    }

    [Fact]
    public void An_asset_disposed_of_after_the_period_is_still_held_at_its_end()
    {
        Register register = new();
        register.Asset(1, Plant, new DateOnly(2025, 1, 10), 12_000m, FixedAssetStatus.Disposed);
        register.Charge(1, new DateOnly(2026, 6, 30), 1_000m);
        register.Dispose(1, End.AddDays(30), 9_000m);

        FixedAssetPeriodRow row = Assert.Single(register.Rows(Start, End));

        Assert.Equal(12_000m, row.ClosingCost);
        Assert.Equal(1_000m, row.ClosingAccumDep);
        Assert.Equal(11_000m, row.ClosingNbv);
        Assert.Null(row.DisposalDate);
        Assert.Null(row.SaleProceeds);
    }

    [Fact]
    public void A_disposal_takes_the_asset_and_its_depreciation_off_the_register()
    {
        Register register = new();
        DateOnly disposed = new(2026, 10, 15);
        register.Asset(1, Plant, new DateOnly(2025, 1, 10), 12_000m, FixedAssetStatus.Disposed);
        register.Charge(1, new DateOnly(2026, 3, 31), 3_000m);
        register.Charge(1, new DateOnly(2026, 9, 30), 1_000m);
        register.Dispose(1, disposed, 5_000m);

        FixedAssetPeriodRow row = Assert.Single(register.Rows(Start, End));

        Assert.Equal(12_000m, row.DisposalCost);
        Assert.Equal(0m, row.ClosingCost);
        Assert.Equal(4_000m, row.DisposalAccumDep);
        Assert.Equal(0m, row.ClosingAccumDep);
        Assert.Equal(0m, row.ClosingNbv);
        Assert.Equal(disposed, row.DisposalDate);
        Assert.Equal(5_000m, row.SaleProceeds);
        Assert.Equal(8_000m, row.NbvAtDisposal);
    }

    [Theory]
    // Book value on disposal is 8,000 against a cost of 12,000.
    [InlineData(5_000, 0, 3_000, 0)]      // below book value: a loss
    [InlineData(8_000, 0, 0, 0)]          // at book value: neither
    [InlineData(9_000, 1_000, 0, 0)]      // above book value, below cost: a gain
    [InlineData(15_000, 4_000, 0, 3_000)] // above cost: a gain up to cost, a capital gain beyond
    public void Proceeds_split_into_gain_loss_and_capital_gain(
        int proceeds, int gain, int loss, int capitalGain)
    {
        Register register = new();
        register.Asset(1, Plant, new DateOnly(2025, 1, 10), 12_000m, FixedAssetStatus.Disposed);
        register.Charge(1, new DateOnly(2026, 3, 31), 4_000m);
        register.Dispose(1, new DateOnly(2026, 10, 15), (decimal)proceeds);

        FixedAssetPeriodRow row = Assert.Single(register.Rows(Start, End));

        Assert.Equal((decimal)gain, row.GainOnDisposal);
        Assert.Equal((decimal)loss, row.LossOnDisposal);
        Assert.Equal((decimal)capitalGain, row.CapitalGain);

        // Whatever the split, it accounts for proceeds less book value exactly.
        Assert.Equal(
            (decimal)proceeds - row.NbvAtDisposal,
            row.GainOnDisposal + row.CapitalGain - row.LossOnDisposal);
    }

    [Fact]
    public void Only_the_books_schedule_is_counted_and_described()
    {
        Register register = new();
        register.Asset(1, Plant, new DateOnly(2025, 1, 10), 12_000m);
        register.Schedule(1, DepreciationScheduleType.Books, DepreciationMethod.StraightLine, 10m, 10, 500m);
        register.Schedule(1, DepreciationScheduleType.Tax, DepreciationMethod.WrittenDownValue, 15m, 0, 0m);
        register.Charge(1, new DateOnly(2026, 6, 30), 100m, register.ScheduleId(1, DepreciationScheduleType.Books));
        register.Charge(1, new DateOnly(2026, 6, 30), 999m, register.ScheduleId(1, DepreciationScheduleType.Tax));
        register.Charge(1, new DateOnly(2026, 7, 31), 100m, scheduleId: null);

        FixedAssetPeriodRow row = Assert.Single(register.Rows(Start, End));

        Assert.Equal(200m, row.Depreciation);
        Assert.Equal("Straight Line", row.DepMethod);
        Assert.Equal(10m, row.Rate);
        Assert.Equal(10, row.EffectiveLife);
        Assert.Equal(500m, row.ResidualValue);
    }

    [Fact]
    public void An_asset_with_no_schedule_has_no_schedule_details()
    {
        Register register = new();
        register.Asset(1, Plant, new DateOnly(2025, 1, 10), 12_000m);

        FixedAssetPeriodRow row = Assert.Single(register.Rows(Start, End));

        Assert.Null(row.DepMethod);
        Assert.Null(row.DepStartDate);
        Assert.Null(row.Rate);
        Assert.Null(row.EffectiveLife);
        Assert.Null(row.ResidualValue);
    }

    [Fact]
    public void The_category_supplies_the_asset_type_and_its_three_accounts()
    {
        Register register = new();
        register.Asset(1, Vehicles, new DateOnly(2025, 1, 10), 12_000m);

        FixedAssetPeriodRow row = Assert.Single(register.Rows(Start, End));

        Assert.Equal("Vehicles", row.AssetType);
        Assert.Equal("Motor Vehicles", row.AssetAccount);
        Assert.Equal("Accumulated Depreciation - Vehicles", row.AccumDepAccount);
        Assert.Equal("Depreciation", row.DepExpenseAccount);
    }

    [Fact]
    public void Every_row_adds_up_across_its_own_roll_forward()
    {
        Register register = Mixed();

        List<FixedAssetPeriodRow> rows = register.Rows(Start, End);

        Assert.NotEmpty(rows);
        Assert.All(rows, row =>
        {
            Assert.Equal(row.OpeningCost + row.AdditionCost - row.DisposalCost, row.ClosingCost);
            Assert.Equal(
                row.OpeningAccumDep + row.Depreciation - row.DisposalAccumDep, row.ClosingAccumDep);
            Assert.Equal(row.OpeningCost - row.OpeningAccumDep, row.OpeningNbv);
            Assert.Equal(row.ClosingCost - row.ClosingAccumDep, row.ClosingNbv);
        });
    }

    // ---- the reconciliation ----------------------------------------------------

    [Fact]
    public void Each_account_a_category_names_appears_once_on_each_side()
    {
        Register register = Mixed();

        // A third category sharing Plant's accounts must not make them appear twice.
        register.Category(3, "Tools", PlantAccount, PlantAccumDep);

        List<FixedAssetReconciliationRow> rows = register.Reconciliation(Start, End);

        Assert.Equal(
            [
                (PlantAccount, "Register"), (PlantAccount, "Ledger"),
                (PlantAccumDep, "Register"), (PlantAccumDep, "Ledger"),
                (VehicleAccount, "Register"), (VehicleAccount, "Ledger"),
                (VehicleAccumDep, "Register"), (VehicleAccumDep, "Ledger"),
            ],
            rows.OrderBy(r => r.AccountId).ThenBy(r => r.SourceOrder)
                .Select(r => (r.AccountId, r.Source)));
    }

    [Fact]
    public void The_register_side_states_what_the_register_says_each_account_holds()
    {
        Register register = new();
        register.Asset(1, Plant, new DateOnly(2025, 1, 10), 12_000m);
        register.Charge(1, new DateOnly(2026, 3, 31), 2_000m);
        register.Charge(1, new DateOnly(2026, 6, 30), 1_000m);
        register.Asset(2, Plant, new DateOnly(2026, 8, 1), 3_000m);

        List<FixedAssetReconciliationRow> rows = register.Reconciliation(Start, End);

        FixedAssetReconciliationRow cost = rows.Single(r => r.AccountId == PlantAccount && r.Source == "Register");
        FixedAssetReconciliationRow accumDep = rows.Single(r => r.AccountId == PlantAccumDep && r.Source == "Register");

        Assert.Equal(12_000m, cost.OpeningCost);
        Assert.Equal(3_000m, cost.CostDebits);
        Assert.Equal(0m, cost.CostCredits);
        Assert.Equal(15_000m, cost.ClosingCost);
        Assert.Equal(0m, cost.OpeningAccumDep);

        Assert.Equal(0m, accumDep.OpeningCost);
        Assert.Equal(2_000m, accumDep.OpeningAccumDep);
        Assert.Equal(1_000m, accumDep.AccumDepCredits);
        Assert.Equal(0m, accumDep.AccumDepDebits);
        Assert.Equal(3_000m, accumDep.ClosingAccumDep);
        Assert.Equal(-3_000m, accumDep.ClosingBookValue);
    }

    [Fact]
    public void The_ledger_side_reads_each_account_by_its_own_normal_balance()
    {
        Register register = new();
        register.Asset(1, Plant, new DateOnly(2025, 1, 10), 12_000m);
        register.Post(PlantAccount, new DateOnly(2025, 1, 10), debit: 12_000m);
        register.Post(PlantAccount, new DateOnly(2026, 5, 1), credit: 500m);
        register.Post(PlantAccumDep, new DateOnly(2026, 3, 31), credit: 2_000m);
        register.Post(PlantAccumDep, new DateOnly(2026, 6, 30), credit: 1_000m);
        register.Post(PlantAccumDep, new DateOnly(2026, 7, 31), debit: 200m);
        register.Post(PlantAccount, End.AddDays(1), debit: 7_777m);
        register.Post(DepreciationExpense, new DateOnly(2026, 6, 30), debit: 1_000m);
        register.Post(Bank, new DateOnly(2026, 6, 30), credit: 12_000m);

        List<FixedAssetReconciliationRow> rows = register.Reconciliation(Start, End);

        FixedAssetReconciliationRow cost = rows.Single(r => r.AccountId == PlantAccount && r.Source == "Ledger");
        FixedAssetReconciliationRow accumDep = rows.Single(r => r.AccountId == PlantAccumDep && r.Source == "Ledger");

        Assert.Equal(12_000m, cost.OpeningCost);
        Assert.Equal(0m, cost.CostDebits);
        Assert.Equal(500m, cost.CostCredits);
        Assert.Equal(11_500m, cost.ClosingCost);
        Assert.Equal(12_000m, cost.OpeningBookValue);
        Assert.Equal(11_500m, cost.ClosingBookValue);

        Assert.Equal(2_000m, accumDep.OpeningAccumDep);
        Assert.Equal(1_000m, accumDep.AccumDepCredits);
        Assert.Equal(200m, accumDep.AccumDepDebits);
        Assert.Equal(2_800m, accumDep.ClosingAccumDep);
        Assert.Equal(0m, accumDep.ClosingCost);

        // Neither the expense account nor the bank is a category's asset or
        // accumulated depreciation account, so neither is reconciled.
        Assert.DoesNotContain(rows, r => r.AccountId is DepreciationExpense or Bank);
    }

    [Fact]
    public void An_account_nothing_has_posted_to_still_shows_its_ledger_row_at_zero()
    {
        // The case that exists today: registering an asset posts nothing, so the
        // ledger side of its accounts is empty and the gap has to be visible.
        Register register = new();
        register.Asset(1, Vehicles, new DateOnly(2025, 1, 10), 20_000m);

        List<FixedAssetReconciliationRow> rows = register.Reconciliation(Start, End);

        FixedAssetReconciliationRow ledger = rows.Single(r => r.AccountId == VehicleAccount && r.Source == "Ledger");
        FixedAssetReconciliationRow registered = rows.Single(r => r.AccountId == VehicleAccount && r.Source == "Register");

        Assert.Equal(0m, ledger.ClosingCost);
        Assert.Equal(20_000m, registered.ClosingCost);
    }

    [Fact]
    public void The_register_sides_book_values_total_the_registers_net_book_value()
    {
        Register register = Mixed();

        decimal registerNbv = register.Rows(Start, End).Sum(r => r.ClosingNbv);
        decimal reconciled = register.Reconciliation(Start, End)
            .Where(r => r.Source == "Register")
            .Sum(r => r.ClosingBookValue);

        Assert.Equal(registerNbv, reconciled);
    }

    // ---- the sources -----------------------------------------------------------

    public static TheoryData<IReportSource> FixedAssetSources =>
    [
        new DepreciationScheduleSource(),
        new DisposalScheduleSource(),
        new FixedAssetReconciliationSource(),
        new FixedAssetsScheduleSource(),
    ];

    [Theory]
    [MemberData(nameof(FixedAssetSources))]
    public void Every_fixed_asset_report_is_filed_under_fixed_assets_and_needs_accounting_view(
        IReportSource source)
    {
        Assert.Equal(ReportModule.FixedAssets, source.Module);
        Assert.Equal("accounting.view", source.RequiredPermission);
        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
    }

    [Theory]
    [MemberData(nameof(FixedAssetSources))]
    public void No_fixed_asset_report_declares_a_column_the_register_cannot_fill(
        IReportSource source)
    {
        // reports.json lists these, and the register has no field for any of them.
        string[] unfillable = ["brand", "outlet", "warrantyExpiry", "costLimit", "averagingMethod", "avgMethod"];

        Assert.DoesNotContain(source.Columns, c => unfillable.Contains(c.Key));
    }

    [Fact]
    public void The_reconciliation_totals_nothing()
    {
        // A total over both sides adds the register to the ledger.
        Assert.DoesNotContain(new FixedAssetReconciliationSource().Columns, c => c.IsAggregatable);
    }

    // ---- fixtures ----------------------------------------------------------------

    /// <summary>
    /// A register with a little of everything in it: held all year, bought in the
    /// year, disposed of at a loss, and a second category with its own accounts.
    /// </summary>
    private static Register Mixed()
    {
        Register register = new();

        register.Asset(1, Plant, new DateOnly(2025, 1, 10), 12_000m);
        register.Charge(1, new DateOnly(2026, 3, 31), 2_000m);
        register.Charge(1, new DateOnly(2026, 6, 30), 1_000m);

        register.Asset(2, Plant, new DateOnly(2026, 8, 1), 3_000m);
        register.Charge(2, new DateOnly(2026, 12, 31), 250m);

        register.Asset(3, Vehicles, new DateOnly(2024, 6, 1), 20_000m, FixedAssetStatus.Disposed);
        register.Charge(3, new DateOnly(2025, 6, 1), 5_000m);
        register.Charge(3, new DateOnly(2026, 6, 1), 1_000m);
        register.Dispose(3, new DateOnly(2026, 11, 1), 10_000m);

        register.Asset(4, Vehicles, new DateOnly(2026, 1, 1), 8_000m);

        return register;
    }

    private sealed class Register
    {
        private readonly List<FixedAssetRead> _assets = [];
        private readonly List<FixedAssetCategoryRead> _categories = [];
        private readonly List<DepreciationScheduleRead> _schedules = [];
        private readonly List<AssetTransactionRead> _transactions = [];
        private readonly List<JournalLedgerRead> _ledger = [];
        private readonly List<AccountRead> _accounts =
        [
            Account(PlantAccount, "1500", "Plant and Machinery"),
            Account(PlantAccumDep, "1501", "Accumulated Depreciation - Plant"),
            Account(DepreciationExpense, "6100", "Depreciation"),
            Account(VehicleAccount, "1520", "Motor Vehicles"),
            Account(VehicleAccumDep, "1521", "Accumulated Depreciation - Vehicles"),
            Account(Bank, "1100", "Bank"),
        ];

        public Register()
        {
            Category(Plant, "Plant", PlantAccount, PlantAccumDep);
            Category(Vehicles, "Vehicles", VehicleAccount, VehicleAccumDep);
        }

        public void Category(long id, string name, long assetAccount, long accumDepAccount) =>
            _categories.Add(new FixedAssetCategoryRead
            {
                FixedAssetCategoryId = id,
                CategoryName = name,
                AssetAccountId = assetAccount,
                AccumulatedDepreciationAccountId = accumDepAccount,
                DepreciationExpenseAccountId = DepreciationExpense,
            });

        public void Asset(
            long id,
            long category,
            DateOnly bought,
            decimal price,
            FixedAssetStatus status = FixedAssetStatus.Active) =>
            _assets.Add(new FixedAssetRead
            {
                FixedAssetId = id,
                FixedAssetCategoryId = category,
                AssetCode = $"FA-{id:000}",
                AssetName = $"Asset {id}",
                PurchaseDate = bought,
                PurchasePrice = price,
                Status = status,
            });

        public long ScheduleId(long asset, DepreciationScheduleType type) =>
            asset * 10 + (long)type;

        public void Schedule(
            long asset,
            DepreciationScheduleType type,
            DepreciationMethod method,
            decimal rate,
            int life,
            decimal residual) =>
            _schedules.Add(new DepreciationScheduleRead
            {
                DepreciationScheduleId = ScheduleId(asset, type),
                FixedAssetId = asset,
                ScheduleType = type,
                DepreciationMethod = method,
                Rate = rate,
                UsefulLifeYears = life,
                DepreciationStartDate = new DateOnly(2025, 2, 1),
                SalvageValue = residual,
            });

        public void Charge(long asset, DateOnly on, decimal amount, long? scheduleId = null) =>
            _transactions.Add(new AssetTransactionRead
            {
                AssetTransactionId = _transactions.Count + 1,
                FixedAssetId = asset,
                TransactionType = AssetTransactionType.Depreciation,
                DepreciationScheduleId = scheduleId,
                TransactionDate = on,
                Amount = amount,
            });

        public void Dispose(long asset, DateOnly on, decimal proceeds) =>
            _transactions.Add(new AssetTransactionRead
            {
                AssetTransactionId = _transactions.Count + 1,
                FixedAssetId = asset,
                TransactionType = AssetTransactionType.Disposal,
                TransactionDate = on,
                Amount = proceeds,
            });

        public void Post(long account, DateOnly on, decimal debit = 0m, decimal credit = 0m) =>
            _ledger.Add(new JournalLedgerRead
            {
                LedgerId = _ledger.Count + 1,
                AccountId = account,
                LedgerDate = on,
                DebitAmountBase = debit,
                CreditAmountBase = credit,
                TransactionTypeCode = "JRN",
                CurrencyCode = "INR",
            });

        public List<FixedAssetPeriodRow> Rows(DateOnly? start, DateOnly? end) =>
            [.. Query(start, end)];

        public List<FixedAssetReconciliationRow> Reconciliation(DateOnly? start, DateOnly? end) =>
            [.. FixedAssetReconciliationSource.Rows(
                Query(start, end),
                _categories.AsQueryable(),
                _accounts.AsQueryable(),
                _ledger.AsQueryable(),
                start,
                end)];

        private IQueryable<FixedAssetPeriodRow> Query(DateOnly? start, DateOnly? end) =>
            FixedAssetRegister.Rows(
                _assets.AsQueryable(),
                _categories.AsQueryable(),
                _schedules.AsQueryable(),
                _transactions.AsQueryable(),
                _accounts.AsQueryable(),
                start,
                end);

        private static AccountRead Account(long id, string code, string name) => new()
        {
            AccountId = id,
            AccountCode = code,
            AccountName = name,
            IsActive = true,
        };
    }
}
