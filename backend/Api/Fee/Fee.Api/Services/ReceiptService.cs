using Fee.Entity.Enums;
using Fee.Entity.Models;
using Fee.Entity.TableEntities;
using Fee.Repository;
using Fee.Repository.SeedData;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Contacts;
using Shared.Kernel.Ledgers;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;

namespace Fee.Api.Services;

/// <summary>
/// Fee receipts and their allocation (S4, TK-64).
///
/// <list type="bullet">
/// <item>Money from a guardian settles their posted demands: the ones chosen,
/// or the oldest first. What is left over is held as their advance.</item>
/// <item><b>A demand's paid amount moves only by a guarded update</b> whose
/// row count is the answer, so two receipts at two desks cannot both settle
/// the last rupee of one demand.</item>
/// <item>The receipt posts Dr bank, Cr the guardian's receivable for what it
/// settles and Cr their overpayment advance for the rest, so the receivable
/// ties to the open demands.</item>
/// </list>
/// </summary>
public sealed class ReceiptService
{
    public const string TypeCode = "FRC";

    private readonly FeeDbContext _db;
    private readonly INumberGenerator _numbers;
    private readonly IContactDirectory _contacts;
    private readonly IAccountDirectory _accounts;
    private readonly IFeeLedger _ledger;
    private readonly IBaseCurrencyProvider _currency;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ReceiptService> _log;

    public ReceiptService(
        FeeDbContext db,
        INumberGenerator numbers,
        IContactDirectory contacts,
        IAccountDirectory accounts,
        IFeeLedger ledger,
        IBaseCurrencyProvider currency,
        ITenantContext tenant,
        ILogger<ReceiptService> log)
    {
        _db = db;
        _numbers = numbers;
        _contacts = contacts;
        _accounts = accounts;
        _ledger = ledger;
        _currency = currency;
        _tenant = tenant;
        _log = log;
    }

    public async Task<List<FeeReceiptView>> ListAsync(long? contactId, CancellationToken ct)
    {
        List<FeeReceipt> rows = await _db.FeeReceipts.AsNoTracking().Include(r => r.Allocations)
            .Where(r => contactId == null || r.ContactId == contactId)
            .OrderByDescending(r => r.ReceiptDate).ThenByDescending(r => r.FeeReceiptId)
            .Take(2000)
            .ToListAsync(ct);
        return [.. rows.Select(View)];
    }

