using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shared.Kernel.Printing;

namespace Sales.Api.Services.Printing;

/// <summary>Printing's answer: the document, laid out, as HTML.</summary>
public sealed class PrintedDocument
{
    public string Html { get; set; } = string.Empty;

    public int PageCount { get; set; }

    public IReadOnlyList<string> UnknownTags { get; set; } = [];

    /// <summary>Null when the layout was the platform's generated one, not a stored template.</summary>
    public long? PrintTemplateId { get; set; }
}

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
/// Posts a document's payload to Printing's <c>api/print/render</c>.
///
/// <b>Under the user's own token, not the internal key</b> — the decision stage P
/// of the Printing design turns on. The token names the branch, so Printing
/// resolves that branch's template through its own query filter and no
/// internal endpoint has to be told which branch it is serving. The bearer
/// header is forwarded from the request that asked Sales to print; a print is
/// always something a signed-in person asked for.
/// </summary>
public sealed class PrintingClient : IPrintingClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _context;

    public PrintingClient(HttpClient http, IHttpContextAccessor context)
    {
        _http = http;
        _context = context;
    }

    public async Task<PrintedDocument> RenderAsync(
        string documentTypeCode,
        long? printTemplateId,
        PrintPayload payload,
        string? watermark,
        CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/print/render")
        {
            Content = JsonContent.Create(
                new
                {
                    documentTypeCode,
                    printTemplateId,
                    watermark,
                    payload = new { singles = payload.Singles, lists = payload.Lists },
                },
                options: Json),
        };

        string? authorization = _context.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization)
            && AuthenticationHeaderValue.TryParse(authorization, out AuthenticationHeaderValue? header))
        {
            message.Headers.Authorization = header;
        }

        using HttpResponseMessage response = await _http.SendAsync(message, ct);

        // A refusal here is Printing's, not the user's: the invoice was found and
        // the caller may print it, so anything else is a failure for the error
        // handler to log and answer, not a message for this client to write.
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PrintedDocument>(Json, ct)
            ?? throw new InvalidOperationException("Printing returned an empty response.");
    }
}
