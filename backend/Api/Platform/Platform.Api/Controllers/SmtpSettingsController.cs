using Microsoft.AspNetCore.Mvc;
using Platform.Api.Services;
using Platform.Entity.Models;
using Shared.Kernel.Interfaces;

namespace Platform.Api.Controllers;

/// <summary>
/// The outbound mail account. Platform admins edit the default row
/// (no customerId); a customer edits its own override.
///
/// The password is write-only in both directions — it is never returned.
/// </summary>
[ApiController]
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

    /// <summary>The platform default mailbox.</summary>
    [HttpGet("default")]
    public async Task<IActionResult> GetDefault(CancellationToken ct)
    {
        SmtpSettingsDto? dto = await _service.GetAsync(null, ct);
        return dto is null ? NoContent() : Ok(dto);
    }

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
    [HttpGet("customers/{customerId:guid}")]
    public async Task<IActionResult> GetForCustomer(Guid customerId, CancellationToken ct)
    {
        SmtpSettingsDto? dto = await _service.GetAsync(customerId, ct);
        return dto is null ? NoContent() : Ok(dto);
    }

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
    [HttpDelete("customers/{customerId:guid}")]
    public async Task<IActionResult> DeleteOverride(Guid customerId, CancellationToken ct) =>
        await _service.DeleteOverrideAsync(customerId, ct) ? NoContent() : NotFound();

    /// <summary>Proves the credentials work before anyone relies on them.</summary>
    [HttpPost("test")]
    public async Task<IActionResult> SendTest(
        [FromQuery] Guid? customerId, [FromBody] SendTestEmailRequest request, CancellationToken ct)
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
