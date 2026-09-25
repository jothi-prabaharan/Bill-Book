using System.Net.Http.Json;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Contacts;

/// <summary>
/// A contact's registered name and billing address as fields, not as the
/// printable text a document carries (TK-91).
///
/// A sales document keeps its billing address as one block of text, which is
/// right for printing and wrong for the IRP, whose buyer block asks for a
/// location, a six-digit PIN and a state code separately. Master holds them as
/// fields on <c>con.ContactAddresses</c>, so they come from there.
/// </summary>
public sealed class ContactPostalAddress
{
    public long ContactId { get; set; }

    /// <summary>The legal name when the contact has one, otherwise the display name.</summary>
    public string LegalName { get; set; } = null!;

    /// <summary>The billing address's own GSTIN, or the contact's.</summary>
    public string? Gstin { get; set; }

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public string? PostalCode { get; set; }

    /// <summary>The two-digit GST state code of the address, or of the contact's place of supply.</summary>
    public string? StateCode { get; set; }

    public string? PhoneNumber { get; set; }

    /// <summary>
    /// The contact's GST registration type by name — Regular, Composition,
    /// Unregistered, Sez, Overseas or Consumer — which decides whether a supply
    /// to it needs an IRN and of which kind.
    /// </summary>
    public string? RegistrationType { get; set; }
}

public interface IContactAddressBook
{
    /// <summary>
    /// The contacts among <paramref name="ids"/> in the current branch, by id.
    /// Throws when Master cannot be asked, so a caller never mistakes "could not
    /// ask" for "has no address".
    /// </summary>
    Task<IReadOnlyDictionary<long, ContactPostalAddress>> FindAsync(IEnumerable<long> ids, CancellationToken ct);
}

/// <summary>Master's <c>internal/contacts/addresses</c>, under the internal key.</summary>
public sealed class HttpContactAddressBook : IContactAddressBook
{
    private readonly HttpClient _http;
    private readonly ITenantContext _tenant;

    public HttpContactAddressBook(HttpClient http, ITenantContext tenant)
    {
        _http = http;
        _tenant = tenant;
    }

    public async Task<IReadOnlyDictionary<long, ContactPostalAddress>> FindAsync(IEnumerable<long> ids, CancellationToken ct)
    {
        List<long> wanted = [.. ids.Distinct()];
        if (wanted.Count == 0)
        {
            return new Dictionary<long, ContactPostalAddress>();
        }

        using HttpResponseMessage response = await _http.PostAsJsonAsync("internal/contacts/addresses", new ContactLookupRequest
        {
            CustomerId = _tenant.CustomerId ?? Guid.Empty,
            OrgId = _tenant.OrgId ?? Guid.Empty,
            Ids = wanted,
        }, ct);
        response.EnsureSuccessStatusCode();

        List<ContactPostalAddress> found = await response.Content.ReadFromJsonAsync<List<ContactPostalAddress>>(ct) ?? [];
        return found.ToDictionary(c => c.ContactId);
    }
}
