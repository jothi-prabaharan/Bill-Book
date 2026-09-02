using Sales.Api.Services;
using Shared.Kernel.Documents;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;

namespace Sales.Api.Tests;

/// <summary>The branch's own currency, fixed for tests. Master is not running here.</summary>
public sealed class StubBaseCurrency(string? currency = "INR") : IBaseCurrencyProvider
{
    public Task<string?> GetBaseCurrencyAsync(CancellationToken ct = default) =>
        Task.FromResult(currency);
}

/// <summary>
/// The branch's state and its discount rule. <c>33</c> is Tamil Nadu, so a
/// customer GSTIN starting <c>33</c> is intra-state and anything else is not —
/// which is what decides CGST + SGST against IGST.
/// </summary>
public sealed class StubBranchSettings(string stateCode = "33", bool discountBeforeTax = true)
    : IBranchSettingsProvider
{
    public Task<BranchSettings?> GetSettingsAsync(CancellationToken ct = default) =>
        Task.FromResult<BranchSettings?>(new BranchSettings(stateCode, discountBeforeTax));
}

/// <summary>
/// One 18% group, resolved for any date. Real rate resolution is effective-dated
/// and lives in Accounting; what these tests need is that the service asks for a
/// rate and uses the one it is given.
/// </summary>
public sealed class StubTaxRates(decimal percent = 18m) : ITaxRateProvider
{
    private TaxRate Rate(long taxGroupId) => new(
        TaxMasterId: taxGroupId,
        TaxGroupId: taxGroupId,
        TaxSystemName: $"GST {percent}%",
        TotalRate: percent,
        CgstRate: percent / 2m,
        SgstRate: percent / 2m,
        IgstRate: percent,
        CessRate: 0m);

    public Task<IReadOnlyDictionary<long, TaxRate>?> GetRatesAsync(
        DateOnly onDate, CancellationToken ct = default)
    {
        IReadOnlyDictionary<long, TaxRate> rates = new Dictionary<long, TaxRate>
        {
            [1] = Rate(1),
            [2] = Rate(2),
        };

        return Task.FromResult<IReadOnlyDictionary<long, TaxRate>?>(rates);
    }

    public async Task<TaxRate?> GetRateAsync(
        long taxGroupId, DateOnly onDate, CancellationToken ct = default)
    {
        IReadOnlyDictionary<long, TaxRate>? rates = await GetRatesAsync(onDate, ct);
        return rates is not null && rates.TryGetValue(taxGroupId, out TaxRate? rate) ? rate : null;
    }
}

public sealed class StubFinancialYear(int month = 4) : IFinancialYearProvider
{
    public Task<int> GetStartMonthAsync(CancellationToken ct = default) => Task.FromResult(month);
}

public sealed class StubCurrentUser(Guid? userId = null) : ICurrentUser
{
    public Guid? UserId { get; } = userId ?? Guid.NewGuid();

    public Guid? CustomerId => null;

    public Guid? OrgId => null;

    public int? RoleId => null;
}

/// <summary>
/// Names that would come from Master and Inventory. Returns a label for every id
/// asked for, so a test can assert the batching happened without a server.
/// </summary>
public sealed class StubNameLookup : IContactNameLookup, IItemNameLookup
{
    /// <summary>Every id this lookup was asked about, in the order the calls came.</summary>
    public List<IReadOnlyCollection<long>> Calls { get; } = [];

    public Task<IReadOnlyDictionary<long, NamedRef>> ResolveAsync(
        IReadOnlyCollection<long> ids, CancellationToken ct = default)
    {
        Calls.Add(ids);

        IReadOnlyDictionary<long, NamedRef> named = ids.ToDictionary(
            id => id,
            id => new NamedRef(id, $"C{id}", $"Name {id}"));

        return Task.FromResult(named);
    }
}

/// <summary>
/// The ledger, recorded rather than posted. Every service that reaches
/// Accounting takes this the same way <see cref="RecordingInventory"/> stands
/// in for Inventory — a test asserting what a document posted should not also
/// have to stand up Accounting over HTTP.
/// </summary>
public sealed class RecordingLedger : ILedgerClient
{
    public List<PostLedgerRequest> Posts { get; } = [];

    public List<AllocateTransactionRequest> Allocations { get; } = [];

    /// <summary>Set to make the next post refused, so a service's rollback path is reachable.</summary>
    public string? RefusePostWith { get; set; }

    public Task<PostLedgerOutcomeResult> PostAsync(PostLedgerRequest request, CancellationToken ct)
    {
        if (RefusePostWith is not null)
        {
            return Task.FromResult(new PostLedgerOutcomeResult(false, RefusePostWith));
        }

        Posts.Add(request);
        return Task.FromResult(new PostLedgerOutcomeResult(true, null));
    }

