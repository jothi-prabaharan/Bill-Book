using Contacts.Api.Services;
using Contacts.Entity.Models;
using Contacts.Entity.TableEntities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;

namespace Contacts.Api.Controllers;

/// <summary>
/// Files held against a contact, and the expiring-licence report.
///
/// On its own controller because files arrive as multipart rather than JSON, so
/// they cannot ride along with the contact aggregate.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("contacts")]
[Route("api/contacts")]
public sealed class ContactAttachmentsController : ControllerBase
{
    private readonly ContactAttachmentService _attachments;

    public ContactAttachmentsController(ContactAttachmentService attachments) =>
        _attachments = attachments;

    [HttpGet("{contactId:long}/attachments")]
    public async Task<IActionResult> List(long contactId, CancellationToken ct) =>
        Ok(await _attachments.ListAsync(contactId, ct));

    /// <summary>
    /// Uploads one file. The size limit is enforced by the service as well as by
    /// the framework, because the framework's limit is a transport concern and
    /// this one is a product decision.
    /// </summary>
    [HttpPost("{contactId:long}/attachments")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        long contactId,
        [FromForm] UploadAttachmentRequest request,
        IFormFile? file,
        CancellationToken ct)
    {
        if (file is null)
        {
            return BadRequest(new MessageResponse { Message = "Choose a file to upload." });
        }

        await using Stream content = file.OpenReadStream();

        (AttachmentOutcome outcome, ContactAttachmentModel? attachment) = await _attachments.UploadAsync(
            contactId,
            request,
            file.FileName,
            file.ContentType,
            file.Length,
            content,
            ct);

        return Respond(outcome, () => Ok(attachment));
    }

    /// <summary>
    /// Streams the file back. The storage key is read from the row rather than
    /// taken from the caller, and the row is org-scoped by the global query
    /// filter — so an id belonging to another organization is simply not found.
    /// </summary>
    [HttpGet("attachments/{attachmentId:long}/download")]
    public async Task<IActionResult> Download(long attachmentId, CancellationToken ct)
    {
        (AttachmentOutcome outcome, Stream? content, ContactAttachment? row) =
            await _attachments.OpenAsync(attachmentId, ct);

        if (outcome != AttachmentOutcome.Ok || content is null || row is null)
        {
            return NotFound();
        }

        // Always as an attachment, never inline: a stored PDF or image should
        // download, not render in the origin the app is served from.
        return File(content, row.ContentType, row.FileName);
    }

    [HttpDelete("attachments/{attachmentId:long}")]
    public async Task<IActionResult> Delete(long attachmentId, CancellationToken ct) =>
        Respond(await _attachments.DeleteAsync(attachmentId, ct), NoContent);

    /// <summary>
    /// Licences lapsing within <paramref name="withinDays"/>. Negative days mean
    /// already expired, which is what the screen leads with.
    /// </summary>
    [HttpGet("licences/expiring")]
    public async Task<IActionResult> ExpiringLicences(
        [FromQuery] int withinDays = 30, CancellationToken ct = default) =>
        Ok(await _attachments.ExpiringLicencesAsync(withinDays, ct));

    private IActionResult Respond(AttachmentOutcome outcome, Func<IActionResult> onOk) =>
        outcome switch
        {
            AttachmentOutcome.Ok => onOk(),
            AttachmentOutcome.NotFound => NotFound(),
            AttachmentOutcome.ContactNotFound => NotFound(),
            AttachmentOutcome.NoFile => BadRequest(new MessageResponse
            {
                Message = "Choose a file to upload.",
            }),
            AttachmentOutcome.TooLarge => BadRequest(new MessageResponse
            {
                Message = "That file is larger than the upload limit.",
            }),
            AttachmentOutcome.UnsupportedType => BadRequest(new MessageResponse
            {
                Message = "Only PDF and image files can be attached to a contact.",
            }),
            AttachmentOutcome.InvalidValue => BadRequest(new MessageResponse
            {
                Message = "That document type is not recognised.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}
