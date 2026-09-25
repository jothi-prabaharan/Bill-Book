using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sales.Entity.Enums;

namespace Sales.Api.Services.EInvoicing;

/// <summary>
/// An IRP that lives in memory, for development and tests (TK-91). It answers
/// the way the real portal does in the ways this product depends on:
/// <list type="bullet">
/// <item>the IRN is the SHA-256 of the supplier's GSTIN, the document type, the
/// number and the financial year, so the same document always gets the same IRN;</item>
/// <item>registering a document twice answers <see cref="IrpErrorCodes.DuplicateIrn"/>,
/// and "get IRN by document" then returns the first registration;</item>
/// <item>an IRN or an e-way bill can be cancelled only within
/// <see cref="EInvoiceRules.CancelWindow"/>, and only once.</item>
/// </list>
/// <b>Nothing it returns is signed by NIC.</b> Its QR payload and signed invoice
/// say SANDBOX in their header, and it is registered only where configuration
/// asks for it, never by default outside Development (see
/// <see cref="EInvoiceGatewayRegistration"/>).
/// </summary>
public sealed class SandboxEInvoiceGateway : IEInvoiceGateway
{
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<string, IrnDetails> _byIrn = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _cancelledIrns = new();
    private readonly ConcurrentDictionary<string, EwayBillDetails> _ewayBills = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _cancelledEwayBills = new();
    private long _ackSequence;

    public SandboxEInvoiceGateway(TimeProvider clock) => _clock = clock;

    public string Name => "Sandbox";

    public Task<IrpResult<IrpSession>> AuthenticateAsync(string gstin, CancellationToken ct) =>
        Task.FromResult(Shared.Kernel.Tax.Gstin.IsValid(gstin)
            ? IrpResult<IrpSession>.Success(new IrpSession(gstin, _clock.GetUtcNow().AddHours(6)))
            : IrpResult<IrpSession>.Refused("GSTIN", "The GSTIN is not valid."));

