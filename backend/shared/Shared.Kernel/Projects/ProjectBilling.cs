using System.Net.Http.Json;

namespace Shared.Kernel.Projects;

/// <summary>
/// What a project invoice can bill (TK-106, design "Project accounting",
/// decision 6). Time, spend-money expenses and milestones are Accounting's;
/// bill lines are Purchase's. Each is claimed by its own owner.
/// </summary>
public enum ProjectItemKind
{
    TimeEntry = 1,

    /// <summary>A billable spend-money line (<c>acc.SpendMoneyDetails</c>).</summary>
    Expense = 2,

    Milestone = 3,

    /// <summary>A billable bill line (<c>pur.BillDetails</c>).</summary>
    BillLine = 4,
}

public sealed class ProjectItemRef
{
    public ProjectItemKind Kind { get; set; }

    public long ItemId { get; set; }

    /// <summary>The project the item belongs to, as the invoice records it. Not used by a claim.</summary>
    public long? ProjectId { get; set; }
}

/// <summary>
/// Claims items for one invoice at post: a guarded update from unbilled to
/// billed that must touch every row, or nothing is claimed. An item this same
/// invoice already holds counts as claimed, so a retried post is safe.
/// </summary>
public sealed class ClaimProjectItemsRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public long InvoiceId { get; set; }

    public List<ProjectItemRef> Items { get; set; } = [];
}

/// <summary>Releases everything one invoice claimed, on its void.</summary>
public sealed class ReleaseProjectItemsRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public long InvoiceId { get; set; }
}

public sealed class ClaimProjectItemsResponse
{
    public bool Claimed { get; set; }

    public int Requested { get; set; }

    public int Touched { get; set; }

    public string? Detail { get; set; }
}

/// <summary>The sentence a lost claim is refused with, the same from both owners.</summary>
public static class ProjectBillingMessages
{
    public const string AlreadyBilled =
        "Some of these project items were billed on another invoice, or are no longer billable. Nothing was posted; remove them and try again.";
}

/// <summary>
/// The claim and its release, over the internal key. Posting an invoice claims
/// Accounting's items and then Purchase's; if the second fails the first is
/// released, so a refused post holds nothing.
/// </summary>
public interface IProjectBillingClient
{
    /// <summary>Null when every item was claimed; otherwise why not. Nothing is left claimed on a refusal.</summary>
    Task<string?> ClaimAsync(Guid customerId, Guid orgId, long invoiceId, IReadOnlyCollection<ProjectItemRef> items, CancellationToken ct);

    Task ReleaseAsync(Guid customerId, Guid orgId, long invoiceId, CancellationToken ct);
}

/// <summary>Accounting's route and Purchase's, each given its own kinds.</summary>
public sealed class HttpProjectBillingClient : IProjectBillingClient
{
    private readonly HttpClient _accounting;
    private readonly HttpClient _purchase;

    public HttpProjectBillingClient(HttpClient accounting, HttpClient purchase)
    {
        _accounting = accounting;
        _purchase = purchase;
    }

    public async Task<string?> ClaimAsync(
        Guid customerId, Guid orgId, long invoiceId, IReadOnlyCollection<ProjectItemRef> items, CancellationToken ct)
    {
        List<ProjectItemRef> own = [.. items.Where(i => i.Kind != ProjectItemKind.BillLine)];
        List<ProjectItemRef> bills = [.. items.Where(i => i.Kind == ProjectItemKind.BillLine)];

        if (own.Count > 0 && await ClaimAtAsync(_accounting, "internal/projects/billing/claim", customerId, orgId, invoiceId, own, ct) is string refused)
        {
            return refused;
        }

        if (bills.Count > 0 && await ClaimAtAsync(_purchase, "internal/bills/billing/claim", customerId, orgId, invoiceId, bills, ct) is string billRefused)
        {
            if (own.Count > 0)
            {
                await ReleaseAtAsync(_accounting, "internal/projects/billing/release", customerId, orgId, invoiceId, ct);
            }

            return billRefused;
        }

        return null;
    }

    public async Task ReleaseAsync(Guid customerId, Guid orgId, long invoiceId, CancellationToken ct)
    {
        await ReleaseAtAsync(_accounting, "internal/projects/billing/release", customerId, orgId, invoiceId, ct);
        await ReleaseAtAsync(_purchase, "internal/bills/billing/release", customerId, orgId, invoiceId, ct);
    }

    private static async Task<string?> ClaimAtAsync(
        HttpClient http, string route, Guid customerId, Guid orgId, long invoiceId, List<ProjectItemRef> items, CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await http.PostAsJsonAsync(route, new ClaimProjectItemsRequest
            {
                CustomerId = customerId,
                OrgId = orgId,
                InvoiceId = invoiceId,
                Items = items,
            }, ct);

            ClaimProjectItemsResponse? answer = await response.Content.ReadFromJsonAsync<ClaimProjectItemsResponse>(ct);
            return answer is { Claimed: true } ? null : answer?.Detail ?? ProjectBillingMessages.AlreadyBilled;
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException
            or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return "The project items on this invoice could not be claimed. Nothing was posted; try again in a moment.";
        }
    }

    private static async Task ReleaseAtAsync(HttpClient http, string route, Guid customerId, Guid orgId, long invoiceId, CancellationToken ct)
    {
        using HttpResponseMessage response = await http.PostAsJsonAsync(route, new ReleaseProjectItemsRequest
        {
            CustomerId = customerId,
            OrgId = orgId,
            InvoiceId = invoiceId,
        }, ct);
        response.EnsureSuccessStatusCode();
    }
}
