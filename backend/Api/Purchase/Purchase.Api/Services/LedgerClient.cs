using System.Net.Http.Json;

namespace Purchase.Api.Services;

/// <summary>
/// Where Purchase posts to the general ledger.
///
/// <b>No other service writes GL rows.</b> Purchase decides which accounts a
/// receipt or a bill touches — it is the only service that knows the vendor and
/// which part of the figure is reclaimable tax — and Accounting decides whether
/// the result is a legal posting.
/// </summary>
public interface ILedgerClient
{
    Task<PostLedgerOutcomeResult> PostAsync(PostLedgerRequest request, CancellationToken ct);
}

public sealed class LedgerClient : ILedgerClient
{
    private readonly HttpClient _http;

    public LedgerClient(HttpClient http) => _http = http;

    public async Task<PostLedgerOutcomeResult> PostAsync(
        PostLedgerRequest request, CancellationToken ct)
    {
        HttpResponseMessage response =
            await _http.PostAsJsonAsync("internal/ledger/postings", request, ct);

        if (response.IsSuccessStatusCode)
        {
            return new PostLedgerOutcomeResult(true, null);
        }

        // The detail is worth carrying rather than swallowing: Accounting's
        // refusals name the leg and the account, and that is what tells a
        // missing GRNI account apart from an unbalanced posting.
        string detail = await response.Content.ReadAsStringAsync(ct);

        return new PostLedgerOutcomeResult(
            false, $"The ledger refused the posting ({(int)response.StatusCode}): {detail}");
    }
}

public sealed record PostLedgerOutcomeResult(bool Posted, string? Detail);

/// <summary>
/// A posting, in Accounting's own shape.
///
/// <b>A leg is a debit or a credit, never both and never negative.</b> Sales'
/// copy of this model carries a single signed <c>Amount</c>, which Accounting
/// would refuse on every leg — see <c>LedgerPostingService</c>, which checks
/// exactly that. This one matches the contract.
/// </summary>
public sealed class PostLedgerRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public string TransactionTypeCode { get; set; } = null!;

    public long TransactionId { get; set; }

    public DateOnly LedgerDate { get; set; }

    public string? CurrencyCode { get; set; }

    public decimal? ExchangeRate { get; set; }

    public long? ContactId { get; set; }

    public long? SourceDocumentId { get; set; }

    /// <summary>Which leg types to clear when <see cref="Legs"/> is empty — a withdrawal.</summary>
    public List<int> WithdrawLedgerTypeIds { get; set; } = [];

    public List<LedgerLegRequest> Legs { get; set; } = [];
}

public sealed class LedgerLegRequest
{
    /// <summary>1 ITEM, 2 TAX, 3 CONTROL, 4 COGS, 5 FX, 6 ROUNDOFF.</summary>
    public int LedgerTypeId { get; set; }

    /// <summary>From <c>mst.LedgerSources</c>. 1 is an ordinary document posting.</summary>
    public int LedgerSourceId { get; set; }

    /// <summary>The document line, or 0 when the leg is not line-level.</summary>
    public long TransactionDetailId { get; set; }

    /// <summary>
    /// How a caller outside Accounting names an account. An account id is a
    /// per-organization number in a database Purchase does not read, so resolving
    /// one here is how a leg lands on the wrong account.
    /// </summary>
    public string? AccountSystemName { get; set; }

    /// <summary>
    /// Which sub-account under the control account this leg is really about.
    /// <c>1</c> Contact, <c>2</c> Item, <c>3</c> Tax — sent as a number because
    /// Purchase does not reference Accounting's assemblies.
    ///
    /// <b>The Accounts Payable leg needs one or payables aging is a single
    /// number with no vendors in it.</b>
    /// </summary>
    public int? SubAccountReferenceType { get; set; }

    public long? SubAccountReferenceId { get; set; }

    /// <summary>
    /// Which of a contact's balances under the control account: <c>0</c> the
    /// trade balance, and the advances above it. Part of the key, not a
    /// refinement — a contact has three sub-accounts under Accounts Payable and
    /// the reference alone matches all three.
    /// </summary>
    public int SubAccountPurpose { get; set; }

    /// <summary>
    /// Which component of a tax rate: <c>0</c> none, <c>1</c> CGST, <c>2</c>
    /// SGST, <c>3</c> IGST. The same story as the purpose — three sub-accounts
    /// share one parent and one rate.
    /// </summary>
    public int SubAccountTaxComponent { get; set; }

    public decimal DebitAmount { get; set; }

    public decimal CreditAmount { get; set; }

    public string? TransactionDesc { get; set; }
}
