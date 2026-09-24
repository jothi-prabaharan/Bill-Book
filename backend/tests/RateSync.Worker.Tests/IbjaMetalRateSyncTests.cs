using Master.Entity.Enums;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RateSync.Worker.Ibja;
using Shared.Kernel.Errors;
using Shared.Kernel.Interfaces;
using Xunit;

namespace RateSync.Worker.Tests;

[Collection(nameof(AdminCollection))]
public sealed class IbjaMetalRateSyncTests
{
    private readonly AdminFixture _admin;

    public IbjaMetalRateSyncTests(AdminFixture admin) => _admin = admin;

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubSecretStore(string? apiKey) : ISecretStore
    {
        public Task<string> GetSecretAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(name == "IbjaApiKey" && apiKey != null ? apiKey : "");

        public Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubIbjaClient(IbjaRates? response, Exception? error = null) : IIbjaClient
    {
        public Task<IbjaRates> FetchRatesAsync(string apiKey, CancellationToken ct)
        {
            if (error != null) throw error;
            if (response != null) return Task.FromResult(response);
            throw new InvalidOperationException("Stub configured with no response or error.");
        }
    }

    [Fact]
    public async Task Parse_a_recorded_IBJA_response()
    {
        DateTimeOffset now = new DateTimeOffset(2099, 1, 15, 14, 0, 0, ExchangeRateSync.IndiaOffset);
        DateOnly today = new DateOnly(2099, 1, 15);
        await ClearRatesAsync(today);

        var rates = new IbjaRates
        {
            Date = today,
            Rates = new Dictionary<string, decimal>
            {
                { "gold_24k_999", 75000m },
                { "gold_22k_916", 70000m },
                { "silver_999", 90000m }
            }
        };

        var sync = new IbjaMetalRateSync(
            _admin.CreateContext(),
            new StubIbjaClient(rates),
            new StubSecretStore("test-key"),
            new FixedClock(now),
            NullLogger<IbjaMetalRateSync>.Instance);

        ExchangeRateSyncOutcome outcome = await sync.RunAsync(default);

        Assert.Equal(ExchangeRateSyncOutcome.Succeeded, outcome);

        await using AdminDbContext db = _admin.CreateContext();
        List<MetalRate> written = await db.MetalRates.Where(r => r.RateDate == today && r.Source == RateSource.Ibja).ToListAsync();

        Assert.Equal(3, written.Count);
        
        MetalRate gold24 = written.Single(r => r.Metal == Metal.Gold && r.PurityCode == "24K");
        Assert.Equal(75000m, gold24.RatePerGram);

        MetalRate gold22 = written.Single(r => r.Metal == Metal.Gold && r.PurityCode == "916");
        Assert.Equal(70000m, gold22.RatePerGram);

        MetalRate silver = written.Single(r => r.Metal == Metal.Silver && r.PurityCode == "999");
        Assert.Equal(90000m, silver.RatePerGram);
        
        RateFetchRun run = await db.RateFetchRuns.SingleAsync(r => r.Source == RateSource.Ibja && r.RunDate == today);
        Assert.Equal(RateFetchStatus.Succeeded, run.Status);
        Assert.Equal(3, run.RatesWritten);
    }

    [Fact]
    public async Task A_second_run_on_the_same_day_writes_nothing()
    {
        DateTimeOffset now = new DateTimeOffset(2099, 1, 16, 14, 0, 0, ExchangeRateSync.IndiaOffset);
        DateOnly today = new DateOnly(2099, 1, 16);
        await ClearRatesAsync(today);

        var rates = new IbjaRates
        {
            Date = today,
            Rates = new Dictionary<string, decimal> { { "gold_24k_999", 75000m } }
        };

        var sync = new IbjaMetalRateSync(
            _admin.CreateContext(),
            new StubIbjaClient(rates),
            new StubSecretStore("test-key"),
            new FixedClock(now),
            NullLogger<IbjaMetalRateSync>.Instance);

        ExchangeRateSyncOutcome run1 = await sync.RunAsync(default);
        Assert.Equal(ExchangeRateSyncOutcome.Succeeded, run1);

        ExchangeRateSyncOutcome run2 = await sync.RunAsync(default);
        Assert.Equal(ExchangeRateSyncOutcome.AlreadyDone, run2);

        await using AdminDbContext db = _admin.CreateContext();
        int rows = await db.MetalRates.CountAsync(r => r.RateDate == today && r.Source == RateSource.Ibja);
        Assert.Equal(1, rows);

        int runRows = await db.RateFetchRuns.CountAsync(r => r.Source == RateSource.Ibja && r.RunDate == today);
        Assert.Equal(1, runRows); // The second run didn't write a row because it aborted early
    }

    [Fact]
    public async Task A_failed_run_leaves_an_open_log_and_writes_nothing()
    {
        DateTimeOffset now = new DateTimeOffset(2099, 1, 17, 14, 0, 0, ExchangeRateSync.IndiaOffset);
        DateOnly today = new DateOnly(2099, 1, 17);
        await ClearRatesAsync(today);

        var sync = new IbjaMetalRateSync(
            _admin.CreateContext(),
            new StubIbjaClient(null, new InvalidOperationException("Network down")),
            new StubSecretStore("test-key"),
            new FixedClock(now),
            NullLogger<IbjaMetalRateSync>.Instance);

        ExchangeRateSyncOutcome run1 = await sync.RunAsync(default);
        Assert.Equal(ExchangeRateSyncOutcome.Failed, run1);

        await using AdminDbContext db = _admin.CreateContext();
        int rows = await db.MetalRates.CountAsync(r => r.RateDate == today && r.Source == RateSource.Ibja);
        Assert.Equal(0, rows);

        RateFetchRun run = await db.RateFetchRuns.SingleAsync(r => r.Source == RateSource.Ibja && r.RunDate == today);
        Assert.Equal(RateFetchStatus.Failed, run.Status);
        Assert.Equal(0, run.RatesWritten);
        Assert.Equal(ErrorFollowUpStatus.Open, run.FollowUpStatus);
        Assert.Contains("Network down", run.Error!);
    }
    
    [Fact]
    public async Task HttpIbjaClient_parses_json_response()
    {
        var json = @"{
          ""success"": true,
          ""date"": ""2026-09-24"",
          ""rates"": {
            ""gold_999"": 76500.00,
            ""silver_999"": 91500.00
          }
        }";

        var handler = new StubHttpMessageHandler(json);
        var client = new HttpIbjaClient(new HttpClient(handler) { BaseAddress = new Uri("https://test/") });

        IbjaRates response = await client.FetchRatesAsync("test-key", default);

        Assert.Equal(new DateOnly(2026, 9, 24), response.Date);
        Assert.Equal(76500.00m, response.Rates["gold_999"]);
        Assert.Equal(91500.00m, response.Rates["silver_999"]);
    }

    private class StubHttpMessageHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(content)
            });
        }
    }

    private async Task ClearRatesAsync(DateOnly date)
    {
        await using AdminDbContext db = _admin.CreateContext();
        await db.MetalRates.Where(r => r.RateDate == date).ExecuteDeleteAsync();
        await db.RateFetchRuns.Where(r => r.RunDate == date).ExecuteDeleteAsync();
    }
}
