using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Entity.Models;
using Sales.Repository.Repositories;
using Shared.Kernel.Internal;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;

namespace Sales.Api.Controllers;

public sealed class InternalCreateAndPostInvoiceRequest
{
    public Guid CustomerId { get; set; }
    public Guid OrgId { get; set; }
    public SaveInvoiceRequest Request { get; set; } = new();
}

public sealed class InternalCreateAndPostInvoiceResult
{
    public bool Success { get; set; }
    public long InvoiceId { get; set; }
    public string? InvoiceNo { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Internal invoice creation and posting for Site billing worksheets (TK-175).
/// Creates the invoice and posts it immediately.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[NoTransaction]
[Route("internal/invoices")]
public sealed class InternalInvoicesController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalInvoicesController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("create-and-post")]
    public async Task<IActionResult> CreateAndPost(
        [FromBody] InternalCreateAndPostInvoiceRequest request, CancellationToken ct)
    {
        if (request.CustomerId == Guid.Empty || request.OrgId == Guid.Empty)
        {
            return BadRequest(new MessageResponse { Message = "A customer and an organization are required." });
        }

        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        var repo = _services.GetRequiredService<IInvoiceRepository>();

        var createResult = await repo.CreateAsync(request.Request, ct);
        if (createResult.Outcome != InvoiceOutcome.Ok)
        {
            return Conflict(new InternalCreateAndPostInvoiceResult
            {
                Success = false,
                Error = createResult.Detail ?? $"Create refused: {createResult.Outcome}",
            });
        }

        var postResult = await repo.PostAsync(createResult.InvoiceId, ct);
        if (postResult.Outcome != InvoiceOutcome.Ok)
        {
            return Conflict(new InternalCreateAndPostInvoiceResult
            {
                Success = false,
                InvoiceId = createResult.InvoiceId,
                Error = postResult.Detail ?? $"Post refused: {postResult.Outcome}",
            });
        }

        var view = await repo.GetAsync(createResult.InvoiceId, ct);

        return Ok(new InternalCreateAndPostInvoiceResult
        {
            Success = true,
            InvoiceId = createResult.InvoiceId,
            InvoiceNo = view?.DocumentNo,
        });
    }
}