    public Task<IrpResult<IrnDetails>> GenerateIrnAsync(string gstin, Inv01Document document, CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(document.DocDtls.Dt, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
        {
            return Task.FromResult(IrpResult<IrnDetails>.Refused("DOC_DATE", "The document date is not dd/MM/yyyy."));
        }

        string irn = IrnFor(gstin, document.DocDtls.Typ, document.DocDtls.No, date);
        if (_byIrn.ContainsKey(irn))
        {
            return Task.FromResult(IrpResult<IrnDetails>.Refused(IrpErrorCodes.DuplicateIrn, "Duplicate IRN."));
        }

        DateTimeOffset now = _clock.GetUtcNow();
        string ackNo = (112_600_000_000_000L + Interlocked.Increment(ref _ackSequence)).ToString(CultureInfo.InvariantCulture);
        var details = new IrnDetails(
            irn,
            ackNo,
            now,
            SignedInvoice: Token(Inv01Json.Serialize(document)),
            SignedQrCode: Token(JsonSerializer.Serialize(new
            {
                SellerGstin = document.SellerDtls.Gstin,
                BuyerGstin = document.BuyerDtls.Gstin,
                DocNo = document.DocDtls.No,
                DocTyp = document.DocDtls.Typ,
                DocDt = document.DocDtls.Dt,
                TotInvVal = document.ValDtls.TotInvVal,
                ItemCnt = document.ItemList.Count,
                MainHsnCode = document.ItemList.OrderByDescending(i => i.AssAmt).Select(i => i.HsnCd).FirstOrDefault(),
                Irn = irn,
                IrnDt = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            })),
            EwayBill: document.EwbDtls is { } ewb ? NewEwayBill(now, ewb.Distance) : null);

        return Task.FromResult(_byIrn.TryAdd(irn, details)
            ? IrpResult<IrnDetails>.Success(details)
            : IrpResult<IrnDetails>.Refused(IrpErrorCodes.DuplicateIrn, "Duplicate IRN."));
    }

    public Task<IrpResult<IrnDetails>> GetIrnByDocumentAsync(
        string gstin, string documentType, string documentNo, DateOnly documentDate, CancellationToken ct) =>
        Task.FromResult(_byIrn.TryGetValue(IrnFor(gstin, documentType, documentNo, documentDate), out IrnDetails? found)
            ? IrpResult<IrnDetails>.Success(found)
            : IrpResult<IrnDetails>.Refused(IrpErrorCodes.NotFound, "No IRN for that document."));

    public Task<IrpResult<IrnCancellation>> CancelIrnAsync(
        string gstin, string irn, EInvoiceCancelReason reason, string remark, CancellationToken ct)
    {
        DateTimeOffset now = _clock.GetUtcNow();
        if (!_byIrn.TryGetValue(irn, out IrnDetails? found))
        {
            return Task.FromResult(IrpResult<IrnCancellation>.Refused(IrpErrorCodes.NotFound, "No such IRN."));
        }

        if (now - found.AckDate > EInvoiceRules.CancelWindow)
        {
            return Task.FromResult(IrpResult<IrnCancellation>.Refused(
                IrpErrorCodes.CancelWindowPassed, "The IRN can no longer be cancelled; 24 hours have passed."));
        }

        return Task.FromResult(_cancelledIrns.TryAdd(irn, now)
            ? IrpResult<IrnCancellation>.Success(new IrnCancellation(irn, now))
            : IrpResult<IrnCancellation>.Refused(IrpErrorCodes.AlreadyCancelled, "The IRN is already cancelled."));
    }

    public Task<IrpResult<EwayBillDetails>> GenerateEwayBillByIrnAsync(string gstin, EwayBillByIrnRequest request, CancellationToken ct)
    {
        if (!_byIrn.ContainsKey(request.Irn) || _cancelledIrns.ContainsKey(request.Irn))
        {
            return Task.FromResult(IrpResult<EwayBillDetails>.Refused(IrpErrorCodes.NotFound, "No live IRN for that request."));
        }

        return Task.FromResult(IrpResult<EwayBillDetails>.Success(NewEwayBill(_clock.GetUtcNow(), request.Transport.DistanceKm)));
    }

    public Task<IrpResult<EwayBillDetails>> GenerateEwayBillAsync(string gstin, StandaloneEwayBillRequest request, CancellationToken ct) =>
        Task.FromResult(request.Items.Count == 0
            ? IrpResult<EwayBillDetails>.Refused("ITEMS", "An e-way bill needs at least one item.")
            : IrpResult<EwayBillDetails>.Success(NewEwayBill(_clock.GetUtcNow(), request.Transport.DistanceKm)));

    public Task<IrpResult<EwayBillDetails>> UpdatePartBAsync(string gstin, PartBUpdate request, CancellationToken ct)
    {
        if (!_ewayBills.TryGetValue(request.EwbNo, out EwayBillDetails? found) || _cancelledEwayBills.ContainsKey(request.EwbNo))
        {
            return Task.FromResult(IrpResult<EwayBillDetails>.Refused(IrpErrorCodes.NotFound, "No live e-way bill with that number."));
        }

        return Task.FromResult(IrpResult<EwayBillDetails>.Success(found));
    }

    public Task<IrpResult<EwayBillCancellation>> CancelEwayBillAsync(
        string gstin, string ewbNo, EwayBillCancelReason reason, string remark, CancellationToken ct)
    {
        DateTimeOffset now = _clock.GetUtcNow();
        if (!_ewayBills.TryGetValue(ewbNo, out EwayBillDetails? found))
        {
            return Task.FromResult(IrpResult<EwayBillCancellation>.Refused(IrpErrorCodes.NotFound, "No such e-way bill."));
        }

        if (now - found.EwbDate > EInvoiceRules.CancelWindow)
        {
            return Task.FromResult(IrpResult<EwayBillCancellation>.Refused(
                IrpErrorCodes.CancelWindowPassed, "The e-way bill can no longer be cancelled; 24 hours have passed."));
        }

        return Task.FromResult(_cancelledEwayBills.TryAdd(ewbNo, now)
            ? IrpResult<EwayBillCancellation>.Success(new EwayBillCancellation(ewbNo, now))
            : IrpResult<EwayBillCancellation>.Refused(IrpErrorCodes.AlreadyCancelled, "The e-way bill is already cancelled."));
    }

    /// <summary>
    /// The IRN as the notification defines it: the SHA-256, in lower-case hex,
    /// of the GSTIN, the document type, the number and the financial year
    /// (April to March, written <c>2026-27</c>).
    /// </summary>
    public static string IrnFor(string gstin, string documentType, string documentNo, DateOnly documentDate)
    {
        int start = documentDate.Month >= 4 ? documentDate.Year : documentDate.Year - 1;
        string year = $"{start}-{(start + 1) % 100:00}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{gstin.ToUpperInvariant()}{documentType}{documentNo.ToUpperInvariant()}{year}"));
        return Convert.ToHexStringLower(hash);
    }

    private EwayBillDetails NewEwayBill(DateTimeOffset now, int distanceKm)
    {
        int days = Math.Max(1, (int)Math.Ceiling(Math.Max(distanceKm, 1) / (double)EInvoiceRules.RoadKmPerDay));
        string ewbNo;
        EwayBillDetails details;
        do
        {
            ewbNo = (100_000_000_000L + Random.Shared.NextInt64(0, 900_000_000_000L)).ToString(CultureInfo.InvariantCulture);
            details = new EwayBillDetails(ewbNo, now, now.AddDays(days));
        }
        while (!_ewayBills.TryAdd(ewbNo, details));

        return details;
    }

    /// <summary>A token shaped like the IRP's signed JWTs, but plainly marked as the sandbox's.</summary>
    private static string Token(string payload)
    {
        static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string header = B64("""{"alg":"none","typ":"JWT","iss":"SANDBOX"}""");
        return $"{header}.{B64(payload)}.SANDBOX";
    }
}

