using System.Net;
using System.Net.Http.Json;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Tax;

/// <summary>
/// What a document asks Accounting about TDS (TK-117): who, when, how much
/// before GST, and whether it is a bill or an advance.
/// </summary>
public sealed class TdsQuoteRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public long ContactId { get; set; }

    /// <summary>BIL or SPM. The document's own earlier row is left out of the year's total, so a retry does not count it twice.</summary>
    public string TransactionTypeCode { get; set; } = null!;

    public long TransactionId { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>In base currency, before GST.</summary>
    public decimal Amount { get; set; }

    /// <summary>1 a bill (credit), 2 an advance. Accounting's <c>TdsEntryKind</c>, by value.</summary>
    public int Kind { get; set; } = 1;
}

/// <summary>
/// Accounting's answer, and what the document sends back with its posting so
/// the register row is written in the ledger's transaction.
/// </summary>
public sealed class TdsQuote
{
    /// <summary>False when the contact takes no TDS; nothing is written and nothing deducted.</summary>
    public bool Applies { get; set; }

    public int Kind { get; set; } = 1;

    public string? SectionCode { get; set; }

    public string? Pan { get; set; }

    /// <summary>Accounting's <c>TdsDeducteeKind</c>, by value.</summary>
    public int DeducteeKind { get; set; }

    public int FinancialYear { get; set; }

    public decimal Amount { get; set; }

    public decimal CoveredByAdvance { get; set; }

    public decimal TdsBase { get; set; }

    public decimal Rate { get; set; }

    public string? CertificateNo { get; set; }

    public decimal CertificateBase { get; set; }

    public decimal? CertificateRate { get; set; }

    public decimal TdsAmount { get; set; }

    public string? Note { get; set; }
}

/// <summary>How a quote came back.</summary>
public enum TdsQuoteOutcome
{
    Ok = 0,

    /// <summary>Accounting or Master could not be asked. Worth retrying.</summary>
    Unavailable = 1,

    /// <summary>The contact names a section the branch has no row for on that date. The detail says which.</summary>
    Refused = 2,
}

public sealed record TdsQuoteResult(TdsQuoteOutcome Outcome, TdsQuote? Quote, string? Detail);

public interface ITdsQuoteClient
{
    Task<TdsQuoteResult> QuoteAsync(TdsQuoteRequest request, CancellationToken ct);
}

/// <summary>Asks Accounting over the internal key.</summary>
public sealed class HttpTdsQuoteClient : ITdsQuoteClient
{
    private readonly HttpClient _http;

    public HttpTdsQuoteClient(HttpClient http) => _http = http;

    public async Task<TdsQuoteResult> QuoteAsync(TdsQuoteRequest request, CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await _http.PostAsJsonAsync("internal/tds/quote", request, ct);

            if (response.IsSuccessStatusCode)
            {
                return new TdsQuoteResult(TdsQuoteOutcome.Ok, await response.Content.ReadFromJsonAsync<TdsQuote>(ct), null);
            }

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                TdsRefusal? refusal = await response.Content.ReadFromJsonAsync<TdsRefusal>(ct);
                return new TdsQuoteResult(TdsQuoteOutcome.Refused, null, refusal?.Message);
            }

            return new TdsQuoteResult(TdsQuoteOutcome.Unavailable, null, null);
        }
        catch (HttpRequestException)
        {
            return new TdsQuoteResult(TdsQuoteOutcome.Unavailable, null, null);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new TdsQuoteResult(TdsQuoteOutcome.Unavailable, null, null);
        }
    }

    private sealed class TdsRefusal
    {
        public string? Message { get; set; }
    }
}

/// <summary>Whether a branch has a TDS section by this code, for the contact form's check.</summary>
public interface ITdsSectionDirectory
{
    /// <summary>True or false, or null when Accounting could not be asked.</summary>
    Task<bool?> ExistsAsync(string sectionCode, CancellationToken ct);
}

public sealed class HttpTdsSectionDirectory : ITdsSectionDirectory
{
    private readonly HttpClient _http;
    private readonly ITenantContext _tenant;

    public HttpTdsSectionDirectory(HttpClient http, ITenantContext tenant)
    {
        _http = http;
        _tenant = tenant;
    }

    public async Task<bool?> ExistsAsync(string sectionCode, CancellationToken ct)
    {
        if (_tenant.CustomerId is not Guid customerId || _tenant.OrgId is not Guid orgId)
        {
            return null;
        }

        try
        {
            using HttpResponseMessage response = await _http.GetAsync(
                $"internal/tds/sections/exists?customerId={customerId}&orgId={orgId}&code={Uri.EscapeDataString(sectionCode)}", ct);

            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<bool>(ct) : null;
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
