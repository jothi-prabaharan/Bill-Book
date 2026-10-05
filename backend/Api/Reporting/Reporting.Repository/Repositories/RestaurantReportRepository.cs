using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Shared.Kernel.Apps;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The restaurant reports a caller may open, for Restaurant › Reports
/// (TK-150). The reports themselves run on the one report engine — the grid,
/// filters, grouping and the Excel and CSV export are the same as every other
/// report's — so this only narrows the catalog to the Restaurant module, with
/// the branch's switches applied: a report whose switch is off is absent.
/// </summary>
public sealed class RestaurantReportRepository
{
    private readonly ReportCatalogRepository _catalog;
    private readonly ReportAudienceRepository _audience;

    public RestaurantReportRepository(ReportCatalogRepository catalog, ReportAudienceRepository audience)
    {
        _catalog = catalog;
        _audience = audience;
    }

    public async Task<List<ReportCatalogItemView>> ListAsync(
        IReadOnlySet<string> permissions, AddOn addOns, CancellationToken ct)
    {
        ReportAudience audience = await _audience.ForAsync(addOns, BusinessType.Restaurant, ct);

        return [.. (await _catalog.ListAsync(permissions, ct, audience)).Where(r => r.Module == ReportModule.Restaurant)];
    }
}
