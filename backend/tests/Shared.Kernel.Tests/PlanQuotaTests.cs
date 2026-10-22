using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Kernel.Errors;
using Shared.Kernel.Licensing;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// The pure half of a plan's monthly document limit (D-69, TK-210): which month a
/// document counts in, the words of a refusal, how the refusal is answered, and
/// how the limits are read from Master. The counting itself, which needs a real
/// database, is in the Sales and Purchase suites' <c>MonthlyQuotaTests</c>.
///
/// Written for TK-210 and not run by the AI that wrote it (docs/Task_Details.md 0.5).
/// </summary>
public sealed class PlanQuotaTests
{
    private static readonly PlanLimitResponse Refusal = new()
    {
        Message = PlanLimitReachedException.MessageFor("Professional", PlanLimitKind.SalesTransactions, 1000),
        Kind = PlanLimitKind.SalesTransactions,
        Plan = "Professional",
        Limit = 1000,
        Used = 1000,
    };

    // ---- Which month a document counts in -----------------------------------

    [Theory]
    [InlineData("2026-10-15T10:00:00Z", "2026-10-01")]
    [InlineData("2026-10-31T18:29:59Z", "2026-10-01")] // 23:59:59 IST on the 31st
    [InlineData("2026-10-31T18:30:00Z", "2026-11-01")] // midnight IST: November
    [InlineData("2026-09-30T18:30:00Z", "2026-10-01")] // 00:00 IST on 1 October, still 30 September in UTC
    [InlineData("2026-12-31T18:30:00Z", "2027-01-01")] // the year turns at the same moment
    [InlineData("2026-02-28T18:30:00Z", "2026-03-01")]
    public void QUOTA_K01_a_document_counts_in_the_indian_calendar_month(string moment, string month) =>
        Assert.Equal(DateOnly.Parse(month), QuotaPeriod.StartOf(DateTimeOffset.Parse(moment)));

    [Fact]
    public void QUOTA_K02_the_offset_the_moment_is_given_in_makes_no_difference()
    {
        // The same instant, written four ways.
        DateTimeOffset utc = new(2026, 10, 31, 18, 30, 0, TimeSpan.Zero);
        DateTimeOffset paris = new(2026, 10, 31, 19, 30, 0, TimeSpan.FromHours(1));
        DateTimeOffset india = new(2026, 11, 1, 0, 0, 0, TimeSpan.FromMinutes(330));
        DateTimeOffset newYork = new(2026, 10, 31, 14, 30, 0, TimeSpan.FromHours(-4));

        Assert.All(new[] { utc, paris, india, newYork },
            moment => Assert.Equal(new DateOnly(2026, 11, 1), QuotaPeriod.StartOf(moment)));
    }

    // ---- What the plan allows, and what a refusal says ------------------------

    [Fact]
    public void QUOTA_K03_each_kind_reads_its_own_limit_and_users_are_not_a_monthly_count()
    {
        var limits = new PlanLimits("Professional", 1000, 250);

        Assert.Equal(1000, limits.MonthlyFor(PlanLimitKind.SalesTransactions));
        Assert.Equal(250, limits.MonthlyFor(PlanLimitKind.PurchaseTransactions));
        Assert.Null(limits.MonthlyFor(PlanLimitKind.Users));
        Assert.Null(new PlanLimits("Elite", null, null).MonthlyFor(PlanLimitKind.SalesTransactions));
    }

    [Theory]
    [InlineData("Professional", PlanLimitKind.SalesTransactions, 1000,
        "Professional plan Sales transaction limit reached (maximum 1,000 Sales transactions this month). Upgrade to the Elite Plan for unlimited transactions.")]
    [InlineData("Trial", PlanLimitKind.PurchaseTransactions, 1000,
        "Trial plan Purchase transaction limit reached (maximum 1,000 Purchase transactions this month). Upgrade to the Elite Plan for unlimited transactions.")]
    [InlineData("Professional", PlanLimitKind.SalesTransactions, 12345,
        "Professional plan Sales transaction limit reached (maximum 12,345 Sales transactions this month). Upgrade to the Elite Plan for unlimited transactions.")]
    public void QUOTA_K04_the_refusal_names_the_plan_the_side_and_the_figure(string plan, PlanLimitKind kind, int limit, string message) =>
        Assert.Equal(message, PlanLimitReachedException.MessageFor(plan, kind, limit));

    [Fact]
    public void QUOTA_K05_a_refusal_is_found_whether_it_is_wrapped_or_not()
    {
        var refusal = new PlanLimitReachedException(Refusal);
        var wrapped = new InvalidOperationException("The save failed.", new AggregateException(refusal));

        Assert.Same(refusal, PlanLimitReachedException.Find(refusal));
        Assert.Same(refusal, PlanLimitReachedException.Find(wrapped));
        Assert.Null(PlanLimitReachedException.Find(new InvalidOperationException("Something else.")));
        Assert.Null(PlanLimitReachedException.Find(null));
        Assert.Equal(Refusal.Message, refusal.Message);
    }

