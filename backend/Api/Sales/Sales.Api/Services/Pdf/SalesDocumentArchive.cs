using Microsoft.EntityFrameworkCore;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Storage;
using Shared.Kernel.Tenancy;

namespace Sales.Api.Services.Pdf;

/// <summary>The three sales documents that keep an archived PDF (TK-22).</summary>
public enum ArchivedSalesDocument
{
    Invoice = 1,
    CreditNote = 2,
    DeliveryChallan = 3,
}

/// <summary>An archived PDF on its way to the browser.</summary>
public sealed record ArchivedPdf(Stream Content, string FileName);

/// <summary>
/// Where each posted sales document's PDF is filed, how a credit note or a
/// challan writes its own, and how any of them is read back (TK-22).
///
/// <b>One key per document</b>: <c>{customer}/{branch}/retail-erp/sales/{folder}/{id}.pdf</c>,
/// the folder fixed per type. The invoice writes its file inside
/// <c>InvoiceService.PostAsync</c>, as it did before this class; the key is the
/// same one.
///
/// <b>Written with <see cref="FileWriteMode.Replace"/></b>, before the post
/// commits, so a post whose commit fails and is retried writes over its own
/// leftover instead of being refused by it.
///
/// <b>Read back only for a document this branch can see and has posted.</b> The
/// lookup goes through the query filter, so another branch's id is simply not
/// found — <c>NotFound</c>, never <c>Forbid</c>, because telling the two apart
/// would confirm the id exists in someone else's books (CLAUDE.md, TK-71).
/// </summary>
public sealed class SalesDocumentArchive
{
    private readonly SalesDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IFileStorage _storage;
    private readonly IOrgIdentityProvider _orgIdentity;
    private readonly IContactNameLookup _contactNames;
    private readonly IItemNameLookup _itemNames;
    private readonly ISalesDocumentPdfRenderer _renderer;

    public SalesDocumentArchive(
        SalesDbContext db,
        ITenantContext tenant,
        IFileStorage storage,
        IOrgIdentityProvider orgIdentity,
        IContactNameLookup contactNames,
        IItemNameLookup itemNames,
        ISalesDocumentPdfRenderer renderer)
    {
        _db = db;
        _tenant = tenant;
        _storage = storage;
        _orgIdentity = orgIdentity;
        _contactNames = contactNames;
        _itemNames = itemNames;
        _renderer = renderer;
    }

    public static string Folder(ArchivedSalesDocument kind) => kind switch
    {
        ArchivedSalesDocument.Invoice => "invoices",
        ArchivedSalesDocument.CreditNote => "credit-notes",
        ArchivedSalesDocument.DeliveryChallan => "delivery-challans",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static string Key(StorageScope scope, ArchivedSalesDocument kind, long id) =>
        StorageKey.DocumentKey(scope, Folder(kind), $"{id}.pdf");

    /// <summary>
    /// The file name a browser saves: the document number, with the slashes a
    /// series puts in it turned into hyphens.
    /// </summary>
    public static string FileName(string documentNo) =>
        string.Concat(documentNo.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == '/' || c == '\\' ? '-' : c))
        + ".pdf";

    /// <summary>
    /// Where this request's branch files sales documents. Throws for a token with
    /// no customer code, so a service calls it <b>before</b> anything it cannot
    /// take back — stock issued, a ledger posted.
    /// </summary>
    public StorageScope Scope() => StorageScope.For(_tenant, StorageApp.RetailErp, StorageModule.Sales);

    /// <summary>
    /// Renders and files one credit note or challan. The seller must resolve: a
    /// document naming a placeholder seller looks filed when it is not, so a
    /// branch Master cannot identify stops the post.
    /// </summary>
    public async Task ArchiveAsync(
        StorageScope scope,
        ArchivedSalesDocument kind,
        long id,
        string title,
        string numberLabel,
        DocumentHeaderBase header,
        IReadOnlyList<DocumentLineBase> lines,
        string? reference,
        CancellationToken ct)
    {
        OrgIdentity seller = await _orgIdentity.GetIdentityAsync(ct)
            ?? throw new InvalidOperationException(
                "The branch issuing this document could not be identified, so its seller "
                + "details cannot be printed. The document was not posted.");

        var contacts = await _contactNames.ResolveAsync([header.ContactId], ct);
        long[] itemIds = lines.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value).Distinct().ToArray();
        var items = itemIds.Length == 0
            ? new Dictionary<long, NamedRef>()
            : await _itemNames.ResolveAsync(itemIds, ct);

        byte[] pdf = _renderer.Render(new SalesPdfModel
        {
            Title = title,
            NumberLabel = numberLabel,
            Header = header,
            Lines = lines,
            Reference = reference,
            SellerName = seller.Name,
            SellerGstin = seller.Gstin,
            SellerAddress = SellerAddress(seller),
            BuyerName = contacts.TryGetValue(header.ContactId, out NamedRef? buyer) ? buyer.Name : "Unknown",
            ItemNames = items.ToDictionary(i => i.Key, i => i.Value.Name),
        });

        await _storage.SaveAsync(
            Key(scope, kind, id), new MemoryStream(pdf), "application/pdf", FileWriteMode.Replace, ct);
    }

    /// <summary>
    /// The stored PDF of a posted document, or null when this branch has no such
    /// posted document or its file is missing.
    /// </summary>
    public async Task<ArchivedPdf?> OpenAsync(ArchivedSalesDocument kind, long id, CancellationToken ct)
    {
        string? documentNo = kind switch
        {
            ArchivedSalesDocument.Invoice => await _db.Invoices.AsNoTracking()
                .Where(x => x.InvoiceId == id && x.PostedAt != null)
                .Select(x => x.DocumentNo).FirstOrDefaultAsync(ct),
            ArchivedSalesDocument.CreditNote => await _db.CreditNotes.AsNoTracking()
                .Where(x => x.CreditNoteId == id && x.PostedAt != null)
                .Select(x => x.DocumentNo).FirstOrDefaultAsync(ct),
            ArchivedSalesDocument.DeliveryChallan => await _db.DeliveryChallans.AsNoTracking()
                .Where(x => x.DeliveryChallanId == id && x.PostedAt != null)
                .Select(x => x.DocumentNo).FirstOrDefaultAsync(ct),
            _ => null,
        };

        if (documentNo is null)
        {
            return null;
        }

        Stream? content = await _storage.OpenReadAsync(Key(Scope(), kind, id), ct);
        return content is null ? null : new ArchivedPdf(content, FileName(documentNo));
    }

    private static string? SellerAddress(OrgIdentity seller)
    {
        string address = string.Join(", ", new[] { seller.AddressLine1, seller.AddressLine2, seller.City, seller.PostalCode }
            .Where(p => !string.IsNullOrWhiteSpace(p)));
        return address.Length == 0 ? null : address;
    }
}
