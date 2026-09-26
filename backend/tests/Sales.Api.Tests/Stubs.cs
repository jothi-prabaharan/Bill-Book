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
/// The branch, as the seller block on a printed invoice sees it.
///
/// <c>identity: null</c> stands for a branch Master could not resolve, which
/// must stop the posting rather than print a placeholder.
/// </summary>
public sealed class StubOrgIdentity(OrgIdentity? identity = null) : IOrgIdentityProvider
{
    private readonly OrgIdentity _identity =
        identity ?? new OrgIdentity(
            "Test Traders", "33AAAAA0000A1Z5", "1 Test Street", null, "Chennai", "33", "600001");

    public bool Resolves { get; init; } = true;

    public Task<OrgIdentity?> GetIdentityAsync(CancellationToken ct = default) =>
        Task.FromResult<OrgIdentity?>(Resolves ? _identity : null);
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

    /// <summary>Set to make the next allocation refused, the way Accounting's guard refuses an overclaim.</summary>
    public string? RefuseAllocationWith { get; set; }

    /// <summary>Every claim released, in order.</summary>
    public List<RemoveAllocationsRequest> RemovedAllocations { get; } = [];

    public Task<AllocateOutcomeResult> AllocateAsync(AllocateTransactionRequest request, CancellationToken ct)
    {
        if (RefuseAllocationWith is not null)
        {
            return Task.FromResult(new AllocateOutcomeResult(false, RefuseAllocationWith));
        }

        Allocations.Add(request);
        return Task.FromResult(new AllocateOutcomeResult(true, null));
    }

    public Task RemoveAllocationsAsync(RemoveAllocationsRequest request, CancellationToken ct)
    {
        RemovedAllocations.Add(request);
        return Task.CompletedTask;
    }

    public Task<List<OutstandingBalanceView>> GetAllOutstandingBalancesAsync(
        int ledgerTypeId, CancellationToken ct) =>
        Task.FromResult(new List<OutstandingBalanceView>());

    public Task<List<OutstandingBalanceView>> GetOutstandingBalancesAsync(
        long contactId, int ledgerTypeId, CancellationToken ct) =>
        Task.FromResult(new List<OutstandingBalanceView>());

    /// <summary>What Accounting says each document has settled, by type code and id. Empty by default.</summary>
    public Dictionary<(string Code, long Id), Settlement> Settlements { get; } = [];

    /// <summary>Every settlement question asked, in order.</summary>
    public List<SettlementQueryRequest> SettlementQueries { get; } = [];

    public Task<IReadOnlyDictionary<long, Settlement>> GetSettlementsAsync(
        SettlementQueryRequest request, CancellationToken ct)
    {
        SettlementQueries.Add(request);
        return Task.FromResult<IReadOnlyDictionary<long, Settlement>>(
            request.TransactionIds
                .Where(id => Settlements.ContainsKey((request.TransactionTypeCode, id)))
                .ToDictionary(id => id, id => Settlements[(request.TransactionTypeCode, id)]));
    }
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

    /// <summary>Every issue asked for, in order.</summary>
    public List<IssueStockRequest> Issues { get; } = [];

    /// <summary>Set to make every issue refused, so a service's stock-refused path is reachable.</summary>
    public bool RefuseIssues { get; set; }

    /// <summary>
    /// A unit cost per item (TK-90). An item here is issued as a real movement:
    /// a fresh id, a line value at this cost, and that value recorded in
    /// <see cref="MovementCosts"/> as what Inventory holds for it.
    /// </summary>
    public Dictionary<long, decimal> IssueUnitCost { get; } = [];

    /// <summary>What each movement costs now. A test restating a movement edits it here.</summary>
    public Dictionary<long, decimal> MovementCosts { get; } = [];

    /// <summary>Set to answer the movement-cost read as an unreachable Inventory does.</summary>
    public bool MovementCostsUnreachable { get; set; }

    private long _nextMovementId = 70_000;

