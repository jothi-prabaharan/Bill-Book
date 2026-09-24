using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using Accounting.Api.Controllers;
using Accounting.Api.Services;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// The register's rules that need no database: one month's charge under each
/// method, which schedules are accepted, and the house rules on the controller
/// (TK-11).
/// </summary>
public sealed class FixedAssetRulesTests
{
    private static DepreciationSchedule Schedule(
        DepreciationMethod method, decimal rate = 0m, int life = 0, decimal salvage = 0m) => new()
        {
            DepreciationMethod = method,
            Rate = rate,
            UsefulLifeYears = life,
            SalvageValue = salvage,
        };

    [Theory]
    [InlineData(1200, 3, 0, 0, 33.33)]     // life wins: 1,200 ÷ 3 ÷ 12
    [InlineData(1200, 0, 10, 0, 10.00)]    // no life: 1,200 × 10% ÷ 12
    [InlineData(1200, 3, 0, 120, 30.00)]   // salvage comes off first: 1,080 ÷ 3 ÷ 12
    public void Straight_line_charges_a_level_monthly_amount(
        decimal cost, int life, decimal rate, decimal salvage, decimal expected)
    {
        decimal charge = DepreciationService.MonthlyCharge(
            cost, Schedule(DepreciationMethod.StraightLine, rate, life, salvage), chargedToDate: 0m);

        Assert.Equal(expected, charge);
    }

    [Theory]
    [InlineData(0, 20.00)]      // 1,200 × 20% ÷ 12
    [InlineData(20, 19.67)]     // 1,180 × 20% ÷ 12
    [InlineData(600, 10.00)]    // 600 × 20% ÷ 12
    public void Written_down_value_charges_the_rate_on_the_book_value_left(decimal chargedToDate, decimal expected)
    {
        decimal charge = DepreciationService.MonthlyCharge(
            1200m, Schedule(DepreciationMethod.WrittenDownValue, rate: 20m), chargedToDate);

        Assert.Equal(expected, charge);
    }

    [Fact]
    public void No_charge_takes_an_asset_below_its_salvage_value()
    {
        DepreciationSchedule schedule = Schedule(DepreciationMethod.StraightLine, life: 3, salvage: 100m);

        // 1,100 depreciable, 1,090 already charged: 10 left, not 30.56.
        Assert.Equal(10m, DepreciationService.MonthlyCharge(1200m, schedule, chargedToDate: 1090m));
        Assert.Equal(0m, DepreciationService.MonthlyCharge(1200m, schedule, chargedToDate: 1100m));
    }

    [Fact]
    public void Schedules_that_could_never_charge_or_repeat_a_type_are_refused()
    {
        CreateDepreciationScheduleRequest Books(DepreciationMethod m, decimal rate = 0, int life = 0, decimal salvage = 0) =>
            new() { ScheduleType = DepreciationScheduleType.Books, DepreciationMethod = m, Rate = rate, UsefulLifeYears = life, SalvageValue = salvage };

        Assert.True(FixedAssetService.SchedulesAreValid([], 1000m));
        Assert.True(FixedAssetService.SchedulesAreValid([Books(DepreciationMethod.StraightLine, life: 5)], 1000m));
        Assert.True(FixedAssetService.SchedulesAreValid([Books(DepreciationMethod.WrittenDownValue, rate: 15)], 1000m));

        Assert.False(FixedAssetService.SchedulesAreValid([Books(DepreciationMethod.StraightLine)], 1000m));
        Assert.False(FixedAssetService.SchedulesAreValid([Books(DepreciationMethod.WrittenDownValue)], 1000m));
        Assert.False(FixedAssetService.SchedulesAreValid([Books(DepreciationMethod.WrittenDownValue, rate: 100)], 1000m));
        Assert.False(FixedAssetService.SchedulesAreValid([Books(DepreciationMethod.StraightLine, life: 5, salvage: 1001)], 1000m));
        Assert.False(FixedAssetService.SchedulesAreValid(
            [Books(DepreciationMethod.StraightLine, life: 5), Books(DepreciationMethod.WrittenDownValue, rate: 15)], 1000m));
    }

