using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Services;
using Platform.Entity.Models;
using Shared.Kernel.Internal;

namespace Platform.Api.Controllers;

/// <summary>
/// Branches under the signed-in head office.
///
/// Every action reads the customer from the caller's own token and never from
/// the route. <c>plt</c> is the master database and holds every customer's rows,
/// so there is no row-level security to fall back on — the claim is the whole
/// boundary, and taking an id from the URL instead would hand any caller anyone
/// else's branches.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("settings")]
[Route("api/organizations")]
public sealed class OrganizationsController : ControllerBase
{
    private readonly OrganizationService _organizations;

    public OrganizationsController(OrganizationService organizations) =>
        _organizations = organizations;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Caller() is Guid customerId
            ? Ok(await _organizations.ListAsync(customerId, ct))
            : Forbid();

    /// <summary>
    /// The branch the caller is signed in to, from the token rather than the
    /// URL. The Organization settings screen edits "this branch" and has no id
    /// to pass — and taking one from the caller would let them edit a branch
    /// they are not in.
    ///
    /// Declared before the <c>{orgId:guid}</c> route below; "current" is not a
    /// Guid so the constraint keeps them apart either way.
    /// </summary>
    [HttpGet("current")]
    public async Task<IActionResult> Current(CancellationToken ct)
    {
        if (Caller() is not Guid customerId || CallerOrg() is not Guid orgId)
        {
            return Forbid();
        }

        OrganizationListItem? organization =
            await _organizations.GetAsync(customerId, orgId, ct);

        return organization is null ? NotFound() : Ok(organization);
    }

    /// <summary>Updates the branch the caller is signed in to.</summary>
    [HttpPut("current")]
    public async Task<IActionResult> UpdateCurrent(
        [FromBody] SaveOrganizationRequest request, CancellationToken ct)
    {
        if (Caller() is not Guid customerId || CallerOrg() is not Guid orgId)
        {
            return Forbid();
        }

        SaveOrganizationResult result =
            await _organizations.UpdateAsync(customerId, orgId, request, ct);

        return result.Outcome == SaveOrganizationOutcome.Ok
            ? NoContent()
            : Respond(result.Outcome);
    }

    [HttpGet("{orgId:guid}")]
    public async Task<IActionResult> Get(Guid orgId, CancellationToken ct)
    {
        if (Caller() is not Guid customerId)
        {
            return Forbid();
        }

        OrganizationListItem? organization =
            await _organizations.GetAsync(customerId, orgId, ct);

        return organization is null ? NotFound() : Ok(organization);
    }

    /// <summary>
    /// Creates a branch and seeds its books. Returns 202 when the row was
    /// written but a service could not be reached — the branch exists, is not
    /// yet usable, and the seeding can be retried against it.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SaveOrganizationRequest request, CancellationToken ct)
    {
        if (Caller() is not Guid customerId)
        {
            return Forbid();
        }

        SaveOrganizationResult result = await _organizations.CreateAsync(customerId, request, ct);

        return result.Outcome switch
        {
            SaveOrganizationOutcome.Ok =>
                CreatedAtAction(nameof(Get), new { orgId = result.OrgId }, result),

            // Created, on the branch's own trial. A 201 with the reason on it,
            // not an error — the branch exists, is seeded and is usable today.
            SaveOrganizationOutcome.CreatedOnTrial =>
                CreatedAtAction(nameof(Get), new { orgId = result.OrgId }, result),

            SaveOrganizationOutcome.SeedingFailed => Accepted(new MessageResponse
            {
                Message = "The branch was created but its books could not be set up: "
                    + string.Join(", ", result.UnseededServices)
                    + ". It stays unusable until this is retried, rather than opening with no "
                    + "chart of accounts behind it.",
            }),

            _ => Respond(result.Outcome),
        };
    }

    [HttpPut("{orgId:guid}")]
    public async Task<IActionResult> Update(
        Guid orgId, [FromBody] SaveOrganizationRequest request, CancellationToken ct)
    {
        if (Caller() is not Guid customerId)
        {
            return Forbid();
        }

        SaveOrganizationResult result =
            await _organizations.UpdateAsync(customerId, orgId, request, ct);

        return result.Outcome == SaveOrganizationOutcome.Ok
            ? NoContent()
            : Respond(result.Outcome);
    }

    /// <summary>Retries the seeding for a branch that was created but not finished.</summary>
    [HttpPost("{orgId:guid}/retry-seeding")]
    public async Task<IActionResult> RetrySeeding(Guid orgId, CancellationToken ct)
    {
        if (Caller() is not Guid customerId)
        {
            return Forbid();
        }

        SaveOrganizationResult result =
            await _organizations.RetrySeedingAsync(customerId, orgId, ct);

        return result.Outcome == SaveOrganizationOutcome.Ok
            ? NoContent()
            : Respond(result.Outcome);
    }

    [HttpDelete("{orgId:guid}")]
    public async Task<IActionResult> Suspend(Guid orgId, CancellationToken ct)
    {
        if (Caller() is not Guid customerId)
        {
            return Forbid();
        }

        SaveOrganizationOutcome outcome =
            await _organizations.SuspendAsync(customerId, orgId, ct);

        return outcome == SaveOrganizationOutcome.Ok ? NoContent() : Respond(outcome);
    }

    /// <summary>
    /// The head office on the caller's token. A token without one is the
    /// pre-auth token, issued before an organization has been chosen, and it
    /// owns nothing.
    /// </summary>
    /// <summary>The branch on the caller's token — which branch they are working in.</summary>
    private Guid? CallerOrg() =>
        Guid.TryParse(User.FindFirstValue("org_id"), out Guid orgId) ? orgId : null;

    private Guid? Caller() =>
        Guid.TryParse(User.FindFirstValue("customer_id"), out Guid customerId)
            ? customerId
            : null;

    private IActionResult Respond(SaveOrganizationOutcome outcome) =>
        outcome switch
        {
            SaveOrganizationOutcome.NotFound => NotFound(),
            SaveOrganizationOutcome.DuplicateCode => Conflict(new MessageResponse
            {
                Message = "Another branch already uses that code.",
            }),
            SaveOrganizationOutcome.DuplicateName => Conflict(new MessageResponse
            {
                Message = "Another branch already uses that name.",
            }),
            SaveOrganizationOutcome.GstinStateMismatch => BadRequest(new MessageResponse
            {
                Message = "The GSTIN's first two digits must match the branch's state.",
            }),
            // Kept for the update path and for any older client still reading it.
            // Create no longer returns this: a branch beyond the entitlement is
            // created on a trial rather than refused.
            SaveOrganizationOutcome.LicenceLimitReached => BadRequest(new MessageResponse
            {
                Message = "Your licence does not allow another branch. Upgrade the plan to add "
                    + "more.",
            }),
            SaveOrganizationOutcome.DatabaseNotReady => BadRequest(new MessageResponse
            {
                Message = "Your account is still being set up. Try again in a moment.",
            }),
            SaveOrganizationOutcome.FirstOrganizationLocked => BadRequest(new MessageResponse
            {
                Message = "The first branch cannot be suspended — the account would have nowhere "
                    + "to sign in to.",
            }),
            SaveOrganizationOutcome.BaseCurrencyLocked => BadRequest(new MessageResponse
            {
                Message = "The base currency is fixed once a branch exists. Every amount already "
                    + "posted was converted to it.",
            }),
            SaveOrganizationOutcome.InvalidValue => BadRequest(new MessageResponse
            {
                Message = "One of the selected options is not a recognised value.",
            }),
            SaveOrganizationOutcome.CreatedOnTrial => NoContent(),

            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}
