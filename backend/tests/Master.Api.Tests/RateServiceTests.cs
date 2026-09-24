using Master.Api.Services;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Exchange and metal rate history (TK-24): a rate entered for a date answers
/// for that date and every later one until a newer rate is entered, a manual
/// row outranks a fetched one on the same date, and one source cannot hold two
/// rows for the same key and date.
///
/// The rows are global, so each test works on a pair or purity of its own and
/// clears it first; a rerun against the same database starts clean.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class RateServiceTests
{
    private readonly AdminFixture _admin;

    public RateServiceTests(AdminFixture admin) => _admin = admin;

    private static readonly DateOnly Monday = new(2026, 9, 21);

    private static async Task ClearPairAsync(AdminDbContext db, string from, string to) =>
        await db.ExchangeRates.Where(r => r.FromCurrencyCode == from && r.ToCurrencyCode == to).ExecuteDeleteAsync();

    private static async Task ClearPurityAsync(AdminDbContext db, string purity) =>
        await db.MetalRates.Where(r => r.PurityCode == purity).ExecuteDeleteAsync();

    [SkippableFact]
    public async Task A_rate_answers_for_its_date_and_every_later_date_until_a_newer_one()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await ClearPairAsync(db, "QXA", "QXB");

        db.ExchangeRates.AddRange(
            new ExchangeRate { FromCurrencyCode = "QXA", ToCurrencyCode = "QXB", RateDate = Monday, Rate = 83.10m, Source = RateSource.Rbi },
            new ExchangeRate { FromCurrencyCode = "QXA", ToCurrencyCode = "QXB", RateDate = Monday.AddDays(3), Rate = 83.40m, Source = RateSource.Rbi });
        await db.SaveChangesAsync();

        var rates = new RateService(db);

        Assert.Null(await rates.LatestExchangeAsync("QXA", "QXB", Monday.AddDays(-1), default));
        Assert.Equal(83.10m, (await rates.LatestExchangeAsync("QXA", "QXB", Monday, default))!.Rate);
        Assert.Equal(83.10m, (await rates.LatestExchangeAsync("qxa", "qxb", Monday.AddDays(2), default))!.Rate);
        Assert.Equal(83.40m, (await rates.LatestExchangeAsync("QXA", "QXB", Monday.AddDays(3), default))!.Rate);
        Assert.Equal(83.40m, (await rates.LatestExchangeAsync("QXA", "QXB", Monday.AddDays(30), default))!.Rate);

        // The pair is directional: nothing is stored the other way round.
        Assert.Null(await rates.LatestExchangeAsync("QXB", "QXA", Monday, default));
    }

    [SkippableFact]
    public async Task A_manual_rate_outranks_a_fetched_one_on_the_same_date()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await ClearPairAsync(db, "QXC", "QXD");

        db.ExchangeRates.AddRange(
            new ExchangeRate { FromCurrencyCode = "QXC", ToCurrencyCode = "QXD", RateDate = Monday, Rate = 1.5m, Source = RateSource.Rbi },
            new ExchangeRate { FromCurrencyCode = "QXC", ToCurrencyCode = "QXD", RateDate = Monday, Rate = 1.6m, Source = RateSource.Manual });
        await db.SaveChangesAsync();

        ExchangeRateDto? rate = await new RateService(db).LatestExchangeAsync("QXC", "QXD", Monday, default);

        Assert.Equal(1.6m, rate!.Rate);
        Assert.Equal("Manual", rate.Source);
    }

    [SkippableFact]
    public async Task One_source_cannot_hold_two_rows_for_the_same_pair_and_date()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await ClearPairAsync(db, "QXE", "QXF");

        db.ExchangeRates.AddRange(
            new ExchangeRate { FromCurrencyCode = "QXE", ToCurrencyCode = "QXF", RateDate = Monday, Rate = 2m, Source = RateSource.Rbi },
            new ExchangeRate { FromCurrencyCode = "QXE", ToCurrencyCode = "QXF", RateDate = Monday, Rate = 3m, Source = RateSource.Rbi });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [SkippableFact]
    public async Task One_source_cannot_hold_two_metal_rows_for_the_same_purity_and_date()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await ClearPurityAsync(db, "QX1");

        db.MetalRates.AddRange(
            new MetalRate { Metal = Metal.Gold, PurityCode = "QX1", RateDate = Monday, RatePerGram = 7000m, Source = RateSource.Ibja },
            new MetalRate { Metal = Metal.Gold, PurityCode = "QX1", RateDate = Monday, RatePerGram = 7100m, Source = RateSource.Ibja });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [SkippableFact]
    public async Task Entering_the_same_manual_rate_again_corrects_it_rather_than_adding_a_row()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        DateOnly date = new(1999, 1, 4);
        await db.ExchangeRates.Where(r => r.FromCurrencyCode == "USD" && r.ToCurrencyCode == "INR" && r.RateDate == date).ExecuteDeleteAsync();
        var rates = new RateService(db);

        Assert.Equal(RateSaveOutcome.Ok, await rates.SetExchangeAsync(
            new SaveExchangeRateRequest { FromCurrencyCode = "usd", ToCurrencyCode = "INR", RateDate = date, Rate = 42.5m }, default));
        Assert.Equal(RateSaveOutcome.Ok, await rates.SetExchangeAsync(
            new SaveExchangeRateRequest { FromCurrencyCode = "USD", ToCurrencyCode = "INR", RateDate = date, Rate = 42.6m }, default));

        ExchangeRate row = await db.ExchangeRates.AsNoTracking()
            .SingleAsync(r => r.FromCurrencyCode == "USD" && r.ToCurrencyCode == "INR" && r.RateDate == date);
        Assert.Equal(42.6m, row.Rate);
        Assert.Equal(RateSource.Manual, row.Source);
    }

    [SkippableFact]
    public async Task Unknown_or_identical_currencies_and_unknown_metals_are_refused()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        var rates = new RateService(db);

        Assert.Equal(RateSaveOutcome.UnknownCurrency, await rates.SetExchangeAsync(
            new SaveExchangeRateRequest { FromCurrencyCode = "QQQ", ToCurrencyCode = "INR", RateDate = Monday, Rate = 1m }, default));
        Assert.Equal(RateSaveOutcome.SameCurrency, await rates.SetExchangeAsync(
            new SaveExchangeRateRequest { FromCurrencyCode = "INR", ToCurrencyCode = "INR", RateDate = Monday, Rate = 1m }, default));
        Assert.Equal(RateSaveOutcome.UnknownMetal, await rates.SetMetalAsync(
            new SaveMetalRateRequest { Metal = "Copper", PurityCode = "999", RateDate = Monday, RatePerGram = 1m }, default));
    }

    [SkippableFact]
    public async Task A_currency_is_worth_one_of_itself_with_nothing_on_file()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();

        Assert.Equal(1m, (await new RateService(db).LatestExchangeAsync("INR", "INR", Monday, default))!.Rate);
    }

    [SkippableFact]
    public async Task Metal_rates_follow_the_same_on_or_before_rule_and_only_manual_rows_can_be_removed()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await ClearPurityAsync(db, "QX2");

        var fetched = new MetalRate { Metal = Metal.Gold, PurityCode = "QX2", RateDate = Monday, RatePerGram = 7000m, Source = RateSource.Ibja };
        db.MetalRates.Add(fetched);
        await db.SaveChangesAsync();

        var rates = new RateService(db);
        Assert.Equal(RateSaveOutcome.Ok, await rates.SetMetalAsync(
            new SaveMetalRateRequest { Metal = "gold", PurityCode = "qx2", RateDate = Monday.AddDays(1), RatePerGram = 7050m }, default));

        Assert.Equal(7000m, (await rates.LatestMetalAsync(Metal.Gold, "QX2", Monday, default))!.RatePerGram);
        Assert.Equal(7050m, (await rates.LatestMetalAsync(Metal.Gold, "QX2", Monday.AddDays(5), default))!.RatePerGram);
        Assert.Null(await rates.LatestMetalAsync(Metal.Silver, "QX2", Monday.AddDays(5), default));

        Assert.Equal(RateSaveOutcome.NotManual, await rates.DeleteMetalAsync(fetched.MetalRateId, default));

        long manualId = (await rates.LatestMetalAsync(Metal.Gold, "QX2", Monday.AddDays(5), default))!.MetalRateId;
        Assert.Equal(RateSaveOutcome.Ok, await rates.DeleteMetalAsync(manualId, default));
        Assert.Equal(7000m, (await rates.LatestMetalAsync(Metal.Gold, "QX2", Monday.AddDays(5), default))!.RatePerGram);
    }
}
