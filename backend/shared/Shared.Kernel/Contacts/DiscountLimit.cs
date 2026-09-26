using System.Net.Http.Json;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Contacts;

/// <summary>
/// The most a line may be discounted for one contact (D-29, TK-102): the
/// contact's own <c>MaxDiscountPercent</c> when set, else the branch's
/// <c>sales.maxLineDiscountPercent</c>. Master holds both, so Master answers.
/// </summary>
public sealed class DiscountLimitRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public long ContactId { get; set; }
}

public sealed class DiscountLimitResponse
{
    /// <summary>The limit as a percentage of the line's gross value, 0 to 100. 100 is no limit.</summary>
    public decimal LimitPercent { get; set; } = 100m;

    /// <summary>Where it came from: <c>Contact</c> or <c>Branch</c>.</summary>
    public string Source { get; set; } = "Branch";
}

public interface IDiscountLimitClient
{
    /// <summary>The limit for <paramref name="contactId"/>, or null when Master could not be asked.</summary>
    Task<DiscountLimitResponse?> LimitForAsync(long contactId, CancellationToken ct);
}

/// <summary>Asks Master over the internal key, naming the branch in the body.</summary>
public sealed class HttpDiscountLimitClient : IDiscountLimitClient
{
    private readonly HttpClient _http;
    private readonly ITenantContext _tenant;

    public HttpDiscountLimitClient(HttpClient http, ITenantContext tenant)
    {
        _http = http;
        _tenant = tenant;
    }

    public async Task<DiscountLimitResponse?> LimitForAsync(long contactId, CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await _http.PostAsJsonAsync("internal/contacts/discount-limit", new DiscountLimitRequest
            {
                CustomerId = _tenant.CustomerId ?? Guid.Empty,
                OrgId = _tenant.OrgId ?? Guid.Empty,
                ContactId = contactId,
            }, ct);

            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<DiscountLimitResponse>(ct)
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
