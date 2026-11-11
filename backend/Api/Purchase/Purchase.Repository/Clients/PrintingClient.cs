using Shared.Kernel.Printing;

namespace Purchase.Repository.Clients;

/// <summary>
/// Printing's <c>api/print/render</c>, which lays a document's payload out by its print template.
/// The implementation lives in Purchase.Api because it forwards the caller's own bearer token.
/// </summary>
public interface IPrintingClient
{
    /// <param name="watermark">Stamped across every page; null for none.</param>
    Task<PrintedDocument> RenderAsync(
        string documentTypeCode,
        long? printTemplateId,
        PrintPayload payload,
        string? watermark,
        CancellationToken ct);
}

/// <summary>
/// Printing's answer: the document, laid out, as HTML. The three identity fields are not Printing's: the
/// service that built the payload fills them in, so the page that shows the document can say which one it is
/// without asking for it a second time.
/// </summary>
public sealed class PrintedDocument
{
    public string Html { get; set; } = string.Empty;

    public int PageCount { get; set; }

    public IReadOnlyList<string> UnknownTags { get; set; } = [];

    /// <summary>Null when the layout was the platform's generated one, not a stored template.</summary>
    public long? PrintTemplateId { get; set; }

    /// <summary>The name of the template the layout came from. Null with the generated layout.</summary>
    public string? PrintTemplateName { get; set; }

    /// <summary>
    /// The template the document itself names, or null when it names none. When it is set and differs
    /// from <see cref="PrintTemplateId"/>, the one it names could not be used and the page says so.
    /// </summary>
    public long? ChosenPrintTemplateId { get; set; }

    /// <summary>The document's own number. Null on a document the service did not name (a kitchen ticket).</summary>
    public string? DocumentNo { get; set; }

    /// <summary>The three-letter transaction type the layout was chosen for.</summary>
    public string? DocumentTypeCode { get; set; }

    /// <summary>The document's status, by name: Draft, ReadyToPost, Posted or Void.</summary>
    public string? Status { get; set; }
}
