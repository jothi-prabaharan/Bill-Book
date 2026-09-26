using System.Text.Json;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Services.Payments;

/// <summary>One invoice the payer chose, and how much of it (TK-98).</summary>
public sealed class PaymentAllocationRequest
{
    public long InvoiceId { get; set; }

    public decimal Amount { get; set; }
}

public sealed class StartPaymentRequest
{
    /// <summary>The invoices being paid. May be empty when paying an amount on account.</summary>
    public List<PaymentAllocationRequest> Invoices { get; set; } = [];

    /// <summary>The whole payment. Defaults to the sum of the invoices; anything beyond them is an advance.</summary>
    public decimal? Amount { get; set; }
}

public enum StartPaymentOutcome
{
    Ok = 0,
    Invalid = 1,
    NotSetUp = 2,
    GatewayUnavailable = 3,
}

public sealed record StartPaymentResult(StartPaymentOutcome Outcome, long OnlinePaymentId = 0, string? CheckoutUrl = null, string? Detail = null);

public enum CompletePaymentOutcome
{
    /// <summary>Recorded now: paid with its receipt, or failed.</summary>
    Ok = 0,

    /// <summary>Already recorded by an earlier callback. Nothing was done, which is the right answer to a replay.</summary>
    AlreadyRecorded = 1,

    NotFound = 2,

    /// <summary>The callback's order or amount is not this payment's.</summary>
    Mismatch = 3,
}

/// <summary>An online payment as the portal shows it: the result comes from here, never from the redirect.</summary>
public sealed class OnlinePaymentView
{
    public long OnlinePaymentId { get; set; }

    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>Created, Paid, Failed or Refunded.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>The receipt's number, once it has one.</summary>
    public string? ReceiptNo { get; set; }

    public string? Note { get; set; }
}

/// <summary>
/// Online payment from the client portal (TK-98, design "Client portal" →
/// Online payment).
///
/// <b>Starting</b> checks what the payer chose against the ledger — their own
/// invoices, in base currency, no more than is owed — records the payment and
/// opens an order at the gateway. <b>Completing</b> happens only on the
/// gateway's verified callback: in one transaction, the payment is claimed out
/// of <c>Created</c> by a guarded update and an ordinary Receive Money is made,
/// posted and allocated. A replayed callback finds nothing to claim and does
/// nothing. If the allocation is refused — an invoice settled by staff in the
/// meantime, say — the money goes in as the contact's advance instead; if even
/// that is refused, the payment is marked paid with a note and no receipt, for
/// staff to record, because the money has been taken either way.
/// </summary>
public sealed class OnlinePaymentService
{
    /// <summary><c>mst.LedgerSources</c> 3 — an invoice payment.</summary>
    private const int InvoicePayment = 3;

    /// <summary><c>mst.LedgerSources</c> 9 — an advance received from a customer.</summary>
    private const int CustomerPrepayment = 9;

    private const int ControlLedgerType = 3;

    private static readonly string[] InvoiceCodes = ["INV", "POS"];

    private static readonly TimeSpan Ist = TimeSpan.FromHours(5.5);

    private readonly AccountingDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPaymentGateway _gateway;
    private readonly IBaseCurrencyProvider _baseCurrency;
    private readonly ReceiveMoneyService _receipts;
    private readonly TimeProvider _clock;

    public OnlinePaymentService(
        AccountingDbContext db,
        ITenantContext tenant,
        IPaymentGateway gateway,
        IBaseCurrencyProvider baseCurrency,
        ReceiveMoneyService receipts,
        TimeProvider clock)
    {
        _db = db;
        _tenant = tenant;
        _gateway = gateway;
        _baseCurrency = baseCurrency;
        _receipts = receipts;
        _clock = clock;
    }

