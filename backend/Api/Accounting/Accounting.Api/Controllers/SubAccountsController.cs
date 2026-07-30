using Accounting.Api.Services;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Accounting.Api.Controllers;

/// <summary>
/// Read-only to users: sub-accounts are provisioned by the master that owns
/// them (a contact, an item, a tax rate), never created by hand.
/// </summary>
[ApiController]
[Authorize]
[Route("api/sub-accounts")]
public sealed class SubAccountsController : ControllerBase
{
    private readonly SubAccountService _subAccounts;

    public SubAccountsController(SubAccountService subAccounts) => _subAccounts = subAccounts;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] SubAccountReferenceType? referenceType,
        [FromQuery] long? referenceId,
        CancellationToken ct) =>
        Ok(await _subAccounts.ListAsync(referenceType, referenceId, ct));
}

/// <summary>
/// Internal provisioning API, called by Contacts, Inventory and the tax master
/// when they create or retire a row. Not routed through the public gateway.
/// </summary>
[ApiController]
[Route("internal/sub-accounts")]
public sealed class InternalSubAccountsController : ControllerBase
{
    private readonly SubAccountService _subAccounts;

    public InternalSubAccountsController(SubAccountService subAccounts) => _subAccounts = subAccounts;

    [HttpPost("provision")]
    public async Task<IActionResult> Provision(
        [FromBody] ProvisionSubAccountsRequest request, CancellationToken ct)
    {
        int created = await _subAccounts.ProvisionAsync(request, ct);
        return Ok(new { created });
    }

    [HttpPost("deactivate")]
    public async Task<IActionResult> Deactivate(
        [FromQuery] SubAccountReferenceType referenceType,
        [FromQuery] long referenceId,
        CancellationToken ct)
    {
        int deactivated = await _subAccounts.DeactivateAsync(referenceType, referenceId, ct);
        return Ok(new { deactivated });
    }
}
