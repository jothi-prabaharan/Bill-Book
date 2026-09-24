using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Kernel.Documents;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// The branch travels with every internal call that reads tenant data (TK-06).
///
/// The tax-rate and name clients authenticate with the internal key alone, so
/// the answering service has no token to take a tenant from. Before TK-06 they
/// sent no branch either, and Accounting, Master and Inventory answered with
/// their query filter scoped to nothing: no rates, no names, for every branch.
/// </summary>
public sealed class InternalTenantTests
{
    private static readonly Guid Customer = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Org = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // --- InternalTenant.Apply ---

    [Fact]
    public void A_named_branch_is_applied_when_there_is_no_token()
    {
        var tenant = new TenantContext();

        Assert.Equal(InternalTenantOutcome.Applied, InternalTenant.Apply(tenant, Customer, Org));
        Assert.Equal(Customer, tenant.CustomerId);
        Assert.Equal(Org, tenant.OrgId);
    }

    [Fact]
    public void A_token_alone_is_enough()
    {
        var tenant = new TenantContext { CustomerId = Customer, OrgId = Org };

        Assert.Equal(InternalTenantOutcome.Applied, InternalTenant.Apply(tenant, Guid.Empty, Guid.Empty));
        Assert.Equal(Org, tenant.OrgId);
    }

    [Fact]
    public void A_named_branch_that_agrees_with_the_token_is_applied()
    {
        var tenant = new TenantContext { CustomerId = Customer, OrgId = Org };

        Assert.Equal(InternalTenantOutcome.Applied, InternalTenant.Apply(tenant, Customer, Org));
    }

    [Fact]
    public void A_named_branch_that_disagrees_with_the_token_is_refused_and_changes_nothing()
    {
        var tenant = new TenantContext { CustomerId = Customer, OrgId = Org };

        Assert.Equal(InternalTenantOutcome.Mismatch, InternalTenant.Apply(tenant, Customer, Guid.NewGuid()));
        Assert.Equal(Org, tenant.OrgId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Half_a_branch_or_none_with_no_token_is_missing(bool customer, bool org)
    {
        var tenant = new TenantContext();

        InternalTenantOutcome outcome = InternalTenant.Apply(
            tenant, customer ? Customer : Guid.Empty, org ? Org : Guid.Empty);

        Assert.Equal(InternalTenantOutcome.Missing, outcome);
        Assert.Null(tenant.CustomerId);
        Assert.Null(tenant.OrgId);
    }

    // --- What the clients send ---

    private sealed class RecordingHandler(string responseJson) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string? Body)> Sent { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Sent.Add((request, body));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static HttpClient Client(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://service.test/") };

    [Fact]
    public async Task The_tax_rate_client_names_the_branch_in_the_query()
    {
        var handler = new RecordingHandler("[]");
        var provider = new HttpTaxRateProvider(
            Client(handler),
            new MemoryCache(new MemoryCacheOptions()),
            new TenantContext { CustomerId = Customer, OrgId = Org },
            NullLogger<HttpTaxRateProvider>.Instance);

        await provider.GetRatesAsync(new DateOnly(2026, 9, 24));

        Uri uri = Assert.Single(handler.Sent).Request.RequestUri!;
        Assert.Equal("/internal/tax/rates", uri.AbsolutePath);
        Assert.Contains($"customerId={Customer}", uri.Query);
        Assert.Contains($"orgId={Org}", uri.Query);
        Assert.Contains("on=2026-09-24", uri.Query);
    }

    [Fact]
    public async Task The_tax_rate_client_asks_nothing_without_a_customer()
    {
        var handler = new RecordingHandler("[]");
        var provider = new HttpTaxRateProvider(
            Client(handler),
            new MemoryCache(new MemoryCacheOptions()),
            new TenantContext { OrgId = Org },
            NullLogger<HttpTaxRateProvider>.Instance);

        Assert.Null(await provider.GetRatesAsync(new DateOnly(2026, 9, 24)));
        Assert.Empty(handler.Sent);
    }

    [Fact]
    public async Task The_name_client_names_the_branch_in_the_body()
    {
        var handler = new RecordingHandler("[{\"id\":7,\"code\":\"C7\",\"name\":\"Seven\"}]");
        var lookup = new HttpContactNameLookup(
            Client(handler),
            new MemoryCache(new MemoryCacheOptions()),
            new TenantContext { CustomerId = Customer, OrgId = Org },
            NullLogger<HttpContactNameLookup>.Instance);

        IReadOnlyDictionary<long, NamedRef> names = await lookup.ResolveAsync([7]);

        Assert.Equal("Seven", names[7].Name);

        var body = JsonSerializer.Deserialize<NameLookupRequest>(
            Assert.Single(handler.Sent).Body!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal([7L], body.Ids);
        Assert.Equal(Customer, body.CustomerId);
        Assert.Equal(Org, body.OrgId);
    }
}
