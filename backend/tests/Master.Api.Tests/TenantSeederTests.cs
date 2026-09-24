using System.Net;
using System.Text;
using Master.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Which services a new branch is seeded by.
///
/// <b>Purchase and Reporting both had a seed endpoint that nothing called</b>,
/// so every branch came up with no purchase numbering series and no report
/// catalog, and nothing failed to say so (TK-01). These tests pin the fan-out:
/// every seeding service is called, on the route and with the key its endpoint
/// expects, and one with no URL configured is reported as failed rather than
/// skipped in silence.
///
/// No database: the service provider is empty, so the vertical falls back to
/// General and the in-process Contacts seed reports itself as failed. Both are
/// the seeder's documented behaviour when its dependencies cannot be resolved,
/// and neither is what these tests are about.
/// </summary>
public sealed class TenantSeederTests
{
    private static readonly string[] Seeded =
        ["Accounting", "Inventory", "Sales", "Purchase", "Reporting", "Printing", "Customer"];

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"seeded\":{}}", Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static HttpTenantSeeder Seeder(RecordingHandler handler, IDictionary<string, string?> settings) =>
        new(
            new Factory(handler),
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<HttpTenantSeeder>.Instance);

    private static Dictionary<string, string?> AllConfigured() =>
        Seeded.ToDictionary(
            s => $"Seeding:{s}",
            s => (string?)$"http://{s.ToLowerInvariant()}.test/");

    [Fact]
    public async Task Every_seeding_service_is_called_including_purchase_and_reporting()
    {
        var handler = new RecordingHandler();
        Dictionary<string, string?> settings = AllConfigured();
        settings["Internal:ApiKey"] = "test-key";

        IReadOnlyList<string> failed =
            await Seeder(handler, settings).SeedAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        string[] hosts = handler.Requests.Select(r => r.RequestUri!.Host).ToArray();
        Assert.Equal(Seeded.Select(s => $"{s.ToLowerInvariant()}.test"), hosts);

        Assert.All(handler.Requests, r =>
        {
            Assert.Equal(HttpMethod.Post, r.Method);
            Assert.Equal("/internal/seed/organization", r.RequestUri!.AbsolutePath);
            Assert.True(r.Headers.Contains(Shared.Kernel.Internal.InternalOnlyAttribute.HeaderName));
        });

        // Only the in-process seed fails, and only because this provider is empty.
        Assert.Equal(["Contacts"], failed);
    }

    [Theory]
    [InlineData("Purchase")]
    [InlineData("Reporting")]
    [InlineData("Customer")]
    public async Task A_service_with_no_url_is_reported_as_failed_and_the_rest_still_run(string missing)
    {
        var handler = new RecordingHandler();
        Dictionary<string, string?> settings = AllConfigured();
        settings[$"Seeding:{missing}"] = "";

        IReadOnlyList<string> failed =
            await Seeder(handler, settings).SeedAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.Contains(missing, failed);
        Assert.Equal(Seeded.Length - 1, handler.Requests.Count);
        Assert.DoesNotContain(handler.Requests, r => r.RequestUri!.Host == $"{missing.ToLowerInvariant()}.test");
    }

    /// <summary>A Payroll branch is seeded with Accounting, Hrm and Printing only, and no contacts (TK-45, TK-48).</summary>
    [Fact]
    public async Task A_payroll_branch_is_seeded_with_the_employee_master_and_no_trading_services()
    {
        var handler = new RecordingHandler();
        Dictionary<string, string?> settings = AllConfigured();
        settings["Seeding:Hrm"] = "http://hrm.test/";

        IReadOnlyList<string> failed = await Seeder(handler, settings).SeedAsync(
            Guid.NewGuid(), Guid.NewGuid(), Shared.Kernel.Apps.App.Payroll, CancellationToken.None);

        Assert.Equal(["accounting.test", "hrm.test", "printing.test"], handler.Requests.Select(r => r.RequestUri!.Host));
        Assert.Empty(failed);
    }
}
