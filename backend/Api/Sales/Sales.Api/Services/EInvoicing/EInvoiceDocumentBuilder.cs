using Microsoft.EntityFrameworkCore;
using Sales.Entity.Enums;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Contacts;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Sales.Api.Services.EInvoicing;

/// <summary>What building a document's INV-01 came to (TK-91).</summary>
public sealed record EInvoiceBuild(
    /// <summary>Whether the document needs an IRN at all: the branch e-invoices by its date, and the supply is B2B, export or SEZ.</summary>
    bool Applies,
    Inv01Document? Document,
    IReadOnlyList<EInvoiceProblem> Problems,

    /// <summary>
    /// True when a problem was that Master could not be asked, which a later
    /// attempt may get past, as against a problem with the document itself.
    /// </summary>
    bool Transient = false,

    /// <summary>
    /// Set when the document asks for its e-way bill with the IRN (TK-93): the
    /// branch generates e-way bills, the invoice carries a vehicle or a
    /// transporter, and its value is over the limit.
    /// </summary>
    EwayTransport? Transport = null)
{
    public bool Ready => Applies && Document is not null && Problems.Count == 0;

    public static EInvoiceBuild NotApplicable { get; } = new(false, null, []);
}

/// <summary>
/// Gathers what an invoice's or credit note's INV-01 needs — the document, the
/// branch from Master's org context, the buyer's address from Master, item names
/// from Inventory — maps it and checks it (TK-91). TK-92 calls this after the
/// posting commits, and before every retry.
/// </summary>
public sealed class EInvoiceDocumentBuilder
{
    /// <summary>India Standard Time. The branch's "today" for the reporting window.</summary>
    private static readonly TimeSpan Ist = TimeSpan.FromHours(5.5);

    private readonly SalesDbContext _db;
    private readonly IBranchSettingsProvider _settings;
    private readonly IOrgIdentityProvider _identity;
    private readonly IContactAddressBook _addresses;
    private readonly IItemNameLookup _items;
    private readonly TimeProvider _clock;

    public EInvoiceDocumentBuilder(
        SalesDbContext db,
        IBranchSettingsProvider settings,
        IOrgIdentityProvider identity,
        IContactAddressBook addresses,
        IItemNameLookup items,
        TimeProvider clock)
    {
        _db = db;
        _settings = settings;
        _identity = identity;
        _addresses = addresses;
        _items = items;
        _clock = clock;
    }

    public async Task<EInvoiceBuild> BuildForInvoiceAsync(long invoiceId, CancellationToken ct)
    {
        Invoice? invoice = await _db.Invoices.AsNoTracking()
            .Include(i => i.Lines).ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct);
        if (invoice is null)
        {
            return EInvoiceBuild.NotApplicable;
        }

        EInvoiceBuild build = await BuildAsync(
            EInvoiceSource.Invoice,
            invoice,
            invoice.Lines.Select(l => (Line: (DocumentLineBase)l, l.UqcCode, Taxes: Taxes(l.Taxes))).ToList(),
            precedingNo: null,
            precedingDate: null,
            ct);

