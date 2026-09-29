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

/// <summary>Printing's answer: the document, laid out, as HTML.</summary>
public sealed class PrintedDocument
{
    public string Html { get; set; } = string.Empty;

    public int PageCount { get; set; }

    public IReadOnlyList<string> UnknownTags { get; set; } = [];

    /// <summary>Null when the layout was the platform's generated one, not a stored template.</summary>
    public long? PrintTemplateId { get; set; }
}
