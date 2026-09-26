namespace Shared.Kernel.Projects;

/// <summary>
/// How a document's lines hand their projects to its ledger legs (TK-105,
/// design "Project accounting", decision 4). A leg built from one line carries
/// that line's project. A header-level leg — the receivable, the payable, a
/// tax total, the round-off — carries a project only when every line names the
/// same one: splitting a receivable across projects would invent sub-balances
/// nobody collects.
/// </summary>
public static class ProjectLegs
{
    /// <summary>The project every line shares, or null when they differ or any line has none.</summary>
    public static long? Common(IEnumerable<long?> lineProjects)
    {
        long? common = null;
        bool any = false;

        foreach (long? project in lineProjects)
        {
            if (project is null)
            {
                return null;
            }

            if (any && common != project)
            {
                return null;
            }

            common = project;
            any = true;
        }

        return common;
    }

    /// <summary>
    /// Splits an aggregated amount — an invoice's one revenue leg, say — across
    /// the projects its lines name, in proportion to each line's value. The
    /// parts add up to <paramref name="total"/> exactly: each is rounded to two
    /// decimals and the last takes what rounding left, so the posting still
    /// balances. Lines with no project share one untagged part. A single group
    /// is returned whole.
    /// </summary>
    public static List<(long? ProjectId, decimal Amount)> Split(
        decimal total, IEnumerable<(long? ProjectId, decimal Weight)> lines)
    {
        List<(long? ProjectId, decimal Weight)> groups = [.. lines
            .GroupBy(l => l.ProjectId)
            .Select(g => (g.Key, g.Sum(l => l.Weight)))];

        decimal weight = groups.Sum(g => g.Weight);
        if (groups.Count <= 1 || weight == 0m)
        {
            return [(groups.Count == 1 ? groups[0].ProjectId : null, total)];
        }

        var parts = new List<(long? ProjectId, decimal Amount)>(groups.Count);
        decimal allocated = 0m;

        for (int i = 0; i < groups.Count; i++)
        {
            decimal amount = i == groups.Count - 1
                ? total - allocated
                : Math.Round(total * groups[i].Weight / weight, 2, MidpointRounding.AwayFromZero);

            allocated += amount;
            parts.Add((groups[i].ProjectId, amount));
        }

        return [.. parts.Where(p => p.Amount != 0m)];
    }
}