    public async Task<StartPaymentResult> StartAsync(long contactId, StartPaymentRequest request, CancellationToken ct)
    {
        (Guid customerId, Guid orgId) = _tenant.Require();

        if (!_gateway.IsConfigured)
        {
            return new StartPaymentResult(StartPaymentOutcome.GatewayUnavailable, Detail: "Online payment is not available yet.");
        }

        long? bankAccountId = await _db.BankAccounts
            .Where(b => b.IsOnlinePaymentAccount && b.IsActive && b.LedgerAccountId != null)
            .Select(b => (long?)b.BankAccountId)
            .FirstOrDefaultAsync(ct);

        if (bankAccountId is null)
        {
            return new StartPaymentResult(StartPaymentOutcome.NotSetUp, Detail: "Online payment is not set up for this business yet.");
        }

        if (await _baseCurrency.GetBaseCurrencyAsync(ct) is not { } currency)
        {
            return new StartPaymentResult(StartPaymentOutcome.GatewayUnavailable, Detail: "Online payment is not available right now. Try again in a moment.");
        }

        List<PaymentAllocationRequest> chosen = [.. request.Invoices.Where(i => i.Amount != 0m)];
        if (chosen.Any(i => i.Amount < 0m) || chosen.GroupBy(i => i.InvoiceId).Any(g => g.Count() > 1))
        {
            return Invalid("Each invoice can be paid once, by an amount above zero.");
        }

        List<AllocationLine> allocations = [];
        foreach (PaymentAllocationRequest line in chosen)
        {
            var legs = await _db.JournalLedger
                .AsNoTracking()
                .Where(l => InvoiceCodes.Contains(l.TransactionTypeCode)
                    && l.TransactionId == line.InvoiceId
                    && l.LedgerTypeId == ControlLedgerType)
                .Select(l => new { l.TransactionTypeCode, l.ContactId, l.CurrencyCode, l.DebitAmountBase, l.CreditAmountBase })
                .ToListAsync(ct);

            // Another contact's invoice reads exactly as one that does not exist.
            if (legs.Count == 0 || legs.Any(l => l.ContactId != contactId))
            {
                return Invalid("One of those invoices could not be found.");
            }

            if (legs.Any(l => l.DebitAmountBase > 0m && l.CurrencyCode != currency))
            {
                return Invalid($"Only invoices in {currency} can be paid online.");
            }

            decimal owed = legs.Sum(l => l.DebitAmountBase - l.CreditAmountBase);
            if (line.Amount > owed + 0.005m)
            {
                return Invalid($"That is more than is owed on one of the invoices ({owed:0.00}).");
            }

            allocations.Add(new AllocationLine(legs[0].TransactionTypeCode, line.InvoiceId, Math.Round(line.Amount, 2)));
        }

        decimal allocated = allocations.Sum(a => a.Amount);
        decimal amount = Math.Round(request.Amount ?? allocated, 2);

        if (amount <= 0m || amount < allocated)
        {
            return Invalid("The payment must be above zero and cover the invoices chosen.");
        }

        var payment = new OnlinePayment
        {
            Reference = $"op_pending_{Guid.NewGuid():N}",
            ContactId = contactId,
            Amount = amount,
            CurrencyCode = currency,
            Allocations = JsonSerializer.Serialize(allocations),
            BankAccountId = bankAccountId.Value,
            Gateway = _gateway.Kind,
        };

        _db.OnlinePayments.Add(payment);
        await _db.SaveChangesAsync(ct);

        payment.Reference = Reference(customerId, orgId, payment.OnlinePaymentId);
        GatewayOrder order = await _gateway.CreateOrderAsync(payment.Reference, amount, currency, ct);
        payment.GatewayOrderId = order.OrderId;
        await _db.SaveChangesAsync(ct);

        return new StartPaymentResult(
            StartPaymentOutcome.Ok, payment.OnlinePaymentId, order.CheckoutUrl ?? $"/pay/sandbox/{payment.OnlinePaymentId}");
    }

    /// <summary>The contact's payment and what became of it, or null when it is not theirs.</summary>
    public async Task<OnlinePaymentView?> GetAsync(long contactId, long onlinePaymentId, CancellationToken ct) =>
        await _db.OnlinePayments
            .AsNoTracking()
            .Where(p => p.OnlinePaymentId == onlinePaymentId && p.ContactId == contactId)
            .Select(p => new OnlinePaymentView
            {
                OnlinePaymentId = p.OnlinePaymentId,
                Amount = p.Amount,
                CurrencyCode = p.CurrencyCode,
                Status = p.Status.ToString(),
                ReceiptNo = _db.ReceiveMoney.Where(r => r.ReceiveMoneyId == p.ReceiveMoneyId).Select(r => r.TransactionNo).FirstOrDefault(),
                Note = p.Note,
            })
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Records what a verified callback said. The tenant must already be the
    /// reference's (<see cref="TryReadReference"/>).
    /// </summary>
    public async Task<CompletePaymentOutcome> CompleteAsync(GatewayCallback callback, CancellationToken ct)
    {
        OnlinePayment? payment = await _db.OnlinePayments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Reference == callback.Reference, ct);