/// <summary>
/// The gateway used where none is configured (TK-91). It refuses every call
/// with <see cref="IrpErrorCodes.NotConfigured"/>, so a branch that switches on
/// e-invoicing before the provider is chosen (D-24) sees its documents fail with
/// a reason, and is never handed an IRN that no portal issued.
/// </summary>
public sealed class UnconfiguredEInvoiceGateway : IEInvoiceGateway
{
    private const string Message = "No e-invoicing provider is configured for this installation yet.";

    public string Name => "None";

    public Task<IrpResult<IrpSession>> AuthenticateAsync(string gstin, CancellationToken ct) => Refuse<IrpSession>();

    public Task<IrpResult<IrnDetails>> GenerateIrnAsync(string gstin, Inv01Document document, CancellationToken ct) => Refuse<IrnDetails>();

    public Task<IrpResult<IrnDetails>> GetIrnByDocumentAsync(string gstin, string documentType, string documentNo, DateOnly documentDate, CancellationToken ct) => Refuse<IrnDetails>();

    public Task<IrpResult<IrnCancellation>> CancelIrnAsync(string gstin, string irn, EInvoiceCancelReason reason, string remark, CancellationToken ct) => Refuse<IrnCancellation>();

    public Task<IrpResult<EwayBillDetails>> GenerateEwayBillByIrnAsync(string gstin, EwayBillByIrnRequest request, CancellationToken ct) => Refuse<EwayBillDetails>();

    public Task<IrpResult<EwayBillDetails>> GenerateEwayBillAsync(string gstin, StandaloneEwayBillRequest request, CancellationToken ct) => Refuse<EwayBillDetails>();

    public Task<IrpResult<EwayBillDetails>> UpdatePartBAsync(string gstin, PartBUpdate request, CancellationToken ct) => Refuse<EwayBillDetails>();

    public Task<IrpResult<EwayBillCancellation>> CancelEwayBillAsync(string gstin, string ewbNo, EwayBillCancelReason reason, string remark, CancellationToken ct) => Refuse<EwayBillCancellation>();

    private static Task<IrpResult<T>> Refuse<T>() where T : class =>
        Task.FromResult(IrpResult<T>.Refused(IrpErrorCodes.NotConfigured, Message));
}
