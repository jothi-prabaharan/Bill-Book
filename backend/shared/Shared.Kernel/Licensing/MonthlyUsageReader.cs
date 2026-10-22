using Microsoft.EntityFrameworkCore;

namespace Shared.Kernel.Licensing;

/// <summary>What a branch has used of one kind of document this month, and what its plan allows (TK-210).</summary>
public class MonthlyUsageResponse
{
    public PlanLimitKind Kind { get; set; }

    /// <summary>Trial, Professional or Elite; null when the plan could not be read.</summary>
    public string? Plan { get; set; }

    /// <summary>The first day of the month being counted, in India Standard Time.</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Counted documents created this month. Zero for a plan with no limit, which is not counted.</summary>
    public int Used { get; set; }

    /// <summary>The month's limit; null is no limit.</summary>
    public int? Limit { get; set; }

    /// <summary>What is left; null with no limit.</summary>
    public int? Remaining { get; set; }
}

/// <summary>Reads a branch's usage for Sales' and Purchase's <c>usage</c> routes (TK-210).</summary>
public static class MonthlyUsageReader
{
    public static async Task<MonthlyUsageResponse> ReadAsync(
        DbContext db,
        IPlanLimitProvider limits,
        PlanLimitKind kind,
        TimeProvider clock,
        CancellationToken ct)
    {
        DateOnly period = QuotaPeriod.StartOf(clock.GetUtcNow());
        PlanLimits? plan = await limits.GetAsync(ct);
        int? cap = plan?.MonthlyFor(kind);

        int used = await db.Set<MonthlyUsage>()
            .Where(u => u.PeriodStart == period)
            .Select(u => u.DocumentCount)
            .FirstOrDefaultAsync(ct);

        return new MonthlyUsageResponse
        {
            Kind = kind,
            Plan = plan?.Plan,
            PeriodStart = period,
            Used = used,
            Limit = cap,
            Remaining = cap is int limit ? Math.Max(0, limit - used) : null,
        };
    }
}