        if (payment is null)
        {
            return CompletePaymentOutcome.NotFound;
        }

        if (payment.GatewayOrderId != callback.OrderId || payment.Amount != callback.Amount)
        {
            return CompletePaymentOutcome.Mismatch;
        }

        if (payment.Status != OnlinePaymentStatus.Created)
        {
            return CompletePaymentOutcome.AlreadyRecorded;
        }

        if (!callback.Succeeded)
        {
            int failed = await _db.OnlinePayments
                .Where(p => p.OnlinePaymentId == payment.OnlinePaymentId && p.Status == OnlinePaymentStatus.Created)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, OnlinePaymentStatus.Failed), ct);
            return failed == 1 ? CompletePaymentOutcome.Ok : CompletePaymentOutcome.AlreadyRecorded;
        }

        List<AllocationLine> allocations = JsonSerializer.Deserialize<List<AllocationLine>>(payment.Allocations) ?? [];

        // As chosen, then as an advance, then with no receipt at all: each
        // attempt claims the payment inside its own transaction, so a refused
        // one leaves it unclaimed for the next and a replay finds it claimed.
        Attempt first = await RecordAsync(payment, callback, allocations, note: null, ct);
        if (first is not Attempt.Refused)
        {
            return first == Attempt.Recorded ? CompletePaymentOutcome.Ok : CompletePaymentOutcome.AlreadyRecorded;
        }

        Attempt second = allocations.Count == 0
            ? Attempt.Refused
            : await RecordAsync(payment, callback, [],
                note: "Received as an advance: the invoices chosen could not be settled as they stood.", ct);
        if (second is not Attempt.Refused)
        {
            return second == Attempt.Recorded ? CompletePaymentOutcome.Ok : CompletePaymentOutcome.AlreadyRecorded;
        }

        return await ClaimWithoutReceiptAsync(payment, callback, ct)
            ? CompletePaymentOutcome.Ok
            : CompletePaymentOutcome.AlreadyRecorded;
    }

    /// <summary>
    /// The sandbox's own checkout (TK-98, D-25): signs the callback the sandbox
    /// would send and records it through the same path a real callback takes.
    /// Only the contact's own open payment, and only with the sandbox gateway.
    /// </summary>
    public async Task<CompletePaymentOutcome> SandboxCheckoutAsync(
        long contactId, long onlinePaymentId, bool succeed, SandboxPaymentGateway sandbox, CancellationToken ct)
    {
        OnlinePayment? payment = await _db.OnlinePayments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.OnlinePaymentId == onlinePaymentId && p.ContactId == contactId, ct);

        if (payment is null || payment.GatewayOrderId is null)
        {
            return CompletePaymentOutcome.NotFound;
        }

        string body = sandbox.Callback(
            payment.Reference,
            payment.GatewayOrderId,
            $"sbx_pay_{Guid.NewGuid():N}"[..24],
            succeed,
            payment.Amount);

        return sandbox.ReadCallback(body) is { } callback
            ? await CompleteAsync(callback, ct)
            : CompletePaymentOutcome.Mismatch;
    }

    public static string Reference(Guid customerId, Guid orgId, long onlinePaymentId) =>
        $"op_{customerId:N}_{orgId:N}_{onlinePaymentId}";

    /// <summary>The customer and branch a payment reference names.</summary>
    public static bool TryReadReference(string? reference, out Guid customerId, out Guid orgId)
    {
        customerId = Guid.Empty;
        orgId = Guid.Empty;
        string[] parts = (reference ?? string.Empty).Split('_');

        return parts.Length == 4
            && parts[0] == "op"
            && Guid.TryParseExact(parts[1], "N", out customerId)
            && Guid.TryParseExact(parts[2], "N", out orgId)
            && long.TryParse(parts[3], out _);
    }

    private enum Attempt
    {
        Recorded,
        AlreadyClaimed,
        Refused,
    }

    private async Task<Attempt> RecordAsync(
        OnlinePayment payment, GatewayCallback callback, List<AllocationLine> allocations, string? note, CancellationToken ct)
    {
        _db.ChangeTracker.Clear();
        await using ITransactionScope scope = await _db.Database.BeginScopeAsync(ct);

        if (!await ClaimAsync(payment, callback, note, ct))
        {
            return Attempt.AlreadyClaimed;
        }

        decimal allocated = allocations.Sum(a => a.Amount);
        List<SaveMoneyLineRequest> lines = [.. allocations.Select(a => new SaveMoneyLineRequest
        {
            LedgerSourceId = InvoicePayment,
            MappingTransactionTypeCode = a.Code,
            MappingTransactionId = a.InvoiceId,
            Amount = a.Amount,
            LineMemo = "Paid online",
        })];

        if (payment.Amount > allocated)
        {
            lines.Add(new SaveMoneyLineRequest
            {
                LedgerSourceId = CustomerPrepayment,
                Amount = payment.Amount - allocated,
                LineMemo = "Paid online, on account",
            });
        }

        MoneyDocumentResult created = await _receipts.CreateAsync(new SaveMoneyDocumentRequest
        {
            TransactionDate = DateOnly.FromDateTime(_clock.GetUtcNow().ToOffset(Ist).DateTime),
            BankAccountId = payment.BankAccountId,
            ContactId = payment.ContactId,
            Amount = payment.Amount,
            CurrencyCode = payment.CurrencyCode,
            PaymentMethod = PaymentMethod.Other,
            ReferenceNo = callback.PaymentId.Length <= 50 ? callback.PaymentId : callback.PaymentId[..50],
            Memo = $"Paid online ({payment.Gateway})",
            Lines = lines,
        }, ct);

        if (created.Outcome != MoneyDocumentOutcome.Ok)
        {
            return Attempt.Refused;
        }

        MoneyDocumentResult posted = await _receipts.PostAsync(created.DocumentId, ct);
        if (posted.Outcome != MoneyDocumentOutcome.Ok)
        {
            return Attempt.Refused;
        }

        await _db.OnlinePayments
            .Where(p => p.OnlinePaymentId == payment.OnlinePaymentId)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.ReceiveMoneyId, created.DocumentId), ct);

        await scope.CommitAsync(ct);
        return Attempt.Recorded;
    }

    private async Task<bool> ClaimWithoutReceiptAsync(OnlinePayment payment, GatewayCallback callback, CancellationToken ct)
    {
        _db.ChangeTracker.Clear();
        await using ITransactionScope scope = await _db.Database.BeginScopeAsync(ct);

        bool claimed = await ClaimAsync(payment, callback,
            "Paid, but no receipt could be made. Record it by hand against this payment.", ct);
        await scope.CommitAsync(ct);
        return claimed;
    }

    /// <summary>The guarded move out of Created. Its row count is the answer, so two callbacks cannot both claim.</summary>
    private async Task<bool> ClaimAsync(OnlinePayment payment, GatewayCallback callback, string? note, CancellationToken ct)
    {
        DateTimeOffset now = _clock.GetUtcNow();

        return await _db.OnlinePayments
            .Where(p => p.OnlinePaymentId == payment.OnlinePaymentId && p.Status == OnlinePaymentStatus.Created)
            .ExecuteUpdateAsync(p => p
                .SetProperty(x => x.Status, OnlinePaymentStatus.Paid)
                .SetProperty(x => x.GatewayPaymentId, callback.PaymentId)
                .SetProperty(x => x.PaidAt, now)
                .SetProperty(x => x.Note, note),
                ct) == 1;
    }

    private static StartPaymentResult Invalid(string detail) => new(StartPaymentOutcome.Invalid, Detail: detail);

    /// <summary>One invoice the payment settles, as stored in <see cref="OnlinePayment.Allocations"/>.</summary>
    public sealed record AllocationLine(string Code, long InvoiceId, decimal Amount);
}
