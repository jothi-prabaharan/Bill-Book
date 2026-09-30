using Customer.Entity.Enums;
using Customer.Entity.Models;
using Customer.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Customer.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("crm")]
[Route("api/email-consents")]
[Route("api/campaigns/consent")]
[RequireAddOn(AddOn.Crm)]
public sealed class EmailConsentsController : ControllerBase
{
    private readonly EmailConsentRepository _repo;

    public EmailConsentsController(EmailConsentRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    [RequirePermission("crm.view")]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] ConsentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var list = await _repo.ListConsentsAsync(search, status, page, pageSize, ct);
        return Ok(list);
    }

    [HttpGet("{email}")]
    [RequirePermission("crm.view")]
    public async Task<IActionResult> Get(string email, CancellationToken ct)
    {
        var consent = await _repo.GetConsentAsync(email, ct);
        return consent is null ? NotFound() : Ok(consent);
    }

    [HttpPost]
    [RequirePermission("crm.edit")]
    public async Task<IActionResult> RecordConsent([FromBody] SaveConsentRequest request, CancellationToken ct)
    {
        var consent = await _repo.RecordConsentAsync(request.Email, request.Status, request.Source, ct);
        return Ok(consent);
    }
}
