using Accounting.Api.Services;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Controllers;

/// <summary>
/// Where every other service posts to the general ledger.
///
/// Guarded by the shared internal key rather than a token, and the tenant comes
/// from the body: the callers include background workers that hold no user
/// token, and a posting that only works while somebody is signed in would mean
/// costs settling only during office hours.
///
/// The endpoint is deliberately dumb about accounting. It takes a balanced set
/// of legs and a document to file them under; deciding which accounts a sale or
/// a stock issue touches belongs to the service that owns the document, and
/// deciding whether the result is a legal posting belongs to
/// <see cref="LedgerPostingService"/>.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/ledger")]
public sealed class InternalLedgerController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalLedgerController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("postings")]
    public async Task<IActionResult> Post(
        [FromBody] PostLedgerRequest request, CancellationToken ct)
    {
        if (request.CustomerId == Guid.Empty || request.OrgId == Guid.Empty)
        {
            return BadRequest(new MessageResponse
            {
                Message = "A customer and an organization are required to post.",
            });
        }

        // Set before anything resolves a DbContext: the context is built from
        // the tenant, so resolving the service first would bind it to no tenant.
        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        var postings = _services.GetRequiredService<LedgerPostingService>();

        PostLedgerResult result = await postings.PostAsync(request, ct);

        return result.Outcome switch
        {
            PostLedgerOutcome.Ok => Ok(new PostLedgerResponse
            {
                Posted = result.Posted,
                Replaced = result.Replaced,
            }),

            // Transient. A 503 is what tells the caller to leave the posting
            // queued and come back, rather than parking it as a bad request.
            PostLedgerOutcome.BaseCurrencyUnavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new MessageResponse
                {
                    Message = "The branch's base currency could not be read, so nothing was "
                        + "posted. The posting is unchanged and can be retried.",
                }),

            PostLedgerOutcome.AccountMissing or PostLedgerOutcome.SubAccountMissing =>
                StatusCode(StatusCodes.Status409Conflict, new MessageResponse
                {
                    Message = result.Detail ?? "The chart of accounts is incomplete.",
                }),

            _ => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? $"The posting was refused: {result.Outcome}.",
            }),
        };
    }
}
