using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Api.Services;
using Sales.Api.Services.Pdf;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

/// <summary>
/// A contact's own invoices in the client portal (TK-95): the list, one
/// invoice with its lines, and its archived PDF. The contact is taken only from
/// the portal token; an invoice that is not theirs, or not posted, is 404.
/// </summary>
[ApiController]
[Authorize]
[RequirePortalAccess]
[Route("api/portal/invoices")]
[RequireApp(App.RetailErp)]
public sealed class PortalInvoicesController : ControllerBase
{
    private readonly PortalInvoiceService _invoices;

    public PortalInvoicesController(PortalInvoiceService invoices) => _invoices = invoices;

    private long ContactId => long.Parse(User.FindFirst(RequirePortalAccessAttribute.ContactClaim)!.Value);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _invoices.ListAsync(ContactId, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        await _invoices.GetAsync(ContactId, id, ct) is { } detail ? Ok(detail) : NotFound();

    [HttpGet("{id:long}/pdf")]
    public async Task<IActionResult> Pdf(long id, CancellationToken ct)
    {
        ArchivedPdf? pdf = await _invoices.PdfAsync(ContactId, id, ct);
        return pdf is null ? NotFound() : File(pdf.Content, "application/pdf", pdf.FileName);
    }
}
