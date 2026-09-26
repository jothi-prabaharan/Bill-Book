using Accounting.Api.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Controllers;

/// <summary>
/// The payment gateway's server-to-server callback (TK-98).
///
/// <b>Anonymous, because the gateway has no token; the signature is the
/// credential.</b> A body that is not the gateway's, or whose signature does not
/// verify, is refused and records nothing. The tenant comes from the payment's
/// reference, which the gateway carries back, and is set before the database is
/// opened — which is why the service is resolved in the action and the request
/// carries no filter transaction. A replayed callback is answered 200 and does
/// nothing, so a gateway retrying delivery stops retrying.
/// </summary>
[ApiController]
[AllowAnonymous]
[RequireApp(App.All)]
[Route("api/payments/{gateway}/callback")]
public sealed class PaymentCallbacksController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public PaymentCallbacksController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost]
    [NoTransaction]
    public async Task<IActionResult> Receive(string gateway, CancellationToken ct)
    {
        IPaymentGateway configured = _services.GetRequiredService<IPaymentGateway>();
        if (!configured.IsConfigured || !string.Equals(gateway, configured.Kind.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        string body;
        using (var reader = new StreamReader(Request.Body))
        {
            body = await reader.ReadToEndAsync(ct);
        }

        if (configured.ReadCallback(body) is not { } callback
            || !OnlinePaymentService.TryReadReference(callback.Reference, out Guid customerId, out Guid orgId))
        {
            return BadRequest();
        }

        _tenant.CustomerId = customerId;
        _tenant.OrgId = orgId;

        CompletePaymentOutcome outcome = await _services.GetRequiredService<OnlinePaymentService>().CompleteAsync(callback, ct);

        return outcome switch
        {
            CompletePaymentOutcome.Ok or CompletePaymentOutcome.AlreadyRecorded => Ok(),
            CompletePaymentOutcome.NotFound => NotFound(),
            _ => BadRequest(),
        };
    }
}
