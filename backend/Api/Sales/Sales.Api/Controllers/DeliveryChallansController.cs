using Microsoft.AspNetCore.Mvc;
using Sales.Api.Services;
using Sales.Entity.Models;

namespace Sales.Api.Controllers;

[ApiController]
[Route("DeliveryChallans")]
public sealed class DeliveryChallansController : ControllerBase
{
    private readonly DeliveryChallanService _service;

    public DeliveryChallansController(DeliveryChallanService service)
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
    public async Task<IActionResult> Save([FromBody] SaveDeliveryChallanRequest request, CancellationToken ct)
    {
        var id = await _service.SaveAsync(null, request, ct);
        return Ok(new { DeliveryChallanId = id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveDeliveryChallanRequest request, CancellationToken ct)
    {
        await _service.SaveAsync(id, request, ct);
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