    public Task<AllocateOutcomeResult> AllocateAsync(AllocateTransactionRequest request, CancellationToken ct)
    {
        Allocations.Add(request);
        return Task.FromResult(new AllocateOutcomeResult(true, null));
    }

    public Task RemoveAllocationsAsync(RemoveAllocationsRequest request, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<List<OutstandingBalanceView>> GetAllOutstandingBalancesAsync(
        int ledgerTypeId, CancellationToken ct) =>
        Task.FromResult(new List<OutstandingBalanceView>());

    public Task<List<OutstandingBalanceView>> GetOutstandingBalancesAsync(
        long contactId, int ledgerTypeId, CancellationToken ct) =>
        Task.FromResult(new List<OutstandingBalanceView>());

    public Task<IReadOnlyDictionary<long, Settlement>> GetSettlementsAsync(
        SettlementQueryRequest request, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<long, Settlement>>(
            new Dictionary<long, Settlement>());
}

/// <summary>The credit check, which says yes unless a test tells it otherwise.</summary>
public sealed class StubCreditCheck : ICreditCheckClient
{
    public string? RefuseWith { get; set; }

    public Task<CreditEvaluateResponse> EvaluateAsync(
        long contactId, decimal newOrderAmountBase, CancellationToken ct) =>
        Task.FromResult(new CreditEvaluateResponse
        {
            Allowed = RefuseWith is null,
            Reason = RefuseWith,
        });
}

/// <summary>
/// Inventory, recorded rather than called.
///
/// A sales order's whole effect on the world is the reservation it takes and
/// gives back, so these tests need to see exactly which items were reserved and
/// which were released — not to stand up the Inventory service over HTTP and
/// make the test about the network.
/// </summary>
public sealed class RecordingInventory : IInventoryClient
{
    public List<ReserveStockRequest> Reservations { get; } = [];

    public List<ReleaseStockRequest> Releases { get; } = [];

    /// <summary>Item ids Inventory should refuse to reserve, and the outcome it gives.</summary>
    public Dictionary<long, string> RefuseReserve { get; } = [];

    /// <summary>Set to make every release fail, so the void's refusal path is reachable.</summary>
    public bool FailReleases { get; set; }

    public Task<ReserveStockResponse> ReserveAsync(ReserveStockRequest request, CancellationToken ct)
    {
        Reservations.Add(request);

        ReserveStockResponse response = new() { Success = true };

        foreach (ReserveStockLine line in request.Lines)
        {
            bool refused = RefuseReserve.TryGetValue(line.ItemId, out string? outcome);
            if (refused)
            {
                response.Success = false;
            }

            response.Lines.Add(new ReserveStockLineResult
            {
                ItemId = line.ItemId,
                RequestedQuantity = line.Quantity,
                Success = !refused,
                Outcome = outcome ?? "Ok",
            });
        }

        return Task.FromResult(response);
    }

    public Task<ReleaseStockResponse> ReleaseAsync(ReleaseStockRequest request, CancellationToken ct)
    {
        Releases.Add(request);

        ReleaseStockResponse response = new() { Success = !FailReleases };

        foreach (ReleaseStockLine line in request.Lines)
        {
            response.Lines.Add(new ReleaseStockLineResult
            {
                ItemId = line.ItemId,
                RequestedQuantity = line.Quantity,
                Success = !FailReleases,
                Outcome = FailReleases ? "Failed" : "Ok",
            });
        }

        return Task.FromResult(response);
    }

    public Task<IssueStockResponse> IssueAsync(IssueStockRequest request, CancellationToken ct) =>
        Task.FromResult(new IssueStockResponse { Success = true });

    public Task<ReceiveStockResponse> ReceiveAsync(ReceiveStockRequest request, CancellationToken ct) =>
        Task.FromResult(new ReceiveStockResponse { Success = true });

    /// <summary>
    /// What the stub says is available. Empty by default, which is how the real
    /// client answers when Inventory cannot be reached — an advisory read that
    /// fails must not fail the screen.
    /// </summary>
    public Dictionary<long, decimal> Available { get; } = [];

    public Task<StockAvailabilityResponse> GetAvailabilityAsync(
        StockAvailabilityRequest request, CancellationToken ct) =>
        Task.FromResult(new StockAvailabilityResponse
        {
            Lines = [.. request.ItemIds
                .Where(Available.ContainsKey)
                .Select(id => new StockAvailabilityLine
                {
                    ItemId = id,
                    QuantityOnHand = Available[id],
                    QuantityReserved = 0m,
                    QuantityAvailable = Available[id],
                    IsTracked = true,
                })],
        });
}
