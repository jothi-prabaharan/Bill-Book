using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Security;
using Site.Entity.Enums;
using Site.Repository.Repositories;

namespace Site.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("site")]
[Route("api/site/stock")]
[RequireBusinessType(BusinessType.Service)]
[RequireAddOn(AddOn.SiteServices)]
public sealed class SiteStockController : ControllerBase
{
    private readonly SiteStockRepository _repo;

    public SiteStockController(SiteStockRepository repo)
    {
        _repo = repo;
    }

    [HttpGet("balances")]
    public async Task<IActionResult> ListBalances([FromQuery] long siteId, CancellationToken ct)
    {
        if (siteId <= 0)
        {
            return BadRequest(new { Message = "Site ID is required." });
        }

        var balances = await _repo.ListBalancesAsync(siteId, ct);
        return Ok(balances);
    }

    [HttpGet("documents")]
    public async Task<IActionResult> ListDocuments(
        [FromQuery] long? siteId,
        [FromQuery] SiteStockDocumentType? type,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken ct)
    {
        var docs = await _repo.ListDocumentsAsync(siteId, type, fromDate, toDate, ct);
        return Ok(docs);
    }

    [HttpGet("documents/{id}")]
    public async Task<IActionResult> GetDocument(long id, CancellationToken ct)
    {
        var doc = await _repo.GetDocumentAsync(id, ct);
        if (doc is null) return NotFound(new { Message = "Document not found." });
        return Ok(doc);
    }

    [HttpPost("dispatch")]
    public async Task<IActionResult> Dispatch([FromBody] DispatchStockRequest request, CancellationToken ct)
    {
        if (request is null || request.SiteId <= 0 || request.SourceWarehouseId <= 0 || request.Lines == null || request.Lines.Count == 0)
        {
            return BadRequest(new { Message = "Valid site, source warehouse, and items are required." });
        }

        try
        {
            var result = await _repo.DispatchAsync(request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
    }

    [HttpPost("return")]
    public async Task<IActionResult> Return([FromBody] ReturnStockRequest request, CancellationToken ct)
    {
        if (request is null || request.SiteId <= 0 || request.DestinationWarehouseId <= 0 || request.Lines == null || request.Lines.Count == 0)
        {
            return BadRequest(new { Message = "Valid site, destination warehouse, and items are required." });
        }

        try
        {
            var result = await _repo.ReturnAsync(request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
    }

    [HttpPost("usage")]
    public async Task<IActionResult> SubmitUsage([FromBody] SubmitMaterialUsageRequest request, CancellationToken ct)
    {
        if (request is null || request.SiteId <= 0 || request.Lines == null || request.Lines.Count == 0)
        {
            return BadRequest(new { Message = "Valid site and used items are required." });
        }

        try
        {
            var result = await _repo.SubmitUsageAsync(request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
    }

    [HttpGet("usage/pending")]
    public async Task<IActionResult> ListPendingUsage([FromQuery] long? siteId, CancellationToken ct)
    {
        var docs = await _repo.ListPendingUsageAsync(siteId, ct);
        return Ok(docs);
    }

    [HttpPost("usage/{id:long}/approve")]
    public async Task<IActionResult> ApproveUsage(long id, CancellationToken ct)
    {
        try
        {
            var result = await _repo.ApproveUsageAsync(id, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
    }

    [HttpPost("usage/{id:long}/reject")]
    public async Task<IActionResult> RejectUsage(long id, [FromBody] RejectUsageRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _repo.RejectUsageAsync(id, request?.Reason ?? "Rejected by admin", ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
    }
}

public sealed class RejectUsageRequest
{
    public string? Reason { get; set; }
}

