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

/// <summary>Printing's answer: the document, laid out, as HTML.</summary>
public sealed class PrintedDocument
{
    public string Html { get; set; } = string.Empty;

    public int PageCount { get; set; }

    public IReadOnlyList<string> UnknownTags { get; set; } = [];

    /// <summary>Null when the layout was the platform's generated one, not a stored template.</summary>
    public long? PrintTemplateId { get; set; }
}
