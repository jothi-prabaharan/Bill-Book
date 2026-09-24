using Master.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Email;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

/// <summary>
/// The mailbox a customer's mail goes out through, for <c>Notification.Worker</c>
/// (TK-19). The worker may not read <c>mst.SmtpSettings</c> itself (hard rule
/// 8), so it asks here, per message.
///
/// <b>This answers with the decrypted password</b>, which until TK-19 never left
/// this process. It goes only to a caller holding the internal key, over the
/// internal network, and the worker holds it for one send and stores it
/// nowhere; that is the price of mail surviving a restart, and it is paid only
/// where <see cref="EmailDelivery.UseWorker"/> is on.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/smtp")]
public sealed class InternalSmtpController : ControllerBase
{
    private readonly SmtpSettingsService _settings;

    public InternalSmtpController(SmtpSettingsService settings) => _settings = settings;

    /// <summary>
    /// The customer's own active mailbox, or the platform default when it has
    /// none, or 404 when neither is configured. No customer asks for the default.
    /// </summary>
    [HttpGet("resolved")]
    public async Task<IActionResult> Resolved([FromQuery] Guid? customerId, CancellationToken ct)
    {
        ResolvedSmtp? smtp = await _settings.ResolveAsync(customerId, ct);
        return smtp is null ? NotFound() : Ok(smtp);
    }
}
