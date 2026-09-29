using System.Net.Http.Json;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Contacts;

/// <summary>
/// What TDS needs to know about a contact (TK-117): whether TDS applies, under
/// which section, their PAN and TAN, and any lower-deduction certificate.
/// Master holds the contact, so Master answers; Accounting computes.
/// </summary>
public sealed class TdsProfileRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public long ContactId { get; set; }
}

public sealed class TdsProfile
{
    public long ContactId { get; set; }

    public bool IsTdsApplicable { get; set; }

    public string? TdsSection { get; set; }

    public string? Pan { get; set; }

    public string? Tan { get; set; }

    public string? CertificateNo { get; set; }

    public decimal? CertificateRate { get; set; }

    public DateOnly? CertificateValidFrom { get; set; }

    public DateOnly? CertificateValidTo { get; set; }

    public decimal? CertificateLimit { get; set; }
}

public interface ITdsProfileClient
{
    /// <summary>
    /// The contact's profile. Null when Master could not be asked; a contact
    /// the branch does not hold comes back with TDS off.
    /// </summary>
    Task<TdsProfile?> ProfileAsync(long contactId, CancellationToken ct);
}

/// <summary>Asks Master over the internal key, naming the branch in the body.</summary>
public sealed class HttpTdsProfileClient : ITdsProfileClient
{
    private readonly HttpClient _http;
    private readonly ITenantContext _tenant;

    public HttpTdsProfileClient(HttpClient http, ITenantContext tenant)
    {
        _http = http;
        _tenant = tenant;
    }

    public async Task<TdsProfile?> ProfileAsync(long contactId, CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await _http.PostAsJsonAsync("internal/contacts/tds-profile", new TdsProfileRequest
            {
                CustomerId = _tenant.CustomerId ?? Guid.Empty,
                OrgId = _tenant.OrgId ?? Guid.Empty,
                ContactId = contactId,
            }, ct);

            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<TdsProfile>(ct)
                : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }
}
