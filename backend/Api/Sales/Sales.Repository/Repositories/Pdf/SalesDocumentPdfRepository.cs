using Sales.Repository.Clients;

namespace Sales.Repository.Repositories;

/// <summary>The three sales documents that can be downloaded as a PDF.</summary>
public enum SalesPdfKind
{
    Invoice = 1,
    CreditNote = 2,
    DeliveryChallan = 3,
}

/// <summary>A PDF on its way to the browser.</summary>
public sealed record DocumentPdf(byte[] Content, string FileName);

/// <summary>
/// The PDF of an invoice, credit note or delivery challan, for the staff download and the client portal
/// (TK-266).
///
/// <b>Laid out by the document's own print template, every time it is asked for.</b> It is the same
/// payload, template and stamp the View page uses, printed by Printing's PDF route, so the PDF is the page
/// on the screen and a change to the template changes both. It carries the stamp the screen shows: PROFORMA
/// on a draft invoice, DRAFT on a draft credit note or challan, and VOID on a voided one.
///
/// <b>Nothing is stored.</b> A PDF used to be rendered and filed when the document posted, in a fixed
/// PDFsharp layout that never saw the template (D-22); that layout and the filing are gone (D-72). The
/// after-commit queue runs before the response is written, so a render queued there would still have added
/// its time to every posting, and a stored copy would have gone stale the first time the template changed.
/// The files filed before TK-266 stay in storage and are never read.
///
/// An id that is not in this branch is null, which the caller answers as not found, never as forbidden: row
/// level security hides another branch's rows, so there is nothing to tell the two apart.
/// </summary>
public sealed class SalesDocumentPdfRepository
{
    private readonly InvoicePrintRepository _invoices;
    private readonly SalesPrintRepository _documents;

    public SalesDocumentPdfRepository(InvoicePrintRepository invoices, SalesPrintRepository documents)
    {
        _invoices = invoices;
        _documents = documents;
    }

    /// <summary>
    /// The file name a browser saves: the document number, with the slashes a
    /// series puts in it turned into hyphens.
    /// </summary>
    public static string FileName(string documentNo) =>
        string.Concat(documentNo.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == '/' || c == '\\' ? '-' : c))
        + ".pdf";

    public async Task<DocumentPdf?> OpenAsync(SalesPdfKind kind, long id, CancellationToken ct)
    {
        PrintedPdf? pdf = kind switch
        {
            SalesPdfKind.Invoice => await _invoices.PrintPdfAsync(id, ct),
            SalesPdfKind.CreditNote => await _documents.PrintCreditNotePdfAsync(id, ct),
            SalesPdfKind.DeliveryChallan => await _documents.PrintChallanPdfAsync(id, ct),
            _ => null,
        };

        return pdf is null ? null : new DocumentPdf(pdf.Content, FileName(pdf.DocumentNo));
    }
}