    public async Task<FeeResult> CreateAsync(SaveReceiptRequest request, CancellationToken ct)
    {
        string? currency;
        try
        {
            IReadOnlyDictionary<long, ContactSummary> contact = await _contacts.FindAsync([request.ContactId], ct);
            if (!contact.TryGetValue(request.ContactId, out ContactSummary? guardian) || !guardian.IsGuardian || !guardian.IsActive)
            {
                return FeeResult.Fail(FeeOutcome.Invalid, "Choose an active guardian of this branch.");
            }

            if ((await _accounts.BankAccountsAsync(ct)).All(b => b.BankAccountId != request.BankAccountId))
            {
                return FeeResult.Fail(FeeOutcome.Invalid, "Choose an active bank or cash account of this branch.");
            }

            currency = await _currency.GetBaseCurrencyAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogError(ex, "A fee receipt could not check its guardian or bank account.");
            return FeeResult.Fail(FeeOutcome.Unavailable);
        }

        if (currency is null)
        {
            return FeeResult.Fail(FeeOutcome.Unavailable);
        }

        var open = await _db.FeeDemands.AsNoTracking()
            .Where(d => d.ContactId == request.ContactId && d.DocumentStatus == FeeDocumentStatus.Posted && d.PaidAmount < d.NetAmount)
            .OrderBy(d => d.DueDate).ThenBy(d => d.FeeDemandId)
            .Select(d => new { d.FeeDemandId, Open = d.NetAmount - d.PaidAmount })
            .ToListAsync(ct);

        List<(long FeeDemandId, decimal Amount)> allocations;
        if (request.Allocations.Count == 0)
        {
            allocations = FeeRules.AutoAllocate(request.Amount, open.Select(o => (o.FeeDemandId, o.Open)));
        }
        else
        {
            allocations = [.. request.Allocations.Select(a => (a.FeeDemandId, a.Amount))];
            if (FeeRules.AllocationProblem(request.Amount, allocations, open.ToDictionary(o => o.FeeDemandId, o => o.Open)) is string problem)
            {
                return FeeResult.Fail(FeeOutcome.Invalid, problem);
            }
        }

        var receipt = new FeeReceipt
        {
            ReceiptNo = (await _numbers.NextAsync(FeeSeed.ReceiptSeriesCode, request.ReceiptDate, ct)).Code,
            ContactId = request.ContactId,
            ReceiptDate = request.ReceiptDate,
            PaymentMode = request.PaymentMode,
            BankAccountId = request.BankAccountId,
            Amount = request.Amount,
            UnallocatedAmount = request.Amount - allocations.Sum(a => a.Amount),
            Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim(),
            CurrencyCode = currency,
        };
        foreach ((long demandId, decimal amount) in allocations)
        {
            receipt.Allocations.Add(new FeeReceiptAllocation { FeeDemandId = demandId, Amount = amount });
        }

        _db.FeeReceipts.Add(receipt);
        await _db.SaveChangesAsync(ct);

        foreach ((long demandId, decimal amount) in allocations)
        {
            int settled = await _db.FeeDemands
                .Where(d => d.FeeDemandId == demandId && d.DocumentStatus == FeeDocumentStatus.Posted && d.PaidAmount + amount <= d.NetAmount)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.PaidAmount, d => d.PaidAmount + amount), ct);
            if (settled != 1)
            {
                // Another receipt settled it first. The transaction rolls this one back whole.
                return FeeResult.Fail(FeeOutcome.StateRule, "A demand was settled by another receipt meanwhile. Reload and try again.");
            }
        }

        FeeResult posted = await SendAsync(receipt, FeeRules.ReceiptLegs(receipt), [], ct);
        return posted.Outcome == FeeOutcome.Ok ? FeeResult.Ok(receipt.FeeReceiptId, View(receipt)) : posted;
    }

    /// <summary>Voids a receipt: its allocations come off the demands they settled, and its ledger rows are withdrawn.</summary>
    public async Task<FeeResult> VoidAsync(long id, string reason, CancellationToken ct)
    {
        FeeReceipt? receipt = await _db.FeeReceipts.Include(r => r.Allocations).FirstOrDefaultAsync(r => r.FeeReceiptId == id, ct);
        if (receipt is null)
        {
            return FeeResult.Fail(FeeOutcome.NotFound);
        }

        if (receipt.DocumentStatus == FeeDocumentStatus.Void)
        {
            return FeeResult.Fail(FeeOutcome.StateRule, "That receipt is already void.");
        }

        foreach (FeeReceiptAllocation allocation in receipt.Allocations)
        {
            decimal amount = allocation.Amount;
            int undone = await _db.FeeDemands
                .Where(d => d.FeeDemandId == allocation.FeeDemandId && d.PaidAmount >= amount)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.PaidAmount, d => d.PaidAmount - amount), ct);
            if (undone != 1)
            {
                return FeeResult.Fail(FeeOutcome.StateRule, "The receipt's allocations no longer match its demands. Reload and try again.");
            }
        }

        _db.FeeReceiptAllocations.RemoveRange(receipt.Allocations);
        receipt.DocumentStatus = FeeDocumentStatus.Void;
        receipt.VoidReason = reason.Trim();
        await _db.SaveChangesAsync(ct);

        FeeResult withdrawn = await SendAsync(receipt, [], [LedgerType.Control], ct);
        return withdrawn.Outcome == FeeOutcome.Ok ? FeeResult.Ok(id) : withdrawn;
    }

    private async Task<FeeResult> SendAsync(FeeReceipt receipt, List<LedgerLeg> legs, List<int> withdraw, CancellationToken ct)
    {
        (Guid customerId, Guid orgId) = _tenant.Require();
        LedgerOutcome outcome;
        try
        {
            outcome = await _ledger.PostAsync(new LedgerPosting
            {
                CustomerId = customerId,
                OrgId = orgId,
                TransactionTypeCode = TypeCode,
                TransactionId = receipt.FeeReceiptId,
                LedgerDate = receipt.ReceiptDate,
                CurrencyCode = receipt.CurrencyCode,
                ExchangeRate = 1m,
                ContactId = receipt.ContactId,
                DocumentNo = receipt.ReceiptNo,
                WithdrawLedgerTypeIds = withdraw,
                Legs = legs,
            }, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogError(ex, "Fee receipt {ReceiptId} could not reach the ledger.", receipt.FeeReceiptId);
            return FeeResult.Fail(FeeOutcome.Unavailable);
        }

        if (!outcome.Posted)
        {
            _log.LogError("The ledger refused fee receipt {ReceiptId}: {Detail}", receipt.FeeReceiptId, outcome.Detail);
            return FeeResult.Fail(FeeOutcome.LedgerRefused);
        }

        return FeeResult.Ok(receipt.FeeReceiptId);
    }

    private static FeeReceiptView View(FeeReceipt r) => new()
    {
        FeeReceiptId = r.FeeReceiptId,
        ReceiptNo = r.ReceiptNo,
        ContactId = r.ContactId,
        ReceiptDate = r.ReceiptDate,
        PaymentMode = r.PaymentMode,
        BankAccountId = r.BankAccountId,
        Amount = r.Amount,
        UnallocatedAmount = r.UnallocatedAmount,
        Reference = r.Reference,
        DocumentStatus = r.DocumentStatus,
        Allocations = [.. r.Allocations.Select(a => new AllocationModel { FeeDemandId = a.FeeDemandId, Amount = a.Amount })],
    };
}
