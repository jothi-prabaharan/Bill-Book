using System.Net.Http.Json;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Ledgers;

// Which ledger and bank accounts another service may name (School fees, TK-64).
// Ids are per branch in Accounting's database, which no other service reads, so
// they are listed and checked through Accounting's internal routes.

public sealed class AccountLookupRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    /// <summary><c>mst.AccountTypes</c> ids to list: 1 Asset, 2 Liability, 3 Equity, 4 Income, 5 Expense. Empty lists none.</summary>
    public List<int> AccountTypeIds { get; set; } = [];

    /// <summary>Particular accounts to look up, whatever their type.</summary>
    public List<long> AccountIds { get; set; } = [];
}

public sealed class AccountSummary
{
    public long AccountId { get; set; }

    public string AccountCode { get; set; } = null!;

    public string AccountName { get; set; } = null!;

    public string? AccountSystemName { get; set; }

    public int AccountTypeId { get; set; }

    public bool IsActive { get; set; }

    public bool IsContra { get; set; }

    /// <summary>A grouping row: nothing posts to it.</summary>
    public bool IsLock { get; set; }
}

public sealed class BankAccountLookupRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }
}

public sealed class BankAccountSummary
{
    public long BankAccountId { get; set; }

    public string AccountName { get; set; } = null!;

    /// <summary>The last four digits only.</summary>
    public string MaskedNumber { get; set; } = null!;

    public bool IsCash { get; set; }
}

public interface IAccountDirectory
{
    /// <summary>Throws when Accounting cannot be asked.</summary>
    Task<IReadOnlyList<AccountSummary>> AccountsAsync(IEnumerable<int> accountTypeIds, IEnumerable<long> accountIds, CancellationToken ct);

    /// <summary>The branch's active bank and cash accounts. Throws when Accounting cannot be asked.</summary>
    Task<IReadOnlyList<BankAccountSummary>> BankAccountsAsync(CancellationToken ct);
}

public sealed class HttpAccountDirectory : IAccountDirectory
{
    private readonly HttpClient _http;
    private readonly ITenantContext _tenant;

    public HttpAccountDirectory(HttpClient http, ITenantContext tenant)
    {
        _http = http;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<AccountSummary>> AccountsAsync(IEnumerable<int> accountTypeIds, IEnumerable<long> accountIds, CancellationToken ct)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync("internal/accounts/lookup", new AccountLookupRequest
        {
            CustomerId = _tenant.CustomerId ?? Guid.Empty,
            OrgId = _tenant.OrgId ?? Guid.Empty,
            AccountTypeIds = [.. accountTypeIds],
            AccountIds = [.. accountIds],
        }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<AccountSummary>>(ct) ?? [];
    }

    public async Task<IReadOnlyList<BankAccountSummary>> BankAccountsAsync(CancellationToken ct)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync("internal/accounts/bank-accounts", new BankAccountLookupRequest
        {
            CustomerId = _tenant.CustomerId ?? Guid.Empty,
            OrgId = _tenant.OrgId ?? Guid.Empty,
        }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<BankAccountSummary>>(ct) ?? [];
    }
}
