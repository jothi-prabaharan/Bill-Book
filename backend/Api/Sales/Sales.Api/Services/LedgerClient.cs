using System.Net.Http.Json;

namespace Sales.Api.Services;

/// <summary>
/// Where Sales posts to the general ledger.
///
/// <b>Matches Accounting's contract exactly.</b> The old Sales model carried a
/// single <c>Amount</c> which Accounting refused on every leg — see
/// <c>LedgerPostingService</c> which checks that a leg is a debit xor credit,
/// never both, never negative. This model uses <c>DebitAmount</c> and
/// <c>CreditAmount</c> and includes the sub-account reference type that
/// completes the key.
/// </summary>
public interface ILedgerClient
{
    Task<PostLedgerOutcomeResult> PostAsync(PostLedgerRequest request, CancellationToken ct);
    Task<bool> AllocateAsync(AllocateTransactionRequest request, CancellationToken ct);
}

public sealed class LedgerClient : ILedgerClient
{
    private readonly HttpClient _http;

    public LedgerClient(HttpClient http) => _http = http;

    public async Task<bool> AllocateAsync(AllocateTransactionRequest request, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("internal/allocations", request, ct);
        if (response.IsSuccessStatusCode)
            return true;

        var content = await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException($"Allocation failed: {response.StatusCode} {content}");
    }

    public async Task<PostLedgerOutcomeResult> PostAsync(
        PostLedgerRequest request, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("internal/ledger/postings", request, ct);

        if (response.IsSuccessStatusCode)
        {
            return new PostLedgerOutcomeResult(true, null);
        }

        // Accounting's refusals name the leg and the account — worth carrying
        // rather than swallowing: a missing GRNI account is different from
        // an unbalanced posting.
        var detail = await response.Content.ReadAsStringAsync(ct);

        return new PostLedgerOutcomeResult(
            false, $"The ledger refused the posting ({(int)response.StatusCode}): {detail}");
    }
}

public sealed record PostLedgerOutcomeResult(bool Posted, string? Detail);

/// <summary>
/// A posting, in Accounting's own shape.
///
/// <b>A leg is a debit or a credit, never both and never negative.</b>
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
    /// <summary><c>mst.LedgerTypes</c>: 1 ITEM, 2 TAX, 3 CONTROL, 4 COGS, 5 FX, 6 ROUNDOFF.</summary>
    public int LedgerTypeId { get; set; }

    /// <summary>From <c>mst.LedgerSources</c>. 1 is an ordinary document posting.</summary>
    public int LedgerSourceId { get; set; }

    /// <summary>The document line, or 0 when the leg is not line-level.</summary>
    public long TransactionDetailId { get; set; }

    /// <summary>
    /// How a caller outside Accounting names an account. An account id is a
    /// per-organization number in a database Sales does not read, so resolving
    /// one here is how a leg lands on the wrong account.
    /// </summary>
    public string? AccountSystemName { get; set; }

    /// <summary>
    /// Which sub-account under the control account this leg is really about.
    /// 1 = Contact, 2 = Item, 3 = Tax. Sent as a number because Sales does not
    /// reference Accounting's assemblies.
    ///
    /// <b>The Accounts Receivable leg needs one or receivables aging is a single
    /// number with no contacts in it.</b>
    /// </summary>
    public int? SubAccountReferenceType { get; set; }

    public long? SubAccountReferenceId { get; set; }

    /// <summary>
    /// Which of a contact's balances under the control account: 0 = the
    /// trade balance, 1 = prepayment advance, 2 = overpayment advance.
    /// Part of the key, not a refinement — a contact has three sub-accounts
    /// under Accounts Receivable and the reference alone matches all three.
    /// </summary>
    public int SubAccountPurpose { get; set; }

    /// <summary>
    /// Which component of a tax rate: 0 = none, 1 = CGST, 2 = SGST, 3 = IGST.
    /// The same story as the purpose — three sub-accounts share one parent
    /// and one rate.
    /// </summary>
    public int SubAccountTaxComponent { get; set; }

    public decimal DebitAmount { get; set; }

    public decimal CreditAmount { get; set; }

    public string? TransactionDesc { get; set; }
}

public sealed class AllocateTransactionRequest
{
    public string SourceTransactionTypeCode { get; set; } = null!;
    public long SourceTransactionId { get; set; }
    public string TargetTransactionTypeCode { get; set; } = null!;
    public long TargetTransactionId { get; set; }
    public decimal Amount { get; set; }
}
