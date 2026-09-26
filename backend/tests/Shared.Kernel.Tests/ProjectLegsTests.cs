using Shared.Kernel.Projects;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>How a document's lines hand their projects to its ledger legs (TK-105).</summary>
public sealed class ProjectLegsTests
{
    [Fact]
    public void A_header_leg_takes_the_project_only_when_every_line_agrees()
    {
        Assert.Equal(7L, ProjectLegs.Common([7, 7, 7]));
        Assert.Null(ProjectLegs.Common([7, 8]));
        Assert.Null(ProjectLegs.Common([7, null]));
        Assert.Null(ProjectLegs.Common([]));
    }

    [Fact]
    public void Revenue_splits_by_project_in_proportion_and_adds_up_exactly()
    {
        List<(long? ProjectId, decimal Amount)> parts = ProjectLegs.Split(1000m, [(1, 1m), (2, 1m), (2, 1m)]);

        Assert.Equal([(1L, 333.33m), (2L, 666.67m)], parts.Select(p => (p.ProjectId!.Value, p.Amount)));
        Assert.Equal(1000m, parts.Sum(p => p.Amount));
    }

    [Fact]
    public void Lines_with_no_project_share_one_untagged_part()
    {
        List<(long? ProjectId, decimal Amount)> parts = ProjectLegs.Split(300m, [(null, 100m), (5, 100m), (null, 100m)]);

        Assert.Equal(200m, parts.Single(p => p.ProjectId is null).Amount);
        Assert.Equal(100m, parts.Single(p => p.ProjectId == 5).Amount);
    }

    [Fact]
    public void One_group_is_returned_whole()
    {
        Assert.Equal([(9L, 500m)], ProjectLegs.Split(500m, [(9, 3m), (9, 4m)]).Select(p => (p.ProjectId!.Value, p.Amount)));
        Assert.Equal([((long?)null, 500m)], ProjectLegs.Split(500m, [(null, 3m)]));
    }
}
