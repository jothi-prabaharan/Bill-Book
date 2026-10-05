using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Master.Api.Services;
using Master.Repository.Repositories;
using Master.Repository.Clients;
using Master.Entity.Models;
using Shared.Kernel.Internal;
using Shared.Kernel.Apps;
using Master.Entity.Enums;

namespace Master.Api.Controllers;

/// <summary>
/// Platform admin: the customer list with provisioning status, and the
/// actions the platform admin pages (/admin in the web app) need on it — create a customer without going through
/// public self-service signup, and retry one stuck at Provisioning or
/// Failed. Operator only, via platform.view/platform.edit — not scoped to
/// any one customer, since the whole point is seeing across all of them.
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin/customers")]
[RequireBusinessType(AllowStandalone = true)]
public sealed class AdminCustomersController : ControllerBase
{
    private readonly SignupRepository _signup;
    private readonly OrganizationRepository _organizations;
    private readonly DemoCustomerRepository _demos;
    private readonly IConfiguration _config;

    public AdminCustomersController(
        SignupRepository signup, OrganizationRepository organizations, DemoCustomerRepository demos, IConfiguration config)
    {
        _signup = signup;
        _organizations = organizations;
        _demos = demos;
        _config = config;
    }

    /// <summary>
    /// Creates the six demo customers, one per business type, or completes any
    /// a previous run left unseeded (TK-154). Idempotent: a second run adds
    /// only what is missing. Master data only, no transactions.
    /// </summary>
    [RequirePermission("platform.edit")]
    [HttpPost("demo")]
    public async Task<IActionResult> CreateDemos([FromBody] DemoCustomersRequest request, CancellationToken ct)
    {
        string? mailbox = DemoCustomerBootstrapWorker.Mailbox(request.OwnerMailbox, _config);
        if (mailbox is null)
        {
            return BadRequest(new MessageResponse { Message = "Give a mailbox for the demo owners' sign-ins." });
        }

        IReadOnlyList<DemoCustomerResult> results = await _demos.EnsureAllAsync(mailbox, ct);
        return Ok(results.Select(r => new DemoCustomerRow
        {
            BusinessType = r.BusinessType.ToString(),
            CustomerCode = r.CustomerCode,
            Created = r.Created,
            Unseeded = r.Unseeded,
        }));
    }

    /// <summary>Every customer, or only those of one kind with <c>?kind=</c> (TK-152).</summary>
    [RequirePermission("platform.view")]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] CustomerKind? kind, CancellationToken ct) =>
        Ok(await _signup.ListAsync(ct, kind));

    /// <summary>
    /// A customer's branches. OrganizationRepository.ListAsync already does
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
    /// A Standard customer lands on Trial. An operator may instead create an
    /// Internal or Demo customer, which starts on Elite and never lapses
    /// (D-44, TK-152); public signup can make neither.
    /// </summary>
    [RequirePermission("platform.edit")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SignupRequest request, CancellationToken ct)
    {
        request.Kind = CustomerKindRepository.KindToCreate(request.Kind, byOperator: true);
        if (SignupRepository.ScopeRefusal(request.BusinessType, request.AddOns) is string scopeRefusal)
        {
            return BadRequest(new { message = scopeRefusal });
        }

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
