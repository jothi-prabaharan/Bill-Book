using Shared.Kernel.Apps;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Master.Entity.Models;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

/// <summary>
/// Internal send endpoint. Other services (Identity, for invites and OTPs) post
/// here rather than reading SMTP credentials themselves — the decrypted password
/// stays inside Platform. Must not be routed through the public gateway.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/notifications")]
public sealed class InternalNotificationsController : ControllerBase
{
    private readonly IEmailSender _email;
    private readonly ILogger<InternalNotificationsController> _logger;

    public InternalNotificationsController(
        IEmailSender email, ILogger<InternalNotificationsController> logger)
    {
        _email = email;
        _logger = logger;
    }

    /// <summary>What most mail servers accept in one message, attachments together.</summary>
    public const int MaxAttachmentBytes = 10 * 1024 * 1024;

    [HttpPost("email")]
    public async Task<IActionResult> SendEmail([FromBody] SendEmailRequest request, CancellationToken ct)
    {
        if (request.Attachments.Sum(a => (long)a.Content.Length) > MaxAttachmentBytes)
        {
            return BadRequest(new MessageResponse { Message = "The attachments are larger than 10 MB together." });
        }

        try
        {
            await _email.SendAsync(new EmailMessage
            {
                ToEmail = request.ToEmail,
                ToName = request.ToName,
                Subject = request.Subject,
                HtmlBody = request.HtmlBody,
                TextBody = request.TextBody,
                CustomerId = request.CustomerId,
                Attachments =
                [
                    .. request.Attachments.Select(a => new EmailAttachment
                    {
                        FileName = a.FileName,
                        ContentType = a.ContentType,
                        Content = a.Content,
                    }),
                ],
            }, ct);
            return Accepted();
        }
        catch (Exception ex)
        {
            // Log without the body — it carries invite links and OTP codes.
            _logger.LogError(ex, "Failed to send '{Subject}' to {ToEmail}", request.Subject, request.ToEmail);
            return StatusCode(StatusCodes.Status502BadGateway, new MessageResponse
            {
                Message = "The mail server rejected the message.",
            });
        }
    }
}
