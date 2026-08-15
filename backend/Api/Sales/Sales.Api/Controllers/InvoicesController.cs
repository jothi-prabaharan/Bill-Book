using Microsoft.AspNetCore.Mvc;
using Sales.Api.Services;
using Sales.Entity.Models;

namespace Sales.Api.Controllers;

[ApiController]
[Route("api/sales/invoices")]
public sealed class InvoicesController : ControllerBase
{
    private readonly InvoiceService _service;

    public InvoicesController(InvoiceService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var list = await _service.ListAsync(from, to, ct);
        return Ok(list);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var view = await _service.GetAsync(id, ct);
        if (view is null)
            return NotFound();

        return Ok(view);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveInvoiceRequest request, CancellationToken ct)
    {
        var id = await _service.SaveAsync(request, null, ct);
        return Ok(new { InvoiceId = id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveInvoiceRequest request, CancellationToken ct)
    {
        await _service.SaveAsync(request, id, ct);
        return Ok();
    }

    [HttpPost("{id}/post")]
    public async Task<IActionResult> Post(long id, CancellationToken ct)
    {
        await _service.PostAsync(id, ct);
        return Ok();
    }

    [HttpPost("{id}/void")]
    public async Task<IActionResult> Void(long id, CancellationToken ct)
    {
        await _service.VoidAsync(id, ct);
        return Ok();
    }
}
