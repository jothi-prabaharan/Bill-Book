using Accounting.Api.Services;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Controllers;

/// <summary>
/// Read-only to users: sub-accounts are provisioned by the master that owns
/// them (a contact, an item, a tax rate), never created by hand.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("accounting")]
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
[AllowAnonymous]
[InternalOnly]
[Route("internal/sub-accounts")]
public sealed class InternalSubAccountsController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalSubAccountsController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    /// <summary>
    /// Creates a master's sub-accounts. The branch comes from the body or from a
    /// forwarded token (<see cref="InternalTenant"/>): before TK-17 only a token
    /// could name it, so a caller with none — seeding a branch's walk-in
    /// customer — provisioned into no branch at all.
    /// </summary>
    [HttpPost("provision")]
    public async Task<IActionResult> Provision(
        [FromBody] ProvisionSubAccountsRequest request, CancellationToken ct)
    {
        switch (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId))
        {
            case InternalTenantOutcome.Missing:
                return BadRequest(new MessageResponse
                {
                    Message = "A customer and an organization are required to provision sub-accounts.",
                });

            case InternalTenantOutcome.Mismatch:
                return Forbid();
        }

        // Resolved after the tenant is set: the context is built from it.
        var subAccounts = _services.GetRequiredService<SubAccountService>();

        ProvisionSubAccountsResult result = await subAccounts.ProvisionAsync(request, ct);

        // A partially provisioned master has an incomplete sub-ledger, so the
        // caller must be able to see it rather than read a bare 200.
        return result.MissingAccounts.Count > 0
            ? StatusCode(StatusCodes.Status409Conflict, new
            {
                created = result.Created,
                missingAccounts = result.MissingAccounts,
                message = "The chart of accounts is missing control accounts, so some "
                    + "sub-accounts were not created.",
            })
            : Ok(new { created = result.Created });
    }

    [HttpPost("deactivate")]
    public async Task<IActionResult> Deactivate(
        [FromQuery] SubAccountReferenceType referenceType,
        [FromQuery] long referenceId,
        CancellationToken ct)
    {
        int deactivated = await _services.GetRequiredService<SubAccountService>()
            .DeactivateAsync(referenceType, referenceId, ct);
        return Ok(new { deactivated });
    }
}
