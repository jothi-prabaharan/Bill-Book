using System.Net;
using System.Text;
using Sales.Api.Services;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// The addresses <see cref="InventoryClient"/> actually calls, pinned against
/// the routes <c>Inventory.Api</c>'s <c>InternalStockController</c> actually
/// serves.
///
/// <b>No stub can catch this class of bug.</b> Every other test in this project
/// hands the services a fake <see cref="IInventoryClient"/>, which is exactly
/// the seam a wrong URL hides behind — the fake answers whatever it is asked,
/// whatever address the real client would have used. Sales spent its whole life
/// posting a credit note's returned stock to <c>internal/stock/receive</c>,
/// which Inventory has never served: the route is <c>receipt</c>. Nothing
/// failed until a real request went over a real socket, and nothing in this
/// suite ever sent one.
///
/// So these tests send one, to a handler that records the path instead of a
/// server. The expected strings below are transcribed from
/// <c>InternalStockController</c>'s own attributes: route prefix
/// <c>internal/stock</c> plus <c>reserve</c>, <c>release</c>, <c>issue</c>,
/// <c>receipt</c> and <c>availability</c>. If either side is renamed without
/// the other, this fails rather than a customer's stock return does.
/// </summary>
public sealed class InventoryClientRouteTests
{
    [Fact]
    public async Task Returning_stock_posts_to_the_receipt_route_inventory_serves()
    {
        (InventoryClient client, RecordingHandler handler) = Build();

        await client.ReceiveAsync(new ReceiveStockRequest(), CancellationToken.None);

        // Not "receive". Inventory's own attribute reads [HttpPost("receipt")],
        // and a credit note's returned stock is what goes through it.
        Assert.Equal("/internal/stock/receipt", handler.LastPath);
    }

    [Fact]
    public async Task Reserving_posts_to_the_reserve_route()
    {
        (InventoryClient client, RecordingHandler handler) = Build();

        await client.ReserveAsync(new ReserveStockRequest(), CancellationToken.None);

        Assert.Equal("/internal/stock/reserve", handler.LastPath);
    }

    [Fact]
    public async Task Releasing_posts_to_the_release_route()
    {
        (InventoryClient client, RecordingHandler handler) = Build();

        await client.ReleaseAsync(new ReleaseStockRequest(), CancellationToken.None);

        Assert.Equal("/internal/stock/release", handler.LastPath);
    }

    [Fact]
    public async Task Issuing_posts_to_the_issue_route()
    {
        (InventoryClient client, RecordingHandler handler) = Build();

        await client.IssueAsync(new IssueStockRequest(), CancellationToken.None);

        Assert.Equal("/internal/stock/issue", handler.LastPath);
    }

    [Fact]
    public async Task Reading_availability_posts_to_the_availability_route()
    {
        (InventoryClient client, RecordingHandler handler) = Build();

        // At least one id: an empty request answers from memory without a call,
        // which is deliberate and covered by the client's own doc comment.
        await client.GetAvailabilityAsync(
            new StockAvailabilityRequest { ItemIds = [7] }, CancellationToken.None);

        Assert.Equal("/internal/stock/availability", handler.LastPath);
    }

    private static (InventoryClient, RecordingHandler) Build()
    {
        RecordingHandler handler = new();

        HttpClient http = new(handler)
        {
            // A base address is what makes the client's relative paths resolve
            // the way they do in the service, where it comes from configuration.
            BaseAddress = new Uri("http://inventory.test/"),
        };

        return (new InventoryClient(http, new TenantContext()), handler);
    }

    /// <summary>Answers every request with an empty object, and remembers where it was sent.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? LastPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastPath = request.RequestUri?.AbsolutePath;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            });
        }
    }
}
