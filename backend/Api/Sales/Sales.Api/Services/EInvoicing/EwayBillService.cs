using Microsoft.EntityFrameworkCore;
using Sales.Api.Services.Pdf;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Contacts;
using Shared.Kernel.Documents;
using Shared.Kernel.Persistence;
using Shared.Kernel.Stock;
using Shared.Kernel.Storage;
using Shared.Kernel.Tenancy;

namespace Sales.Api.Services.EInvoicing;

/// <summary>
/// E-way bills for goods leaving on a posted invoice or delivery challan (TK-93).
///
/// <list type="bullet">
/// <item><b>Generate</b> is asked by a person, with Part B. For an invoice that
/// already has its IRN it goes by IRN; otherwise it is standalone. The row is
/// written Pending in the request's transaction and sent once it commits, the
/// same shape as registering an IRN, so a lost answer never loses the record
/// that one was asked for.</item>
/// <item><b>Only over the limit.</b> A consignment at or under
/// <see cref="EInvoiceRules.EwayBillThreshold"/> needs none, and asking for one
/// is refused rather than sent.</item>
/// <item><b>Part B update and cancel</b> act on a live bill; cancel only within
/// 24 hours of generation.</item>
/// <item><b>A number typed on a challan</b> is a bill made outside the product,
/// recorded as a <see cref="EwayBillOrigin.Manual"/> row (<see cref="SyncManualAsync"/>).</item>
/// </list>
/// </summary>
public sealed class EwayBillService
{
    private static readonly TimeSpan Ist = TimeSpan.FromHours(5.5);

    private readonly SalesDbContext _db;
    private readonly IBranchSettingsProvider _settings;
    private readonly IOrgIdentityProvider _identity;
    private readonly IContactAddressBook _addresses;
    private readonly IItemNameLookup _items;
    private readonly IUqcLookup _uqc;
    private readonly IEInvoiceGateway _gateway;
    private readonly IAfterCommit _afterCommit;
    private readonly SalesDocumentArchive _archive;
    private readonly TimeProvider _clock;
    private readonly ILogger<EwayBillService> _log;

    public EwayBillService(
        SalesDbContext db,
        IBranchSettingsProvider settings,
        IOrgIdentityProvider identity,
        IContactAddressBook addresses,
        IItemNameLookup items,
        IUqcLookup uqc,
        IEInvoiceGateway gateway,
        IAfterCommit afterCommit,
        SalesDocumentArchive archive,
        TimeProvider clock,
        ILogger<EwayBillService> log)
    {
        _db = db;
        _settings = settings;
        _identity = identity;
        _addresses = addresses;
        _items = items;
        _uqc = uqc;
        _gateway = gateway;
        _afterCommit = afterCommit;
        _archive = archive;
        _clock = clock;
        _log = log;
    }

    /// <summary>The document's latest e-way bill, or null when it has none.</summary>
    public async Task<EwayBillView?> GetAsync(EwayBillSource source, long sourceId, CancellationToken ct)
    {
        EwayBill? row = await _db.EwayBills.AsNoTracking()
            .Where(e => e.SourceType == source && e.SourceId == sourceId)
            .OrderByDescending(e => e.EwayBillId)
            .FirstOrDefaultAsync(ct);
        return row is null ? null : View(null, row);
    }