        return await WithEwayBillAsync(build, invoice, ct);
    }

    public async Task<EInvoiceBuild> BuildForCreditNoteAsync(long creditNoteId, CancellationToken ct)
    {
        CreditNote? note = await _db.CreditNotes.AsNoTracking()
            .Include(c => c.Lines).ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(c => c.CreditNoteId == creditNoteId, ct);
        if (note is null)
        {
            return EInvoiceBuild.NotApplicable;
        }

        var original = await _db.Invoices.AsNoTracking()
            .Where(i => i.InvoiceId == note.InvoiceId)
            .Select(i => new { i.DocumentNo, i.DocumentDate })
            .FirstOrDefaultAsync(ct);

        return await BuildAsync(
            EInvoiceSource.CreditNote,
            note,
            note.Lines.Select(l => (Line: (DocumentLineBase)l, l.UqcCode, Taxes: Taxes(l.Taxes))).ToList(),
            original?.DocumentNo,
            original?.DocumentDate,
            ct);
    }

    /// <summary>
    /// Asks for the e-way bill in the same call as the IRN when the invoice
    /// carries transport details and is over the limit, on a branch that
    /// generates e-way bills (design, flow step 6).
    /// </summary>
    private async Task<EInvoiceBuild> WithEwayBillAsync(EInvoiceBuild build, Invoice invoice, CancellationToken ct)
    {
        if (build.Document is null || invoice.TotalAmount <= EInvoiceRules.EwayBillThreshold
            || (invoice.VehicleNo is null && invoice.TransporterId is null)
            || (await _settings.GetSettingsAsync(ct)) is not { EwayBillEnabled: true })
        {
            return build;
        }

        var transport = new EwayTransport(
            invoice.TransportMode ?? TransportMode.Road,
            invoice.TransportDistanceKm ?? 0,
            invoice.VehicleNo,
            invoice.TransporterId,
            invoice.TransporterName);

        return build with
        {
            Document = build.Document with { EwbDtls = EwayBillMapper.Details(transport) },
            Transport = transport,
        };
    }

    private async Task<EInvoiceBuild> BuildAsync(
        EInvoiceSource kind,
        DocumentHeaderBase header,
        List<(DocumentLineBase Line, string? UqcCode, IReadOnlyList<EInvoiceInputTax> Taxes)> lines,
        string? precedingNo,
        DateOnly? precedingDate,
        CancellationToken ct)
    {
        BranchSettings? settings = await _settings.GetSettingsAsync(ct);
        OrgIdentity? seller = await _identity.GetIdentityAsync(ct);
        if (settings is null || seller is null)
        {
            return new EInvoiceBuild(true, null,
                [new EInvoiceProblem("SELLER_UNREAD", "The branch's details could not be read. It will be tried again.")],
                Transient: true);
        }

        if (!EInvoiceApplicability.BranchApplies(settings.EInvoiceFrom, header.DocumentDate))
        {
            return EInvoiceBuild.NotApplicable;
        }

        ContactPostalAddress? buyer;
        try
        {
            IReadOnlyDictionary<long, ContactPostalAddress> found = await _addresses.FindAsync([header.ContactId], ct);
            buyer = found.GetValueOrDefault(header.ContactId);
        }
        catch (HttpRequestException)
        {
            return new EInvoiceBuild(true, null,
                [new EInvoiceProblem("BUYER_UNREAD", "The customer's address could not be read. It will be tried again.")],
                Transient: true);
        }

        if (buyer is null)
        {
            return new EInvoiceBuild(true, null, [new EInvoiceProblem("BUYER", "The customer is not in this branch's contacts.")]);
        }

        // The GSTIN the document was raised against is the one that counts; the
        // contact's may have changed since.
        string? buyerGstin = string.IsNullOrWhiteSpace(header.ContactGstin) ? buyer.Gstin : header.ContactGstin;
        bool igstCharged = header.IgstAmount > 0;
        if (EInvoiceApplicability.SupplyTypeFor(buyer.RegistrationType, buyerGstin, igstCharged) is null)
        {
            return EInvoiceBuild.NotApplicable;
        }

        IReadOnlyDictionary<long, NamedRef> names = await _items.ResolveAsync(
            [.. lines.Select(l => l.Line.ItemId).OfType<long>().Distinct()], ct);

        var input = new EInvoiceInput(
            kind,
            header.DocumentNo,
            header.DocumentDate,
            seller,
            new ContactPostalAddress
            {
                ContactId = buyer.ContactId,
                LegalName = buyer.LegalName,
                Gstin = buyerGstin,
                AddressLine1 = buyer.AddressLine1,
                AddressLine2 = buyer.AddressLine2,
                City = buyer.City,
                PostalCode = buyer.PostalCode,
                StateCode = buyer.StateCode,
                PhoneNumber = buyer.PhoneNumber,
                RegistrationType = buyer.RegistrationType,
            },
            buyer.RegistrationType,
            header.IsInterState,
            [.. lines.Select(l => new EInvoiceInputLine(
                l.Line.LineNumber,
                l.Line.Description ?? (l.Line.ItemId is long id && names.TryGetValue(id, out NamedRef? name) ? name.Name : null),
                l.Line.HsnSacCode,
                l.Line.Quantity,
                l.UqcCode,
                settings.DiscountBeforeTax ? l.Line.DiscountAmount : 0m,
                l.Line.TaxableAmount,
                l.Taxes))],
            header.TotalAmount,
            header.RoundOffAmount,
            precedingNo,
            precedingDate);

        Inv01Document document = Inv01Mapper.Map(input);
        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().ToOffset(Ist).DateTime);
        return new EInvoiceBuild(true, document, EInvoiceValidator.Validate(document, header.DocumentDate, today));
    }

    private static IReadOnlyList<EInvoiceInputTax> Taxes<TTax>(IEnumerable<TTax> taxes)
        where TTax : DocumentLineTaxBase =>
        [.. taxes.Select(t => new EInvoiceInputTax(t.TaxComponent, t.Rate, t.Amount))];
}

/// <summary>
/// Chooses the gateway (TK-91). <c>EInvoicing:Gateway</c> names it; unset, it is
/// the sandbox in Development and <see cref="UnconfiguredEInvoiceGateway"/>
/// everywhere else. The sandbox is refused in Production outright, because its
/// IRNs are issued by no portal and must never reach a real invoice.
/// </summary>
public static class EInvoiceGatewayRegistration
{
    public static IServiceCollection AddEInvoiceGateway(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        string gateway = configuration["EInvoicing:Gateway"] is { Length: > 0 } named
            ? named
            : environment.IsDevelopment() ? "Sandbox" : "None";

        switch (gateway)
        {
            case "Sandbox" when environment.IsProduction():
                throw new InvalidOperationException(
                    "EInvoicing:Gateway is Sandbox in Production. The sandbox issues IRNs no portal registered; "
                        + "configure a real provider or leave the setting empty.");
            case "Sandbox":
                services.AddSingleton<IEInvoiceGateway, SandboxEInvoiceGateway>();
                break;
            case "None":
                services.AddSingleton<IEInvoiceGateway, UnconfiguredEInvoiceGateway>();
                break;
            default:
                throw new InvalidOperationException(
                    $"EInvoicing:Gateway is '{gateway}', which is not a gateway this build knows. Use Sandbox, or leave it empty.");
        }

        services.AddScoped<EInvoiceDocumentBuilder>();
        return services;
    }
}
