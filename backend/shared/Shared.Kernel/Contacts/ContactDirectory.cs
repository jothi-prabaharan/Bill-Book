using System.Net.Http.Json;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Contacts;

/// <summary>
/// Which contacts a service may point at (School, TK-61): a guardian on a
/// student, a vendor on an AMC contract. Ids across services are unenforced
/// (hard rule 8), so they are checked here, through Master's
/// <c>internal/contacts/lookup</c>, before they are stored.
/// </summary>
public sealed class ContactLookupRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public List<long> Ids { get; set; } = [];
}

/// <summary>What another service may know of a contact: who it is and what it is for.</summary>
public sealed class ContactSummary
{
    public long ContactId { get; set; }

    public string ContactCode { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public bool IsCustomer { get; set; }

    public bool IsVendor { get; set; }

    public bool IsGuardian { get; set; }

    public bool IsActive { get; set; }
}

public interface IContactDirectory
{
    /// <summary>The contacts among <paramref name="ids"/> in the current branch. Throws when Master cannot be asked.</summary>
    Task<IReadOnlyDictionary<long, ContactSummary>> FindAsync(IEnumerable<long> ids, CancellationToken ct);
}

/// <summary>Asks Master over the internal key, naming the branch in the body.</summary>
public sealed class HttpContactDirectory : IContactDirectory
{
    private readonly HttpClient _http;
    private readonly ITenantContext _tenant;

    public HttpContactDirectory(HttpClient http, ITenantContext tenant)
    {
        _http = http;
        _tenant = tenant;
    }

    public async Task<IReadOnlyDictionary<long, ContactSummary>> FindAsync(IEnumerable<long> ids, CancellationToken ct)
    {
        List<long> wanted = [.. ids.Distinct()];
        if (wanted.Count == 0)
        {
            return new Dictionary<long, ContactSummary>();
        }

        using HttpResponseMessage response = await _http.PostAsJsonAsync("internal/contacts/lookup", new ContactLookupRequest
        {
            CustomerId = _tenant.CustomerId ?? Guid.Empty,
            OrgId = _tenant.OrgId ?? Guid.Empty,
            Ids = wanted,
        }, ct);
        response.EnsureSuccessStatusCode();

        List<ContactSummary> found = await response.Content.ReadFromJsonAsync<List<ContactSummary>>(ct) ?? [];
        return found.ToDictionary(c => c.ContactId);
    }
}