    public async Task<EwayBillResult> GenerateAsync(
        EwayBillSource source, long sourceId, GenerateEwayBillRequest request, CancellationToken ct)
    {
        DocumentHeaderBase? document = await HeaderAsync(source, sourceId, ct);
        if (document is null)
        {
            return new EwayBillResult(EwayBillOutcome.NotFound);
        }

        if ((await _settings.GetSettingsAsync(ct)) is not { EwayBillEnabled: true })
        {
            return new EwayBillResult(EwayBillOutcome.Refused,
                "This branch does not generate e-way bills. Switch them on in Settings › Organization › Statutory.");
        }

        if (document.Status != DocumentStatus.Posted)
        {
            return new EwayBillResult(EwayBillOutcome.Refused, "Only a posted document moves goods, so post it first.");
        }

        if (document.TotalAmount <= EInvoiceRules.EwayBillThreshold)
        {
            return new EwayBillResult(EwayBillOutcome.Refused,
                $"The consignment is worth {document.TotalAmount:#,##0.00}, which is not over the "
                    + $"{EInvoiceRules.EwayBillThreshold:#,##0} limit, so no e-way bill is needed.");
        }

        if (await LiveAsync(source, sourceId, ct) is EwayBill live)
        {
            return new EwayBillResult(EwayBillOutcome.Refused,
                $"This document already has e-way bill {live.EwbNo ?? "(being generated)"}. Cancel it before generating another.",
                View(null, live));
        }

        EwayTransport transport = Transport(request.TransportMode, request.DistanceKm, request.VehicleNo, request.TransporterId, request.TransporterName);
        IReadOnlyList<EInvoiceProblem> problems = EwayBillMapper.ValidateTransport(transport);
        if (problems.Count > 0)
        {
            return new EwayBillResult(EwayBillOutcome.Invalid, string.Join(" ", problems.Select(p => p.Message)));
        }

        bool byIrn = source == EwayBillSource.Invoice && await _db.EInvoices.AnyAsync(
            e => e.SourceType == EInvoiceSource.Invoice && e.SourceId == sourceId && e.Status == EInvoiceStatus.Registered, ct);

        var row = new EwayBill
        {
            SourceType = source,
            SourceId = sourceId,
            Origin = byIrn ? EwayBillOrigin.ByIrn : EwayBillOrigin.Standalone,
            Status = EwayBillStatus.Pending,
        };
        EwayBillMapper.Apply(row, transport);
        _db.EwayBills.Add(row);
        await _db.SaveChangesAsync(ct);

        EwayBillView view = View(null, row);
        long id = row.EwayBillId;
        _afterCommit.Enqueue(token => SendAsync(id, view, token));
        return new EwayBillResult(EwayBillOutcome.Ok, EwayBill: view);
    }

    /// <summary>
    /// Sends one Pending row to the portal. Runs after the generate request has
    /// committed; public so a test can play the commit.
    /// </summary>
    public async Task<EwayBillView?> SendAsync(long ewayBillId, EwayBillView? view, CancellationToken ct)
    {
        EwayBill? row = await LoadAsync(ewayBillId, ct);
        if (row is null || row.Status != EwayBillStatus.Pending)
        {
            return row is null ? null : View(view, row);
        }

        row.Attempts++;
        EwayTransport transport = EwayBillMapper.TransportOf(row);
        OrgIdentity? seller = await _identity.GetIdentityAsync(ct);

        IrpResult<EwayBillDetails> result;
        if (seller?.Gstin is not { Length: > 0 } gstin)
        {
            result = IrpResult<EwayBillDetails>.Refused("SELLER", "The branch's details could not be read. Try again in a moment.");
        }
        else if (row.Origin == EwayBillOrigin.ByIrn)
        {
            string? irn = await _db.EInvoices
                .Where(e => e.SourceType == EInvoiceSource.Invoice && e.SourceId == row.SourceId && e.Status == EInvoiceStatus.Registered)
                .Select(e => e.Irn)
                .FirstOrDefaultAsync(ct);
            result = irn is null
                ? IrpResult<EwayBillDetails>.Refused(IrpErrorCodes.NotFound, "The invoice's IRN is no longer live.")
                : await CallAsync(() => _gateway.GenerateEwayBillByIrnAsync(gstin, new EwayBillByIrnRequest(irn, transport), ct));
        }
        else
        {
            (StandaloneEwayBillRequest? request, string? problem) = await StandaloneAsync(row, seller, transport, ct);
            result = request is null
                ? IrpResult<EwayBillDetails>.Refused("VALIDATION", problem!)
                : await CallAsync(() => _gateway.GenerateEwayBillAsync(gstin, request, ct));
        }

        if (result.Value is EwayBillDetails details)
        {
            EwayBillMapper.Generated(row, transport, details);
            await OnChallanAsync(row, details, ct);
        }
        else
        {
            row.Status = EwayBillStatus.Failed;
            row.LastErrorCode = Truncate(result.ErrorCode ?? "PORTAL", 20);
            row.LastErrorMessage = Truncate(result.ErrorMessage ?? "The portal refused the e-way bill.", 1000);
        }

        await _db.SaveChangesAsync(ct);
        return View(view, row);
    }

