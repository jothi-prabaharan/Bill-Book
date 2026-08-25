using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Master.Api.Services;
using Master.Entity.Models;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

/// <summary>
/// Platform admin: the customer list with provisioning status, and the
/// actions apps/admin needs on it — create a customer without going through
/// public self-service signup, and retry one stuck at Provisioning or
/// Failed. Operator only, via platform.view/platform.edit — not scoped to
/// any one customer, since the whole point is seeing across all of them.
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin/customers")]
public sealed class AdminCustomersController : ControllerBase
{
    private readonly SignupService _signup;
    private readonly OrganizationService _organizations;

    public AdminCustomersController(SignupService signup, OrganizationService organizations)
    {
        _signup = signup;
        _organizations = organizations;
    }

    [RequirePermission("platform.view")]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _signup.ListAsync(ct));

    /// <summary>
    /// A customer's branches. OrganizationService.ListAsync already does
    /// exactly this — it is what the customer's own Settings screen calls —
    /// scoped here to the route's customerId instead of the caller's own,
    /// since a platform operator is never signed in to any one customer.
    /// </summary>
    [RequirePermission("platform.view")]
    [HttpGet("{customerId:guid}/organizations")]
    public async Task<IActionResult> Organizations(Guid customerId, CancellationToken ct) =>
        Ok(await _organizations.ListAsync(customerId, ct));

    /// <summary>
    /// Admin-initiated provisioning — the same SignupAsync the public trial
    /// signup form calls, so the two can never seed a customer differently.
    /// Still lands on Trial; picking a paid plan tier at creation is a
    /// follow-up, not something this task needed.
    /// </summary>
    [RequirePermission("platform.edit")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SignupRequest request, CancellationToken ct)
    {
        if (await _signup.EmailExistsAsync(request.Email, ct))
        {
            return BadRequest(new MessageResponse
            {
                Message = "An account with this email already exists.",
            });
        }

        SignupResponse response = await _signup.SignupAsync(request, ct);
        return AcceptedAtAction(nameof(List), response);
    }

    /// <summary>
    /// Retries seeding for a customer whose signup did not finish. Every
    /// seed is idempotent, so this is safe to call repeatedly.
    /// </summary>
    [RequirePermission("platform.edit")]
    [HttpPost("{customerId:guid}/retry-provisioning")]
    public async Task<IActionResult> RetryProvisioning(Guid customerId, CancellationToken ct)
    {
        RetryProvisioningResult result = await _signup.RetryProvisioningAsync(customerId, ct);

        return result.Outcome switch
        {
            RetryProvisioningOutcome.Ok => NoContent(),
            RetryProvisioningOutcome.NotFound => NotFound(),
            RetryProvisioningOutcome.Failed => Accepted(new MessageResponse
            {
                Message = "Still could not seed: " + string.Join(", ", result.UnseededServices)
                    + ". The customer stays unusable until this is retried again.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}
