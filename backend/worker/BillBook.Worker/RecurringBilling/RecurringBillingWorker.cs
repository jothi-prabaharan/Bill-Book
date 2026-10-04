using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel.Apps;
using Shared.Kernel.Tenancy;

namespace BillBook.Worker.RecurringBilling;

/// <summary>
/// Raises due recurring invoices and bills (TK-139), once an hour by default.
///
/// The worker only says "run now" for a branch: Sales and Purchase raise the
/// documents themselves, through their own invoice and bill repositories, so
/// there is no second copy of numbering, tax or posting here. What makes each
/// occurrence happen once lives with them — the schedule's row version and the
/// unique (schedule, date) key — so a second worker replica or a rerun raises
/// nothing twice.
///
/// A branch without the <see cref="AddOn.RecurringBilling"/> add-on is skipped:
/// switching the add-on off stops its schedules without deleting them.
/// </summary>
public sealed class RecurringBillingWorker : BranchWorkerBase<RecurringBillingWorker>
{
    public RecurringBillingWorker(
        IServiceScopeFactory scopes, ILogger<RecurringBillingWorker> logger, IConfiguration config)
        : base(scopes, logger, config)
    {
    }

    protected override string JobName => "RecurringBilling";

    protected override TimeSpan DefaultInterval => TimeSpan.FromHours(1);

    protected override async Task ProcessBranchAsync(ActiveOrganization branch, IServiceScope scope, CancellationToken ct)
    {
        if ((branch.AddOns & AddOn.RecurringBilling) == 0)
        {
            return;
        }

        RecurringBillingClients clients = scope.ServiceProvider.GetRequiredService<RecurringBillingClients>();
        RunRecurring body = new(branch.CustomerId, branch.OrgId, null);

        // Both are attempted even if the first fails; a failure is rethrown so
        // the base class audits it to the error log with the branch named.
        HttpRequestException? failure = null;
        foreach (HttpClient client in new[] { clients.Sales, clients.Purchase })
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync("run", body, ct);
            if (!response.IsSuccessStatusCode)
            {
                failure ??= new HttpRequestException(
                    $"{client.BaseAddress} answered {(int)response.StatusCode}.", null, response.StatusCode);
            }
        }

        if (failure is not null)
        {
            throw failure;
        }
    }

    private sealed record RunRecurring(Guid CustomerId, Guid OrgId, DateOnly? AsOf);
}

/// <summary>Sales' and Purchase's <c>internal/recurring-*</c> routes, each carrying the internal key.</summary>
public sealed class RecurringBillingClients
{
    public const string SalesClient = "recurring-sales";
    public const string PurchaseClient = "recurring-purchase";

    public RecurringBillingClients(IHttpClientFactory factory)
    {
        Sales = factory.CreateClient(SalesClient);
        Purchase = factory.CreateClient(PurchaseClient);
    }

    public HttpClient Sales { get; }

    public HttpClient Purchase { get; }
}
