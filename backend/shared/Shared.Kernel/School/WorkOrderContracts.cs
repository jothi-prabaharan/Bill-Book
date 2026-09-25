using System.Net.Http.Json;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.School;

// Raising a work order from another School service (TK-67 plan occurrences,
// TK-68 AMC visits) through WorkOrder's internal/work-orders/raise. Idempotent on
// SourceKey: raising the same key twice returns the first work order.

public sealed class RaiseWorkOrder
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    /// <summary>E.g. <c>PPM:{planId}:{yyyy-MM-dd}</c>, or <c>AMC:{visitId}</c>. At most 60 characters.</summary>
    public string SourceKey { get; set; } = null!;

    public string Title { get; set; } = null!;

    /// <summary>WorkOrder's source by name: <c>Preventive</c> or <c>Amc</c>.</summary>
    public string WorkOrderSource { get; set; } = "Preventive";

    public long? FacilityAssetId { get; set; }

    public long? SpaceId { get; set; }

    public DateOnly ReportedDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public long? AssignedEmployeeId { get; set; }

    public long? PreventivePlanId { get; set; }

    public long? AmcContractId { get; set; }
}

public sealed class RaisedWorkOrder
{
    public long WorkOrderId { get; set; }

    public string WorkOrderNo { get; set; } = null!;

    public bool Created { get; set; }
}

public interface IWorkOrderClient
{
    /// <summary>Raises the work order, or returns the one already raised for that key. Throws when WorkOrder cannot be reached.</summary>
    Task<RaisedWorkOrder> RaiseAsync(RaiseWorkOrder request, CancellationToken ct);
}

public sealed class HttpWorkOrderClient : IWorkOrderClient
{
    private readonly HttpClient _http;
    private readonly ITenantContext _tenant;

    public HttpWorkOrderClient(HttpClient http, ITenantContext tenant)
    {
        _http = http;
        _tenant = tenant;
    }

    public async Task<RaisedWorkOrder> RaiseAsync(RaiseWorkOrder request, CancellationToken ct)
    {
        request.CustomerId = _tenant.CustomerId ?? Guid.Empty;
        request.OrgId = _tenant.OrgId ?? Guid.Empty;

        using HttpResponseMessage response = await _http.PostAsJsonAsync("internal/work-orders/raise", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RaisedWorkOrder>(ct)
            ?? throw new HttpRequestException("WorkOrder answered with no body.");
    }
}
