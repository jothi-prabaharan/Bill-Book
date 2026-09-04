using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Master.Api.Services;
using Master.Entity.Models;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

/// <summary>
/// The outbound mail account. Platform admins edit the default row
/// (no customerId); a customer edits its own override.
///
/// The password is write-only in both directions — it is never returned.
/// </summary>
[ApiController]
[Authorize]
[Route("api/smtp-settings")]
public sealed class SmtpSettingsController : ControllerBase
{
    private readonly SmtpSettingsService _service;

    /// <summary>
    /// The concrete SMTP sender, not the queued one: a test must report the real
    /// failure to the admin debugging their credentials, so it sends inline.
    /// </summary>
    private readonly SmtpEmailSender _email;

    public SmtpSettingsController(SmtpSettingsService service, SmtpEmailSender email)
    {
        _service = service;
        _email = email;
    }

    /// <summary>
    /// The platform default mailbox. Operator-only: it belongs to nobody's
    /// account, so no tenant claim can protect it and a permission has to.
    /// </summary>
    [RequirePermission("platform.view")]
    [HttpGet("default")]
    public async Task<IActionResult> GetDefault(CancellationToken ct)
    {
        SmtpSettingsDto? dto = await _service.GetAsync(null, ct);
        return dto is null ? NoContent() : Ok(dto);
    }

    [RequirePermission("platform.edit")]
    [HttpPut("default")]
    public async Task<IActionResult> SaveDefault(
        [FromBody] SaveSmtpSettingsRequest request, CancellationToken ct)
    {
        try
        {
            Guid id = await _service.SaveAsync(null, request, ct);
            return Ok(new { smtpSettingsId = id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse { Message = ex.Message });
        }
    }

    /// <summary>A customer's own mailbox, falling back to the default (flagged inherited).</summary>
    [CustomerRouteMustMatchToken]
    [RequirePermission("settings.view")]
    [HttpGet("customers/{customerId:guid}")]
    public async Task<IActionResult> GetForCustomer(Guid customerId, CancellationToken ct)
    {
        SmtpSettingsDto? dto = await _service.GetAsync(customerId, ct);
        return dto is null ? NoContent() : Ok(dto);
    }

    [CustomerRouteMustMatchToken]
    [RequirePermission("settings.edit")]
    [HttpPut("customers/{customerId:guid}")]
    public async Task<IActionResult> SaveForCustomer(
        Guid customerId, [FromBody] SaveSmtpSettingsRequest request, CancellationToken ct)
    {
        try
        {
            Guid id = await _service.SaveAsync(customerId, request, ct);
            return Ok(new { smtpSettingsId = id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse { Message = ex.Message });
        }
    }

    /// <summary>Drops the override so the customer sends from the platform mailbox again.</summary>
    [CustomerRouteMustMatchToken]
    [RequirePermission("settings.edit")]
    [HttpDelete("customers/{customerId:guid}")]
    public async Task<IActionResult> DeleteOverride(Guid customerId, CancellationToken ct) =>
        await _service.DeleteOverrideAsync(customerId, ct) ? NoContent() : NotFound();

    /// <summary>
    /// Proves the platform's own credentials work, before anyone relies on them.
    ///
    /// <b>Two routes rather than one with a query parameter.</b> It was one, and
    /// the authority was decided in the method body — <c>MaySendAs</c> read the
    /// customer id off the query string and compared it to the token. That check
    /// was correct and invisible: a controller guarded inside its method looks,
    /// to a reader of attributes and to a test reflecting over them, exactly like
    /// one guarded nowhere. It also asked only "is this your account", never "may
    /// you send as it", so any signed-in user of a customer could send through
    /// that customer's mail account and spend its reputation.
    ///
    /// Split, each half states its own authority where it can be seen and
    /// asserted: the platform mailbox belongs to the operator, a customer's
    /// mailbox to somebody who may edit that customer's settings.
    /// </summary>
    [RequirePermission("platform.edit")]
    [HttpPost("default/test")]
    public Task<IActionResult> SendTestFromDefault(
        [FromBody] SendTestEmailRequest request, CancellationToken ct) =>
        SendAsync(null, request, ct);

    /// <summary>Proves a customer's own credentials work.</summary>
    [CustomerRouteMustMatchToken]
    [RequirePermission("settings.edit")]
    [HttpPost("customers/{customerId:guid}/test")]
    public Task<IActionResult> SendTestForCustomer(
        Guid customerId, [FromBody] SendTestEmailRequest request, CancellationToken ct) =>
        SendAsync(customerId, request, ct);

    [NonAction]
    private async Task<IActionResult> SendAsync(
        Guid? customerId, SendTestEmailRequest request, CancellationToken ct)
    {
        try
        {
            await _email.SendAsync(new EmailMessage
            {
                ToEmail = request.ToEmail,
                Subject = "Bill-Book test email",
                HtmlBody = "<p>Your SMTP settings are working.</p>",
                TextBody = "Your SMTP settings are working.",
                CustomerId = customerId,
            }, ct);

            return Ok(new MessageResponse { Message = $"Test email sent to {request.ToEmail}." });
        }
        catch (Exception ex)
        {
            // The reason matters here — the admin is debugging their own credentials.
            return BadRequest(new MessageResponse { Message = $"Send failed: {ex.Message}" });
        }
    }
}