    public async Task<EwayBillResult> UpdatePartBAsync(
        EwayBillSource source, long sourceId, UpdatePartBRequest request, CancellationToken ct)
    {
        EwayBill? live = await LiveAsync(source, sourceId, ct);
        if (live is null)
        {
            return new EwayBillResult(EwayBillOutcome.NotFound);
        }

        if (live.Origin == EwayBillOrigin.Manual || live.EwbNo is null)
        {
            return new EwayBillResult(EwayBillOutcome.Refused,
                "This e-way bill was not generated here, so its vehicle is changed on the portal where it was made.");
        }

        EwayTransport transport = Transport(request.TransportMode, live.DistanceKm, request.VehicleNo, live.TransporterId, live.TransporterName);
        IReadOnlyList<EInvoiceProblem> problems = EwayBillMapper.ValidateTransport(transport);
        if (problems.Count > 0)
        {
            return new EwayBillResult(EwayBillOutcome.Invalid, string.Join(" ", problems.Select(p => p.Message)));
        }

        if ((await _identity.GetIdentityAsync(ct))?.Gstin is not { Length: > 0 } gstin)
        {
            return new EwayBillResult(EwayBillOutcome.Refused, "The branch's details could not be read. Try again in a moment.");
        }

        IrpResult<EwayBillDetails> result = await CallAsync(() => _gateway.UpdatePartBAsync(
            gstin, new PartBUpdate(live.EwbNo, transport, request.FromPlace.Trim(), request.FromStateCode, request.Reason.Trim()), ct));
        if (result.Value is not EwayBillDetails details)
        {
            return new EwayBillResult(EwayBillOutcome.Refused, $"The portal did not change the vehicle: {result.ErrorMessage}");
        }

        live.VehicleNo = transport.VehicleNo;
        live.TransportMode = transport.Mode;
        live.ValidUntil = details.ValidUntil ?? live.ValidUntil;
        await _db.SaveChangesAsync(ct);
        return new EwayBillResult(EwayBillOutcome.Ok, EwayBill: View(null, live));
    }

    public async Task<EwayBillResult> CancelAsync(
        EwayBillSource source, long sourceId, CancelEwayBillRequest request, CancellationToken ct)
    {
        EwayBill? live = await LiveAsync(source, sourceId, ct);
        if (live is null)
        {
            return new EwayBillResult(EwayBillOutcome.NotFound);
        }

        DateTimeOffset now = _clock.GetUtcNow();
        if (live.Origin != EwayBillOrigin.Manual)
        {
            if (live.EwbNo is null || live.EwbDate is not DateTimeOffset generated || now - generated > EInvoiceRules.CancelWindow)
            {
                return new EwayBillResult(EwayBillOutcome.Refused,
                    "This e-way bill was generated more than 24 hours ago, so it can no longer be cancelled.");
            }

            if ((await _identity.GetIdentityAsync(ct))?.Gstin is not { Length: > 0 } gstin)
            {
                return new EwayBillResult(EwayBillOutcome.Refused, "The branch's details could not be read. Try again in a moment.");
            }

            IrpResult<EwayBillCancellation> result = await CallAsync(() =>
                _gateway.CancelEwayBillAsync(gstin, live.EwbNo, request.Reason, request.Remark.Trim(), ct));
            if (!result.Ok && result.ErrorCode != IrpErrorCodes.AlreadyCancelled)
            {
                return new EwayBillResult(EwayBillOutcome.Refused, $"The portal did not cancel the e-way bill: {result.ErrorMessage}");
            }
        }

        live.Status = EwayBillStatus.Cancelled;
        live.CancelReason = request.Reason;
        live.CancelledAt = now;

        if (source == EwayBillSource.DeliveryChallan
            && await _db.DeliveryChallans.FirstOrDefaultAsync(c => c.DeliveryChallanId == sourceId, ct) is DeliveryChallan challan
            && challan.EwayBillNo == live.EwbNo)
        {
            challan.EwayBillNo = null;
            challan.EwayBillDate = null;
        }

        await _db.SaveChangesAsync(ct);
        return new EwayBillResult(EwayBillOutcome.Ok, EwayBill: View(null, live));
    }

