using System.Net.Http.Json;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Projects;

/// <summary>
/// Which projects a line in another service may name (TK-104): Accounting owns
/// projects, and the ledger dimension is enforced at its posting API. A service
/// checks the ids through Accounting's <c>internal/projects/exists</c> before it
/// stores them, as it does contacts through Master.
/// </summary>
public sealed class ProjectExistsRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public List<long> Ids { get; set; } = [];
}

public sealed class ProjectSummary
{
    public long ProjectId { get; set; }

    public string ProjectCode { get; set; } = null!;

    public string ProjectName { get; set; } = null!;

    public string Status { get; set; } = null!;

    /// <summary>False for a completed or cancelled project, which takes no postings.</summary>
    public bool IsPostable { get; set; }
}

public interface IProjectDirectory
{
    /// <summary>The projects among <paramref name="ids"/> in the current branch. Throws when Accounting cannot be asked.</summary>
    Task<IReadOnlyDictionary<long, ProjectSummary>> FindAsync(IEnumerable<long> ids, CancellationToken ct);
}

/// <summary>Asks Accounting over the internal key, naming the branch in the body.</summary>
public sealed class HttpProjectDirectory : IProjectDirectory
{
    private readonly HttpClient _http;
    private readonly ITenantContext _tenant;

    public HttpProjectDirectory(HttpClient http, ITenantContext tenant)
    {
        _http = http;
        _tenant = tenant;
    }

    public async Task<IReadOnlyDictionary<long, ProjectSummary>> FindAsync(IEnumerable<long> ids, CancellationToken ct)
    {
        List<long> wanted = [.. ids.Distinct()];
        if (wanted.Count == 0)
        {
            return new Dictionary<long, ProjectSummary>();
        }

        using HttpResponseMessage response = await _http.PostAsJsonAsync("internal/projects/exists", new ProjectExistsRequest
        {
            CustomerId = _tenant.CustomerId ?? Guid.Empty,
            OrgId = _tenant.OrgId ?? Guid.Empty,
            Ids = wanted,
        }, ct);
        response.EnsureSuccessStatusCode();

        List<ProjectSummary> found = await response.Content.ReadFromJsonAsync<List<ProjectSummary>>(ct) ?? [];
        return found.ToDictionary(p => p.ProjectId);
    }
}

/// <summary>Why the projects a document's lines name may not be stored, or null (TK-105).</summary>
public static class ProjectCheck
{
    /// <summary>
    /// Null when every project named is the branch's and still open, or when
    /// none is named. "Could not ask" is a refusal, never a pass: a line saved
    /// against a project nobody checked would only be refused later, at post.
    /// </summary>
    public static async Task<string?> RefusalAsync(IProjectDirectory? directory, IEnumerable<long?> projectIds, CancellationToken ct)
    {
        List<long> ids = [.. projectIds.Where(id => id is not null).Select(id => id!.Value).Distinct()];
        if (directory is null || ids.Count == 0)
        {
            return null;
        }

        IReadOnlyDictionary<long, ProjectSummary> found;
        try
        {
            found = await directory.FindAsync(ids, ct);
        }
        catch (HttpRequestException)
        {
            return "The projects on these lines could not be checked. Nothing was saved; try again in a moment.";
        }

        foreach (long id in ids)
        {
            if (!found.TryGetValue(id, out ProjectSummary? project))
            {
                return $"Project {id} is not a project of this branch.";
            }

            if (!project.IsPostable)
            {
                return $"Project {project.ProjectCode} is {project.Status.ToLowerInvariant()}, so nothing more can be charged to it.";
            }
        }

        return null;
    }
}
