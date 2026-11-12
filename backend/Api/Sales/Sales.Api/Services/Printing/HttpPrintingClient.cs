using Sales.Repository.Repositories;
using Sales.Repository.Clients;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shared.Kernel.Printing;

namespace Sales.Api.Services;

/// <summary>
/// Posts a document's payload to Printing's <c>api/print/render</c>, or to <c>api/print/render-pdf</c>
/// for the same document as a PDF.
///
/// <b>Under the user's own token, not the internal key</b> — the decision stage P
/// of the Printing design turns on. The token names the branch, so Printing
/// resolves that branch's template through its own query filter and no
/// internal endpoint has to be told which branch it is serving. The bearer
/// header is forwarded from the request that asked Sales to print; a print is
/// always something a signed-in person asked for. A client-portal download is one
/// of those: the portal's token names the branch the same way.
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
        using HttpResponseMessage response =
            await PostAsync("api/print/render", documentTypeCode, printTemplateId, payload, watermark, ct);

        return await response.Content.ReadFromJsonAsync<PrintedDocument>(Json, ct)
            ?? throw new InvalidOperationException("Printing returned an empty response.");
    }

    public async Task<byte[]> RenderPdfAsync(
        string documentTypeCode,
        long? printTemplateId,
        PrintPayload payload,
        string? watermark,
        CancellationToken ct)
    {
        using HttpResponseMessage response =
            await PostAsync("api/print/render-pdf", documentTypeCode, printTemplateId, payload, watermark, ct);

        byte[] pdf = await response.Content.ReadAsByteArrayAsync(ct);
        return pdf.Length > 0
            ? pdf
            : throw new InvalidOperationException("Printing returned an empty PDF.");
    }

    private async Task<HttpResponseMessage> PostAsync(
        string path,
        string documentTypeCode,
        long? printTemplateId,
        PrintPayload payload,
        string? watermark,
        CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, path)
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

        HttpResponseMessage response = await _http.SendAsync(message, ct);

        // A refusal here is Printing's, not the user's: the document was found and
        // the caller may print it, so anything else is a failure for the error
        // handler to log and answer, not a message for this client to write.
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            response.Dispose();
            throw;
        }

        return response;
    }
}