    [Theory]
    [InlineData(1200, 200, 1100, 100, 0)]    // sold for 100 over book value: a gain
    [InlineData(1200, 200, 900, 0, 100)]     // 100 under: a loss
    [InlineData(1200, 200, 1000, 0, 0)]      // exactly book value: no gain/loss leg
    [InlineData(1200, 0, 0, 0, 1200)]        // scrapped undepreciated: all of it lost
    public void A_disposal_balances_and_puts_the_difference_to_gain_or_loss(
        decimal cost, decimal accumulated, decimal proceeds, decimal gain, decimal loss)
    {
        var lines = DisposalLinesFor(cost, accumulated, proceeds);

        decimal debits = proceeds + lines.Sum(l => l.DebitAmount);
        Assert.Equal(debits, lines.Sum(l => l.CreditAmount));

        Assert.Equal(cost, lines.Where(l => l.AccountId == AssetAccount).Sum(l => l.CreditAmount));
        Assert.Equal(accumulated, lines.Where(l => l.AccountId == AccumulatedAccount).Sum(l => l.DebitAmount));
        Assert.Equal(gain, lines.Where(l => l.AccountId == GainLossAccount).Sum(l => l.CreditAmount));
        Assert.Equal(loss, lines.Where(l => l.AccountId == GainLossAccount).Sum(l => l.DebitAmount));
        Assert.All(lines, l => Assert.True((l.DebitAmount == 0) != (l.CreditAmount == 0)));
    }

    [Fact]
    public void An_undepreciated_asset_writes_back_no_accumulated_depreciation()
    {
        Assert.DoesNotContain(DisposalLinesFor(1200m, 0m, 1200m), l => l.AccountId == AccumulatedAccount);
    }

    [Fact]
    public void A_bill_assets_code_is_the_bill_number_and_line()
    {
        Assert.Equal("BIL/26-27/0042-3", FixedAssetService.BillAssetCode("BIL/26-27/0042", 3));
        Assert.Equal(50, FixedAssetService.BillAssetCode(new string('X', 60), 1).Length);
    }

    private const long AssetAccount = 1;
    private const long AccumulatedAccount = 2;
    private const long GainLossAccount = 3;

    private static SaveJournalLineRequest[] DisposalLinesFor(decimal cost, decimal accumulated, decimal proceeds) =>
        FixedAssetService.DisposalLines(
            cost, accumulated, proceeds, AssetAccount, AccumulatedAccount, GainLossAccount, "Disposal").ToArray();

    [Fact]
    public void Every_action_takes_a_cancellation_token()
    {
        MethodInfo[] actions = Actions();

        Assert.NotEmpty(actions);
        Assert.All(actions, a =>
            Assert.Contains(a.GetParameters(), p => p.ParameterType == typeof(CancellationToken)));
    }

    [Fact]
    public void Dispose_takes_a_long_id_and_both_sign_offs_need_approve()
    {
        MethodInfo dispose = typeof(FixedAssetsController).GetMethod(nameof(FixedAssetsController.DisposeAsset))!;
        MethodInfo schedules = typeof(FixedAssetsController).GetMethod(nameof(FixedAssetsController.SetSchedules))!;

        Assert.Equal("{id:long}/schedules", schedules.GetCustomAttribute<HttpPutAttribute>()!.Template);
        MethodInfo run = typeof(FixedAssetsController).GetMethod(nameof(FixedAssetsController.RunDepreciation))!;

        Assert.Equal("{id:long}/dispose", dispose.GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.Equal("approve", dispose.GetCustomAttribute<PermissionActionAttribute>()!.Action);
        Assert.Equal("approve", run.GetCustomAttribute<PermissionActionAttribute>()!.Action);
    }

    [Fact]
    public void The_controller_holds_no_database_context()
    {
        // Every rule is the service's; the controller only maps outcomes.
        Assert.DoesNotContain(
            typeof(FixedAssetsController).GetConstructors().Single().GetParameters(),
            p => p.ParameterType.Name.EndsWith("DbContext", StringComparison.Ordinal));
    }

    private static MethodInfo[] Actions() =>
        typeof(FixedAssetsController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes().Any(a => a is Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute))
            .ToArray();
}
