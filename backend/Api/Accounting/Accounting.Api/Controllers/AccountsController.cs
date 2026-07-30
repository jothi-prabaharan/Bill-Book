using Accounting.Api.Services;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Accounting.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/accounts")]
public sealed class AccountsController : ControllerBase
{
    private readonly AccountService _accounts;

    public AccountsController(AccountService accounts) => _accounts = accounts;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive, CancellationToken ct) =>
        Ok(await _accounts.ListAsync(includeInactive, ct));

    [HttpGet("{accountId:long}")]
    public async Task<IActionResult> Get(long accountId, CancellationToken ct)
    {
        AccountListItem? account = await _accounts.GetAsync(accountId, ct);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveAccountRequest request, CancellationToken ct)
    {
        SaveAccountResult result = await _accounts.CreateAsync(request, ct);
        return Respond(result, () => CreatedAtAction(
            nameof(Get), new { accountId = result.AccountId }, new { accountId = result.AccountId }));
    }

    [HttpPut("{accountId:long}")]
    public async Task<IActionResult> Update(
        long accountId, [FromBody] SaveAccountRequest request, CancellationToken ct)
    {
        SaveAccountResult result = await _accounts.UpdateAsync(accountId, request, ct);
        return Respond(result, NoContent);
    }

    /// <summary>Deactivates. Accounts are never deleted — postings reference them.</summary>
    [HttpDelete("{accountId:long}")]
    public async Task<IActionResult> Deactivate(long accountId, CancellationToken ct)
    {
        DeleteAccountOutcome outcome = await _accounts.DeactivateAsync(accountId, ct);
        return outcome switch
        {
            DeleteAccountOutcome.Ok => NoContent(),
            DeleteAccountOutcome.NotFound => NotFound(),
            DeleteAccountOutcome.SystemAccount => BadRequest(new MessageResponse
            {
                Message = "System accounts cannot be removed. Deactivate is also blocked for them.",
            }),
            DeleteAccountOutcome.HasChildren => Conflict(new MessageResponse
            {
                Message = "This account has child accounts. Move or deactivate those first.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    private IActionResult Respond(SaveAccountResult result, Func<IActionResult> onOk) =>
        result.Outcome switch
        {
            SaveAccountOutcome.Ok => onOk(),
            SaveAccountOutcome.NotFound => NotFound(),
            SaveAccountOutcome.CodeInUse => Conflict(new MessageResponse
            {
                Message = "That account code is already in use.",
            }),
            SaveAccountOutcome.ConfigLocked => BadRequest(new MessageResponse
            {
                Message = "This account has been used, so its type, code and usage flags are fixed. "
                    + "Only the name, active state and posting lock can change.",
            }),
            SaveAccountOutcome.InvalidParent => BadRequest(new MessageResponse
            {
                Message = "The parent account must exist and share the same account type.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}

public class MessageResponse
{
    public string Message { get; set; } = null!;
}
