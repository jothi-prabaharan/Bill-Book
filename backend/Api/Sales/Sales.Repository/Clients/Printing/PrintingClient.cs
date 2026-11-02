using Sales.Repository.Repositories;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shared.Kernel.Printing;

namespace Sales.Repository.Clients;

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

    /// <summary>The document's own number. Null on a document the service did not name (a kitchen ticket).</summary>
    public string? DocumentNo { get; set; }

    /// <summary>The three-letter transaction type the layout was chosen for.</summary>
    public string? DocumentTypeCode { get; set; }

    /// <summary>The document's status, by name: Draft, ReadyToPost, Posted or Void.</summary>
    public string? Status { get; set; }
}
