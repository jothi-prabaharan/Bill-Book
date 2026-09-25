using Sales.Entity.Enums;

namespace Sales.Api.Services.EInvoicing;

/// <summary>
/// What the IRP and the e-way bill portal answered (TK-91). Either
/// <see cref="Value"/> or an error: the IRP's own code and text, and whether
/// trying again later could help.
/// </summary>
public sealed record IrpResult<T>(T? Value, string? ErrorCode = null, string? ErrorMessage = null, bool Transient = false)
    where T : class
{
    public bool Ok => Value is not null;

    public static IrpResult<T> Success(T value) => new(value);

    /// <summary>A refusal a person has to fix: retrying the same request gets the same answer.</summary>
    public static IrpResult<T> Refused(string code, string message) => new(null, code, message);

    /// <summary>The portal could not be reached or was busy; the worker tries again.</summary>
    public static IrpResult<T> Unavailable(string message) => new(null, IrpErrorCodes.Unavailable, message, Transient: true);
}

/// <summary>A signed-in session at the IRP for one GSTIN.</summary>
public sealed record IrpSession(string Gstin, DateTimeOffset ExpiresAt);

/// <summary>A registered e-invoice, as the IRP returned it.</summary>
public sealed record IrnDetails(
    string Irn,
    string AckNo,
    DateTimeOffset AckDate,
    string SignedInvoice,
    string SignedQrCode,

    /// <summary>Set when an e-way bill was asked for in the same call (TK-93).</summary>
    EwayBillDetails? EwayBill = null);

public sealed record IrnCancellation(string Irn, DateTimeOffset CancelledAt);

public sealed record EwayBillDetails(string EwbNo, DateTimeOffset EwbDate, DateTimeOffset? ValidUntil);

public sealed record EwayBillCancellation(string EwbNo, DateTimeOffset CancelledAt);

/// <summary>Part B: how the goods travel.</summary>
public sealed record EwayTransport(
    TransportMode Mode,
    int DistanceKm,
    string? VehicleNo,
    string? TransporterId,
    string? TransporterName,
    string? TransportDocumentNo = null,
    DateOnly? TransportDocumentDate = null);

/// <summary>An e-way bill asked for against an IRN already registered.</summary>
public sealed record EwayBillByIrnRequest(string Irn, EwayTransport Transport);

/// <summary>One line of a standalone e-way bill.</summary>
public sealed record EwayItem(
    string HsnCode,
    string? Description,
    decimal Quantity,
    string? Unit,
    decimal TaxableAmount,
    decimal CgstRate,
    decimal SgstRate,
    decimal IgstRate,
    decimal CessRate);

/// <summary>
/// An e-way bill for a document with no IRN: a delivery challan, or an invoice
/// registered without transport details (TK-93).
/// </summary>
public sealed record StandaloneEwayBillRequest(
    /// <summary><c>INV</c>, <c>CHL</c> (challan) or <c>CRN</c>.</summary>
    string DocumentType,
    string DocumentNo,
    DateOnly DocumentDate,

    /// <summary>The portal's sub-supply type: 1 supply, 4 job work, 5 for own use, 8 others, and so on.</summary>
    int SubSupplyType,
    Inv01Party From,
    Inv01Party To,
    IReadOnlyList<EwayItem> Items,
    decimal TotalValue,
    EwayTransport Transport);

/// <summary>A vehicle change on a live e-way bill.</summary>
public sealed record PartBUpdate(string EwbNo, EwayTransport Transport, string FromPlace, string FromStateCode, string Reason);

/// <summary>
/// The IRP and the e-way bill portal, behind one interface (TK-91). Which
/// provider fills it — a GST Suvidha Provider or NIC directly — is the owner's
/// choice, D-24; nothing else in the design depends on it. The credentials are
/// the branch's, read through <see cref="EInvoiceCredentials"/>, keyed by GSTIN.
/// </summary>
public interface IEInvoiceGateway
{
    /// <summary>A name for the operator: which gateway answered.</summary>
    string Name { get; }