    [Fact]
    public void QUOTA_K06_the_refusal_goes_over_the_wire_with_a_code_a_screen_can_branch_on()
    {
        string json = JsonSerializer.Serialize(Refusal, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement body = document.RootElement;

        // The kind travels as its name, so a screen reads "SalesTransactions" and not a number.
        Assert.Equal("PlanLimitReached", body.GetProperty("code").GetString());
        Assert.Equal("SalesTransactions", body.GetProperty("kind").GetString());
        Assert.Equal("Professional", body.GetProperty("plan").GetString());
        Assert.Equal(1000, body.GetProperty("limit").GetInt32());
        Assert.Equal(1000, body.GetProperty("used").GetInt32());
        Assert.Equal(Refusal.Message, body.GetProperty("message").GetString());
    }

    // ---- How a refusal is answered ----------------------------------------------

    private sealed class RecordingStore : IErrorLogStore
    {
        public List<ErrorLog> Recorded { get; } = [];

        public Task<Guid?> RecordAsync(ErrorLog entry, CancellationToken ct)
        {
            Recorded.Add(entry);
            return Task.FromResult<Guid?>(entry.ErrorReference);
        }
    }

    private sealed class ServicesWith(object service) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType.IsInstanceOfType(service) ? service : null;
    }

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Test.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task QUOTA_K07_the_handler_answers_a_409_with_the_limit_and_writes_nothing_to_the_error_log(string environment)
    {
        var store = new RecordingStore();
        var handler = new GlobalExceptionHandler(new Environment(environment), NullLogger<GlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext { RequestServices = new ServicesWith(store) };
        context.Request.Method = "POST";
        context.Request.Path = "/api/sales/invoices";
        var body = new MemoryStream();
        context.Response.Body = body;

        // Wrapped, as an exception out of an interceptor can arrive.
        var thrown = new InvalidOperationException("Could not save.", new PlanLimitReachedException(Refusal));

        Assert.True(await handler.TryHandleAsync(context, thrown, CancellationToken.None));

        body.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(body);
        JsonElement answer = document.RootElement;

        Assert.Equal(409, context.Response.StatusCode);
        Assert.Equal("PlanLimitReached", answer.GetProperty("code").GetString());
        Assert.Equal(Refusal.Message, answer.GetProperty("message").GetString());
        Assert.Equal("SalesTransactions", answer.GetProperty("kind").GetString());

        // An expected answer is not a failure: no row for an operator to follow up.
        Assert.Empty(store.Recorded);
    }

    [Fact]
    public void QUOTA_K08_the_filter_answers_the_same_way()
    {
        var filter = new ApiExceptionFilter(new Environment("Production"), NullLogger<ApiExceptionFilter>.Instance);
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(
            httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var exceptionContext = new ExceptionContext(actionContext, [])
        {
            Exception = new PlanLimitReachedException(Refusal),
        };

        filter.OnException(exceptionContext);

        Assert.True(exceptionContext.ExceptionHandled);
        ConflictObjectResult result = Assert.IsType<ConflictObjectResult>(exceptionContext.Result);
        Assert.Same(Refusal, result.Value);
    }

    // ---- Reading the limits from Master ---------------------------------------------

    private sealed class CountingHandler(Func<HttpResponseMessage> answer) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public string? LastPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastPath = request.RequestUri?.AbsolutePath;
            return Task.FromResult(answer());
        }
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpPlanLimitProvider Provider(CountingHandler handler, IMemoryCache cache, Guid? orgId) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("http://master.test/") },
            cache,
            new TenantContext { CustomerId = Guid.NewGuid(), OrgId = orgId },
            NullLogger<HttpPlanLimitProvider>.Instance);

    [Fact]
    public async Task QUOTA_K09_the_limits_come_from_the_branchs_org_context_and_are_cached()
    {
        Guid orgId = Guid.NewGuid();
        var handler = new CountingHandler(() => Json(
            """{"planName":"Professional","monthlySalesTransactionLimit":1000,"monthlyPurchaseTransactionLimit":1000,"maxUsers":5}"""));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        HttpPlanLimitProvider provider = Provider(handler, cache, orgId);

        PlanLimits? first = await provider.GetAsync();
        PlanLimits? second = await provider.GetAsync();

        Assert.Equal(new PlanLimits("Professional", 1000, 1000), first);
        Assert.Equal(first, second);
        Assert.Equal($"/internal/orgs/{orgId}/context", handler.LastPath);

        // One call for two documents: a busy branch costs Master a call a minute, not one a document.
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task QUOTA_K10_a_plan_with_no_limit_is_read_as_none()
    {
        var handler = new CountingHandler(() => Json(
            """{"planName":"Elite","monthlySalesTransactionLimit":null,"monthlyPurchaseTransactionLimit":null}"""));
        using var cache = new MemoryCache(new MemoryCacheOptions());

        PlanLimits? limits = await Provider(handler, cache, Guid.NewGuid()).GetAsync();

        Assert.Equal(new PlanLimits("Elite", null, null), limits);
    }

    [Fact]
    public async Task QUOTA_K11_an_unreachable_master_is_unknown_and_is_asked_again_next_time()
    {
        int call = 0;
        var handler = new CountingHandler(() => ++call == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : Json("""{"planName":"Professional","monthlySalesTransactionLimit":1000,"monthlyPurchaseTransactionLimit":1000}"""));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        HttpPlanLimitProvider provider = Provider(handler, cache, Guid.NewGuid());

        // Unknown is not a limit: the caller counts nothing and refuses nothing.
        Assert.Null(await provider.GetAsync());

        // Only an answer is cached, so the next document asks again and gets it.
        Assert.Equal(new PlanLimits("Professional", 1000, 1000), await provider.GetAsync());
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task QUOTA_K12_without_a_branch_there_is_nothing_to_ask()
    {
        var handler = new CountingHandler(() => Json("{}"));
        using var cache = new MemoryCache(new MemoryCacheOptions());

        Assert.Null(await Provider(handler, cache, orgId: null).GetAsync());
        Assert.Equal(0, handler.Calls);
    }
}
