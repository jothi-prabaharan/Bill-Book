using Accounting.Api.Services;
using Accounting.Repository.Repositories;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Apps;

namespace Accounting.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("accounting")]
[Route("api/accounts")]
[RequireBusinessType(BusinessType.Retail, BusinessType.Pharma, BusinessType.Jewellery, BusinessType.Restaurant, BusinessType.School, BusinessType.Service)]
public sealed class AccountsController : ControllerBase
{
    private readonly AccountRepository _accounts;

    public AccountsController(AccountRepository accounts) => _accounts = accounts;

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
            SaveAccountOutcome.ParentHasTransactions => BadRequest(new MessageResponse
            {
                Message = "An account with existing transactions cannot be assigned as a parent account.",
            }),
            SaveAccountOutcome.CurrencyNotInUse => BadRequest(new MessageResponse
            {
                Message = "This branch does not use that currency. Switch it on under Settings › Currencies first.",
            }),
            SaveAccountOutcome.CurrencyUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new MessageResponse
            {
                Message = "The branch's currencies could not be read just now. Nothing was saved; try again.",
            }),
            SaveAccountOutcome.BankFlagNotAllowed => BadRequest(new MessageResponse
            {
                Message = "Bank accounts are set up under Banking, which sets this flag itself. It cannot be changed here.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}

public class MessageResponse
{
    public string Message { get; set; } = null!;
}
