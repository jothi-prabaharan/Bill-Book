using Accounting.Api.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Shared.Kernel.Persistence;

namespace Accounting.Api.Controllers;

/// <summary>
/// Paying online from the client portal (TK-98): start a payment for chosen
/// invoices or an amount, and read what became of it. The contact is the
/// portal token's. The result the portal shows comes from here, not from the
/// gateway's redirect.
/// </summary>
[ApiController]
[Authorize]
[RequirePortalAccess]
[Route("api/portal/payments")]
[RequireApp(App.RetailErp)]
public sealed class PortalPaymentsController : ControllerBase
{
    private readonly OnlinePaymentService _payments;

    public PortalPaymentsController(OnlinePaymentService payments) => _payments = payments;

    private long ContactId => long.Parse(User.FindFirst(RequirePortalAccessAttribute.ContactClaim)!.Value);

    [HttpPost]
    public async Task<IActionResult> Start([FromBody] StartPaymentRequest request, CancellationToken ct)
    {
        StartPaymentResult result = await _payments.StartAsync(ContactId, request, ct);

        return result.Outcome switch
        {
            StartPaymentOutcome.Ok => Ok(new { onlinePaymentId = result.OnlinePaymentId, checkoutUrl = result.CheckoutUrl }),
            StartPaymentOutcome.Invalid => UnprocessableEntity(new { message = result.Detail }),
            StartPaymentOutcome.NotSetUp => Conflict(new { message = result.Detail }),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = result.Detail }),
        };
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        await _payments.GetAsync(ContactId, id, ct) is { } view ? Ok(view) : NotFound();

    /// <summary>
    /// The sandbox's checkout (D-25): pays or fails the contact's own open
    /// payment through the same verified-callback path a real gateway takes.
    /// Not found when the sandbox is not the gateway.
    /// </summary>
    [HttpPost("{id:long}/sandbox-checkout")]
    [NoTransaction]
    public async Task<IActionResult> SandboxCheckout(
        long id, [FromBody] SandboxCheckoutRequest request, [FromServices] IServiceProvider services, CancellationToken ct)
    {
        if (services.GetService<SandboxPaymentGateway>() is not { } sandbox)
        {
            return NotFound();
        }

        CompletePaymentOutcome outcome = await _payments.SandboxCheckoutAsync(ContactId, id, request.Succeed, sandbox, ct);
        return outcome is CompletePaymentOutcome.NotFound ? NotFound() : NoContent();
    }
}

public sealed class SandboxCheckoutRequest
{
    public bool Succeed { get; set; } = true;
}