    /// <summary>
    /// Keeps a challan's typed e-way bill number as a Manual row (TK-93): made
    /// outside the product, so the portal is never asked about it. A typed
    /// number adds or corrects the row; clearing it removes the row, which was
    /// only ever a note. A bill generated here is never touched.
    /// </summary>
    public static async Task SyncManualAsync(SalesDbContext db, DeliveryChallan challan, CancellationToken ct)
    {
        EwayBill? live = await db.EwayBills
            .Where(e => e.SourceType == EwayBillSource.DeliveryChallan
                && e.SourceId == challan.DeliveryChallanId
                && (e.Status == EwayBillStatus.Generated || e.Status == EwayBillStatus.Pending))
            .OrderByDescending(e => e.EwayBillId)
            .FirstOrDefaultAsync(ct);

        if (live is not null && live.Origin != EwayBillOrigin.Manual)
        {
            return;
        }

        string? typed = string.IsNullOrWhiteSpace(challan.EwayBillNo) ? null : challan.EwayBillNo.Trim();
        if (typed is null)
        {
            if (live is not null)
            {
                db.EwayBills.Remove(live);
                await db.SaveChangesAsync(ct);
            }

            return;
        }

        live ??= db.EwayBills.Add(new EwayBill
        {
            SourceType = EwayBillSource.DeliveryChallan,
            SourceId = challan.DeliveryChallanId,
            Origin = EwayBillOrigin.Manual,
        }).Entity;

        live.Status = EwayBillStatus.Generated;
        live.EwbNo = typed.Length <= 12 ? typed : typed[..12];
        live.EwbDate = challan.EwayBillDate is DateOnly date ? new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), Ist) : null;
        live.VehicleNo = challan.VehicleNo;
        live.TransporterName = challan.TransporterName;
        await db.SaveChangesAsync(ct);
    }

    private async Task<DocumentHeaderBase?> HeaderAsync(EwayBillSource source, long sourceId, CancellationToken ct) => source switch
    {
        EwayBillSource.Invoice => await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.InvoiceId == sourceId, ct),
        EwayBillSource.DeliveryChallan => await _db.DeliveryChallans.AsNoTracking().FirstOrDefaultAsync(c => c.DeliveryChallanId == sourceId, ct),
        EwayBillSource.CreditNote => await _db.CreditNotes.AsNoTracking().FirstOrDefaultAsync(c => c.CreditNoteId == sourceId, ct),
        _ => null,
    };

    private Task<EwayBill?> LiveAsync(EwayBillSource source, long sourceId, CancellationToken ct) =>
        _db.EwayBills
            .Where(e => e.SourceType == source && e.SourceId == sourceId
                && (e.Status == EwayBillStatus.Generated || e.Status == EwayBillStatus.Pending))
            .OrderByDescending(e => e.EwayBillId)
            .FirstOrDefaultAsync(ct);

    /// <summary>The standalone request from the document's own lines, with each line's GST unit stamped first.</summary>
    private async Task<(StandaloneEwayBillRequest? Request, string? Problem)> StandaloneAsync(
        EwayBill row, OrgIdentity seller, EwayTransport transport, CancellationToken ct)
    {
        DocumentHeaderBase header;
        List<(DocumentLineBase Line, string? Uqc, IReadOnlyList<DocumentLineTaxBase> Taxes)> lines;
        int subSupply;
        string type;

        if (row.SourceType == EwayBillSource.DeliveryChallan)
        {
            DeliveryChallan challan = await _db.DeliveryChallans.Include(c => c.Lines).ThenInclude(l => l.Taxes)
                .FirstAsync(c => c.DeliveryChallanId == row.SourceId, ct);
            await LineUqc.StampAsync(_uqc, challan.Lines, l => l.UomId, l => l.UqcCode, (l, code) => l.UqcCode = code, ct);
            header = challan;
            lines = [.. challan.Lines.Select(l => ((DocumentLineBase)l, l.UqcCode, (IReadOnlyList<DocumentLineTaxBase>)l.Taxes))];
            subSupply = EwayBillMapper.SubSupplyTypeFor(challan.ChallanType);
            type = "CHL";
        }
        else if (row.SourceType == EwayBillSource.Invoice)
        {
            Invoice invoice = await _db.Invoices.Include(i => i.Lines).ThenInclude(l => l.Taxes)
                .FirstAsync(i => i.InvoiceId == row.SourceId, ct);
            header = invoice;
            lines = [.. invoice.Lines.Select(l => ((DocumentLineBase)l, l.UqcCode, (IReadOnlyList<DocumentLineTaxBase>)l.Taxes))];
            subSupply = 1;
            type = "INV";
        }
        else
        {
            return (null, "E-way bills for returns are not generated here yet.");
        }

        ContactPostalAddress? buyer;
        try
        {
            buyer = (await _addresses.FindAsync([header.ContactId], ct)).GetValueOrDefault(header.ContactId);
        }
        catch (HttpRequestException)
        {
            return (null, "The customer's address could not be read. Try again in a moment.");
        }

        if (buyer is null)
        {
            return (null, "The customer is not in this branch's contacts.");
        }

        IReadOnlyDictionary<long, NamedRef> names = await _items.ResolveAsync(
            [.. lines.Select(l => l.Line.ItemId).OfType<long>().Distinct()], ct);

        StandaloneEwayBillRequest request = EwayBillMapper.Standalone(
            type, subSupply, header, lines, names.ToDictionary(n => n.Key, n => n.Value.Name), seller, buyer, transport);
        IReadOnlyList<EInvoiceProblem> problems = EwayBillMapper.Validate(request);
        return problems.Count == 0 ? (request, null) : (null, string.Join(" ", problems.Select(p => p.Message)));
    }

    /// <summary>
    /// A challan carries its bill on its face: its two columns take the number
    /// and the date, and its filed PDF is written again with them on it.
    /// </summary>
    private async Task OnChallanAsync(EwayBill row, EwayBillDetails details, CancellationToken ct)
    {
        if (row.SourceType != EwayBillSource.DeliveryChallan)
        {
            return;
        }

        DeliveryChallan challan = await _db.DeliveryChallans.Include(c => c.Lines)
            .FirstAsync(c => c.DeliveryChallanId == row.SourceId, ct);
        challan.EwayBillNo = details.EwbNo;
        challan.EwayBillDate = DateOnly.FromDateTime(details.EwbDate.ToOffset(Ist).DateTime);
        challan.VehicleNo ??= row.VehicleNo;
        challan.TransporterName ??= row.TransporterName;

        try
        {
            string valid = details.ValidUntil is DateTimeOffset until
                ? $", valid until {until.ToOffset(Ist):dd-MMM-yyyy}"
                : string.Empty;
            await _archive.ArchiveAsync(
                _archive.Scope(),
                ArchivedSalesDocument.DeliveryChallan,
                challan.DeliveryChallanId,
                "DELIVERY CHALLAN",
                "Challan No",
                challan,
                challan.Lines,
                $"E-way bill {details.EwbNo} dated {challan.EwayBillDate:dd-MMM-yyyy}{valid}"
                    + (row.VehicleNo is null ? string.Empty : $", vehicle {row.VehicleNo}"),
                ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or StorageKeyExistsException or IOException)
        {
            // The bill is generated and recorded; a PDF that could not be
            // written again is not a reason to lose that.
            _log.LogWarning(ex, "The challan {ChallanId}'s PDF could not be written again with its e-way bill.", challan.DeliveryChallanId);
        }
    }

    private static EwayTransport Transport(TransportMode mode, int distance, string? vehicle, string? transporterId, string? transporterName) =>
        new(
            mode,
            distance,
            string.IsNullOrWhiteSpace(vehicle) ? null : vehicle.Replace(" ", string.Empty).ToUpperInvariant(),
            string.IsNullOrWhiteSpace(transporterId) ? null : transporterId.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(transporterName) ? null : transporterName.Trim());

    private static async Task<IrpResult<T>> CallAsync<T>(Func<Task<IrpResult<T>>> call)
        where T : class
    {
        try
        {
            return await call();
        }
        catch (HttpRequestException ex)
        {
            return IrpResult<T>.Unavailable(ex.Message);
        }
    }

    private async Task<EwayBill?> LoadAsync(long id, CancellationToken ct)
    {
        EwayBill? tracked = _db.EwayBills.Local.FirstOrDefault(e => e.EwayBillId == id);
        if (tracked is not null)
        {
            await _db.Entry(tracked).ReloadAsync(ct);
            return _db.Entry(tracked).State == EntityState.Detached ? null : tracked;
        }

        return await _db.EwayBills.FirstOrDefaultAsync(e => e.EwayBillId == id, ct);
    }

    public static EwayBillView View(EwayBillView? view, EwayBill row)
    {
        view ??= new EwayBillView();
        view.EwayBillId = row.EwayBillId;
        view.Origin = row.Origin;
        view.Status = row.Status;
        view.EwbNo = row.EwbNo;
        view.EwbDate = row.EwbDate;
        view.ValidUntil = row.ValidUntil;
        view.TransportMode = row.TransportMode;
        view.VehicleNo = row.VehicleNo;
        view.TransporterId = row.TransporterId;
        view.TransporterName = row.TransporterName;
        view.DistanceKm = row.DistanceKm;
        view.Message = row.Status == EwayBillStatus.Failed ? row.LastErrorMessage : null;
        return view;
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
