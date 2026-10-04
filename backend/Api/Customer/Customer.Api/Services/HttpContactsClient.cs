using Customer.Repository.Clients;
using Customer.Repository.Repositories;
using System.Net;
using System.Net.Http.Json;
using Shared.Kernel.Documents;

namespace Customer.Api.Services;



public sealed class ContactsClient : IContactsClient
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _accessor;
    private readonly ILogger<ContactsClient> _log;

    public ContactsClient(
        HttpClient http, IHttpContextAccessor accessor, ILogger<ContactsClient> log)
    {
        _http = http;
        _accessor = accessor;
        _log = log;
    }

    public async Task<bool> ExistsInCallerOrgAsync(long contactId, CancellationToken ct)
    {
        HttpRequestMessage request = Authorized(
            HttpMethod.Post,
            "internal/contacts/names",
            new NameLookupRequest { Ids = [contactId] });

        HttpResponseMessage response = await _http.SendAsync(request, ct);

        // A failure here is not "the contact is fine". An unreachable Master, a
        // wrong internal key, an expired token — every one of them has to refuse
        // the write, because the alternative is writing a cross-org reference
        // whenever the check happens to be unavailable.
        response.EnsureSuccessStatusCode();

        List<NamedRef>? names = await response.Content.ReadFromJsonAsync<List<NamedRef>>(ct);

        return names?.Any(n => n.Id == contactId) == true;
    }

    public async Task<CreatedContact?> CreateAsync(
        NewContactRequest request, CancellationToken ct)
    {
        // Master's own contact API, not an internal back door: creating master
        // data needs `contacts.edit` on the caller's token, and routing through
        // the public API is what makes that check happen. A CRM user without it
        // gets a 403 they can act on, rather than a contact they were not
        // entitled to create.
        //
        // `quick`, not the full create, so the contact rules — the person role,
        // the category, the branch's base currency — stay in Contacts. A lead
        // knows a name, a phone and an email; composing the rest here would put
        // Contacts' defaults in this service.
        HttpRequestMessage message = Authorized(HttpMethod.Post, "api/contacts/quick", new
        {
            displayName = request.DisplayName,
            legalName = request.LegalName,
            email = request.Email,
            mobileNumber = request.Phone,
            isCustomer = true,
            isVendor = false,
        });

        HttpResponseMessage response = await _http.SendAsync(message, ct);

        if (!response.IsSuccessStatusCode)
        {
            // Contacts explains its refusals — "a contact needs an email or a
            // mobile number", "that GSTIN is already used". Passing the message
            // through is what turns a dead end into something the person can fix;
            // swallowing it would leave them with a 400 and no next step.
            string? reason = await ReasonAsync(response, ct);

            _log.LogWarning(
                "Master refused a contact created from a lead: {Status}. {Reason}",
                (int)response.StatusCode,
                reason);

            throw new ContactCreationFailedException(response.StatusCode, reason);
        }

        CreateContactResponse? created =
            await response.Content.ReadFromJsonAsync<CreateContactResponse>(ct);

        return created is null
            ? throw new ContactCreationFailedException(
                HttpStatusCode.BadGateway,
                "Contacts accepted the request but returned nothing to record against the lead.")
            : new CreatedContact(created.ContactId, created.ContactCode, request.DisplayName);
    }

    public async Task<List<ContactItemDto>> ListContactsAsync(CancellationToken ct)
    {
        HttpRequestMessage request = Authorized(
            HttpMethod.Get,
            "api/contacts?includeInactive=false");

        HttpResponseMessage response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var list = await response.Content.ReadFromJsonAsync<List<ContactItemDto>>(ct);
        return list ?? [];
    }

    public async Task<IReadOnlyDictionary<long, ContactEmail>?> EmailsAsync(
        Guid customerId, Guid orgId, IReadOnlyCollection<long> contactIds, CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await _http.PostAsJsonAsync(
                "internal/contacts/emails", new { customerId, orgId, ids = contactIds }, ct);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            List<ContactEmail>? rows = await response.Content.ReadFromJsonAsync<List<ContactEmail>>(ct);
            return rows?.ToDictionary(r => r.ContactId);
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning(ex, "Master could not be asked for contact emails");
            return null;
        }
    }

    /// <summary>The <c>{ message }</c> on a refusal, when there is one.</summary>
    private static async Task<string?> ReasonAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            MessageBody? body = await response.Content.ReadFromJsonAsync<MessageBody>(ct);

            return string.IsNullOrWhiteSpace(body?.Message) ? null : body.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
        {
            // A refusal without a JSON body — a 403 from the permission filter,
            // say. There is simply nothing to quote.
            return null;
        }
    }

    private HttpRequestMessage Authorized(HttpMethod method, string route, object? body = null)
    {
        var request = new HttpRequestMessage(method, route);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        string? authorization = _accessor.HttpContext?.Request.Headers.Authorization.ToString();

        if (!string.IsNullOrEmpty(authorization))
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        return request;
    }

    private sealed class CreateContactResponse
    {
        public long ContactId { get; set; }

        public string ContactCode { get; set; } = string.Empty;
    }

    private sealed class MessageBody
    {
        public string? Message { get; set; }
    }
}

/// <summary>
/// Master would not create the contact. Carries the status so the controller can
/// answer 403 for a permission refusal and 502 for anything else — a CRM user
/// lacking <c>contacts.edit</c> is a different problem from Master being down,
/// and one of them the person can fix.
/// </summary>
public sealed class ContactCreationFailedException : Exception
{
    public ContactCreationFailedException(HttpStatusCode status, string? reason = null)
        : base(reason ?? $"The contact could not be created ({(int)status}).") =>
        Status = status;

    public HttpStatusCode Status { get; }
}