    public Task<StockMovementCostsResponse?> GetMovementCostsAsync(
        StockMovementCostsRequest request, CancellationToken ct) =>
        Task.FromResult(MovementCostsUnreachable
            ? null
            : new StockMovementCostsResponse
            {
                Lines = [.. request.StockMovementIds
                    .Where(MovementCosts.ContainsKey)
                    .Select(id => new StockMovementCostLine { StockMovementId = id, TotalCost = MovementCosts[id] })],
            });

    /// <summary>
    /// Each issue succeeds and answers every line with no movement and no cost —
    /// what the stub always answered, so a suite that never looks at issues is
    /// unaffected by their being recorded.
    /// </summary>
    public Task<IssueStockResponse> IssueAsync(IssueStockRequest request, CancellationToken ct)
    {
        Issues.Add(request);

        return Task.FromResult(new IssueStockResponse
        {
            Success = !RefuseIssues,
            Lines = [.. request.Lines.Select(Issue)],
        });

        IssueStockLineResult Issue(IssueStockLine line)
        {
            if (RefuseIssues || !IssueUnitCost.TryGetValue(line.ItemId, out decimal unitCost))
            {
                return new IssueStockLineResult
                {
                    SourceLineId = line.SourceLineId,
                    ItemId = line.ItemId,
                    RequestedQuantity = line.Quantity,
                    Success = !RefuseIssues,
                    Outcome = RefuseIssues ? "InsufficientStock" : "Ok",
                };
            }

            long movementId = Interlocked.Increment(ref _nextMovementId);
            decimal value = Math.Round(line.Quantity * unitCost, 2, MidpointRounding.AwayFromZero);
            MovementCosts[movementId] = value;

            return new IssueStockLineResult
            {
                SourceLineId = line.SourceLineId,
                ItemId = line.ItemId,
                RequestedQuantity = line.Quantity,
                Success = true,
                Outcome = "Ok",
                StockMovementId = movementId,
                UnitCost = unitCost,
                LineValue = value,
            };
        }
    }

    /// <summary>Every receipt asked for, in order — a credit note's returns among them.</summary>
    public List<ReceiveStockRequest> Receipts { get; } = [];

    /// <summary>Set to make every receipt refused.</summary>
    public bool RefuseReceipts { get; set; }

    public Task<ReceiveStockResponse> ReceiveAsync(ReceiveStockRequest request, CancellationToken ct)
    {
        Receipts.Add(request);
        return Task.FromResult(new ReceiveStockResponse { Success = !RefuseReceipts });
    }

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

/// <summary>
/// Saves nothing and reports the key back. Public here rather than private to
/// one test class, so a second suite needing an InvoiceService does not have to
/// write its own.
/// </summary>
public sealed class StubDocumentStorage : Shared.Kernel.Storage.IFileStorage
{
    public Task<string> SaveAsync(string key, Stream content, string contentType, Shared.Kernel.Storage.FileWriteMode mode = Shared.Kernel.Storage.FileWriteMode.CreateNew, CancellationToken ct = default) =>
        Task.FromResult(key);

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default) =>
        Task.FromResult<Stream?>(null);

    public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;

    public Task<Uri?> GetDownloadUrlAsync(string key, TimeSpan lifetime, CancellationToken ct = default) =>
        Task.FromResult<Uri?>(null);
}

/// <summary>Renders a single byte, which is enough for anything not about the PDF.</summary>
public sealed class StubInvoicePdf : Sales.Api.Services.Pdf.IInvoicePdfRenderer
{
    public byte[] Render(Sales.Api.Services.Pdf.PdfInvoiceModel model) => [0x25];
}

