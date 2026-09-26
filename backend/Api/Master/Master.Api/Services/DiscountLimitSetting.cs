using System.Globalization;
using Master.Repository;
using Microsoft.EntityFrameworkCore;

namespace Master.Api.Services;

/// <summary>A branch's most a sales line may be discounted (D-29, TK-102).</summary>
public interface IDiscountLimitSetting
{
    Task<decimal> PercentAsync(Guid orgId, CancellationToken ct);
}

/// <summary>
/// Reads <c>sales.maxLineDiscountPercent</c> from <c>mst.Configurations</c>: the
/// branch's override, else the shipped default. Anything that is not a number
/// from 0 to 100 reads as 100, no limit, rather than as a limit nobody set.
/// </summary>
public sealed class ConfigurationDiscountLimitSetting : IDiscountLimitSetting
{
    public const string Code = "sales.maxLineDiscountPercent";

    private readonly AdminDbContext _admin;

    public ConfigurationDiscountLimitSetting(AdminDbContext admin) => _admin = admin;

    public async Task<decimal> PercentAsync(Guid orgId, CancellationToken ct)
    {
        var rows = await _admin.Configurations
            .AsNoTracking()
            .Where(c => c.Code == Code && (c.OrgId == null || c.OrgId == orgId))
            .Select(c => new { c.OrgId, c.Value })
            .ToListAsync(ct);

        return Parse(rows.FirstOrDefault(r => r.OrgId == orgId)?.Value ?? rows.FirstOrDefault(r => r.OrgId == null)?.Value);
    }

    public static decimal Parse(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal percent) && percent is >= 0m and <= 100m
            ? percent
            : 100m;
}
