using Customer.Repository.Repositories;
using System.Net;
using System.Net.Http.Json;
using Shared.Kernel.Documents;

namespace Customer.Repository.Clients;

/// <summary>
/// The Contacts half of Master, as seen from this service.
///
/// <b>Why a client and not a query.</b> A lead and a ticket both name a
/// <c>ContactId</c>, and the foreign key on those columns proves only that the
/// id exists — it says nothing about whose branch it belongs to. Reading
/// <c>con.Contacts</c> from here to find out would mean referencing another
/// service's <c>DbContext</c>, which is the rule this project does not bend
/// (CLAUDE.md 8). So the question goes to the service that owns the answer.
///
/// <b>The caller's bearer token is forwarded on every call</b>, exactly as
/// <c>AccountingSubAccounts</c> does it in Master. That is what makes the answer
/// trustworthy rather than merely available: Master resolves its own tenant
/// context from that token, so its query filter and its RLS policy both run as
/// the calling user, and a contact in another branch is invisible to the lookup
/// rather than visible-and-then-compared. Without the header the call arrives
/// with no tenant context, <c>CurrentOrgId</c> is <c>Guid.Empty</c>, and every
/// lookup answers "no such contact" — which would fail closed, but would also
/// make every conversion impossible.
///
/// The internal key rides along on the handler, because
/// <c>internal/contacts/names</c> is <see cref="Shared.Kernel.Internal.InternalOnlyAttribute"/>
/// and needs both.
/// </summary>
public interface IContactsClient
{
    /// <summary>
    /// Whether this contact exists <b>in the caller's own branch</b>.
    ///
    /// False covers three cases the caller must treat alike — no such contact,
    /// a contact in another branch, and a contact in another customer entirely —
    /// because telling them apart is itself the information an id-probing caller
    /// is after.
    /// </summary>
    Task<bool> ExistsInCallerOrgAsync(long contactId, CancellationToken ct);

    /// <summary>
    /// Creates a contact in the caller's branch, through Master's own API so the
    /// contact rules — code allocation, GSTIN validation, the six sub-accounts —
    /// stay in the service that owns them.
    ///
    /// Returns null when Master refused. <paramref name="failure"/> then carries
    /// something worth showing the person.
    /// </summary>
    Task<CreatedContact?> CreateAsync(NewContactRequest request, CancellationToken ct);

    /// <summary>
    /// Lists active contacts in the caller's branch for campaign audience resolution.
    /// </summary>
    Task<List<ContactItemDto>> ListContactsAsync(CancellationToken ct);

    /// <summary>
    /// Where each contact is written to (Master's <c>internal/contacts/emails</c>),
    /// for a caller with no user token — the service reminder run (TK-140). The
    /// branch travels in the body. Null when Master could not be read.
    /// </summary>
    Task<IReadOnlyDictionary<long, ContactEmail>?> EmailsAsync(
        Guid customerId, Guid orgId, IReadOnlyCollection<long> contactIds, CancellationToken ct);
}

/// <summary>What Master gives back when it has made a contact.</summary>
public sealed record CreatedContact(long ContactId, string ContactCode, string DisplayName);

public sealed class ContactItemDto
{
    public long ContactId { get; set; }
    public string ContactCode { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Email { get; set; }
    public string? MobileNumber { get; set; }
    public bool IsCustomer { get; set; }
    public bool IsVendor { get; set; }
    public string? ContactCategory { get; set; }
    public string? City { get; set; }
    public bool IsActive { get; set; }
    public Dictionary<string, System.Text.Json.JsonElement>? CustomFields { get; set; }
}

/// <summary>
/// The few fields a lead can supply. Deliberately not the whole of
/// <c>SaveContactRequest</c>: a lead holds a name, a company, a phone and an
/// email, and inventing the rest of a contact master here would put contact
/// rules in the wrong service.
/// </summary>
public sealed class NewContactRequest
{
    public string DisplayName { get; set; } = null!;

    public string? LegalName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }
}