/// <summary>
/// Keeps every file it is given, in memory, and honours the write mode the way
/// the real stores do: a create-only save over an existing key is refused. So a
/// test can post, read the file back, and see a retry write over its leftover.
/// </summary>
public sealed class RecordingDocumentStorage : Shared.Kernel.Storage.IFileStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public List<(string Key, Shared.Kernel.Storage.FileWriteMode Mode)> Saves { get; } = [];

    public async Task<string> SaveAsync(string key, Stream content, string contentType, Shared.Kernel.Storage.FileWriteMode mode = Shared.Kernel.Storage.FileWriteMode.CreateNew, CancellationToken ct = default)
    {
        if (mode == Shared.Kernel.Storage.FileWriteMode.CreateNew && Files.ContainsKey(key))
        {
            throw new Shared.Kernel.Storage.StorageKeyExistsException(key);
        }

        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, ct);
        Files[key] = copy.ToArray();
        Saves.Add((key, mode));
        return key;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default) =>
        Task.FromResult<Stream?>(Files.TryGetValue(key, out byte[]? bytes) ? new MemoryStream(bytes) : null);

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        Files.Remove(key);
        return Task.CompletedTask;
    }

    public Task<Uri?> GetDownloadUrlAsync(string key, TimeSpan lifetime, CancellationToken ct = default) =>
        Task.FromResult<Uri?>(null);
}

/// <summary>Keeps each model it draws, so a test can read what would have printed.</summary>
public sealed class RecordingSalesPdf : Sales.Api.Services.Pdf.ISalesDocumentPdfRenderer
{
    public List<Sales.Api.Services.Pdf.SalesPdfModel> Rendered { get; } = [];

    public byte[] Render(Sales.Api.Services.Pdf.SalesPdfModel model)
    {
        Rendered.Add(model);
        return "%PDF"u8.ToArray();
    }
}

/// <summary>The archive a credit note or challan service is built with in tests.</summary>
public static class TestArchive
{
    public static Sales.Api.Services.Pdf.SalesDocumentArchive For(
        Sales.Repository.SalesDbContext db,
        ITenantContext tenant,
        Shared.Kernel.Storage.IFileStorage? storage = null,
        Sales.Api.Services.Pdf.ISalesDocumentPdfRenderer? pdf = null) =>
        new(db, tenant, storage ?? new StubDocumentStorage(), new StubOrgIdentity(),
            new StubNameLookup(), new StubNameLookup(), pdf ?? new RecordingSalesPdf());
}

/// <summary>
/// Inventory's UQC answer (TK-91): unit 1 is <c>NOS</c>, unit 2 <c>KGS</c>, and
/// every other unit is unknown. <see cref="Fail"/> makes it throw as an
/// unreachable Inventory does.
/// </summary>
internal sealed class StubUqcLookup : Shared.Kernel.Stock.IUqcLookup
{
    public bool Fail { get; set; }

    public Task<IReadOnlyDictionary<long, string>> FindAsync(IEnumerable<long> uomIds, CancellationToken ct)
    {
        if (Fail)
        {
            throw new HttpRequestException("Inventory is down.");
        }

        var known = new Dictionary<long, string> { [1] = "NOS", [2] = "KGS" };
        IReadOnlyDictionary<long, string> found = uomIds.Distinct()
            .Where(known.ContainsKey)
            .ToDictionary(id => id, id => known[id]);
        return Task.FromResult(found);
    }
}

/// <summary>
/// E-invoicing that records what posting and voiding asked of it (TK-92).
/// Needs no IRN by default; <see cref="VoidRefusal"/> makes a void refused.
/// </summary>
internal sealed class StubEInvoicing : Sales.Api.Services.EInvoicing.IEInvoicePosting
{
    public List<(Sales.Entity.Enums.EInvoiceSource Source, long Id)> Posted { get; } = [];

    public List<(Sales.Entity.Enums.EInvoiceSource Source, long Id, string Reason)> Voided { get; } = [];

    public string? VoidRefusal { get; set; }

    public Task<Sales.Entity.Models.EInvoiceStateView?> OnPostedAsync(
        Sales.Entity.Enums.EInvoiceSource source, long sourceId, DocumentHeaderBase document, CancellationToken ct)
    {
        Posted.Add((source, sourceId));
        return Task.FromResult<Sales.Entity.Models.EInvoiceStateView?>(null);
    }

    public Task<string?> BeforeVoidAsync(
        Sales.Entity.Enums.EInvoiceSource source, long sourceId, string reason,
        Sales.Entity.Enums.EInvoiceCancelReason? cancelReason, CancellationToken ct)
    {
        Voided.Add((source, sourceId, reason));
        return Task.FromResult(VoidRefusal);
    }
}