    Task<IrpResult<IrpSession>> AuthenticateAsync(string gstin, CancellationToken ct);

    Task<IrpResult<IrnDetails>> GenerateIrnAsync(string gstin, Inv01Document document, CancellationToken ct);

    /// <summary>
    /// The IRN already registered for a document. What a retry uses after a
    /// "duplicate IRN" answer, when the first request registered and its answer
    /// was lost (design, decision 6).
    /// </summary>
    Task<IrpResult<IrnDetails>> GetIrnByDocumentAsync(
        string gstin, string documentType, string documentNo, DateOnly documentDate, CancellationToken ct);

    Task<IrpResult<IrnCancellation>> CancelIrnAsync(
        string gstin, string irn, EInvoiceCancelReason reason, string remark, CancellationToken ct);

    Task<IrpResult<EwayBillDetails>> GenerateEwayBillByIrnAsync(string gstin, EwayBillByIrnRequest request, CancellationToken ct);

    Task<IrpResult<EwayBillDetails>> GenerateEwayBillAsync(string gstin, StandaloneEwayBillRequest request, CancellationToken ct);

    Task<IrpResult<EwayBillDetails>> UpdatePartBAsync(string gstin, PartBUpdate request, CancellationToken ct);

    Task<IrpResult<EwayBillCancellation>> CancelEwayBillAsync(
        string gstin, string ewbNo, EwayBillCancelReason reason, string remark, CancellationToken ct);
}

/// <summary>
/// The error codes this product acts on. <see cref="DuplicateIrn"/> is NIC's
/// own; the others are this product's names for conditions every provider
/// reports in its own words, mapped by each gateway.
/// </summary>
public static class IrpErrorCodes
{
    /// <summary>NIC: an IRN already exists for this document.</summary>
    public const string DuplicateIrn = "2150";

    public const string CancelWindowPassed = "CANCEL_WINDOW";

    public const string AlreadyCancelled = "ALREADY_CANCELLED";

    public const string NotFound = "NOT_FOUND";

    /// <summary>No gateway is configured for this environment.</summary>
    public const string NotConfigured = "NOT_CONFIGURED";

    /// <summary>The portal could not be reached, timed out or was busy.</summary>
    public const string Unavailable = "UNAVAILABLE";
}

/// <summary>The legal limits the design names, each with one home (TK-91).</summary>
public static class EInvoiceRules
{
    /// <summary>An IRN, or an e-way bill, can be cancelled within this long of its acknowledgement.</summary>
    public static readonly TimeSpan CancelWindow = TimeSpan.FromHours(24);

    /// <summary>
    /// A consignment above this value needs an e-way bill (₹50,000 at the time of
    /// writing). Check it against the current notification when TK-93 uses it.
    /// </summary>
    public const decimal EwayBillThreshold = 50_000m;

    /// <summary>A road e-way bill is valid one day for each full or part 200 km.</summary>
    public const int RoadKmPerDay = 200;
}

/// <summary>
/// Where a branch's IRP credentials live in <c>ISecretStore</c>: under names
/// built from its GSTIN, never in <c>appsettings</c> and never returned by any
/// API. Letters, digits and dashes only, so a Key Vault takes them as they are.
/// </summary>
public static class EInvoiceCredentials
{
    public static string UsernameKey(string gstin) => $"einvoice-{gstin.ToUpperInvariant()}-username";

    public static string PasswordKey(string gstin) => $"einvoice-{gstin.ToUpperInvariant()}-password";

    /// <summary>The provider's API client id, when it issues one per taxpayer.</summary>
    public static string ClientIdKey(string gstin) => $"einvoice-{gstin.ToUpperInvariant()}-clientid";

    public static string ClientSecretKey(string gstin) => $"einvoice-{gstin.ToUpperInvariant()}-clientsecret";
}
