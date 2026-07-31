using Banking.Api.Services;
using Banking.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Controllers;

public class MessageResponse
{
    public string Message { get; set; } = null!;
}

/// <summary>Banking › Banks. The institution, entered once and reused by its accounts.</summary>
[ApiController]
[Authorize]
[Route("api/banks")]
public sealed class BanksController : ControllerBase
{
    private readonly BankService _banks;

    public BanksController(BankService banks) => _banks = banks;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive, CancellationToken ct) =>
        Ok(await _banks.ListBanksAsync(includeInactive, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveBankRequest request, CancellationToken ct) =>
        Respond(await _banks.CreateBankAsync(request, ct), () => StatusCode(StatusCodes.Status201Created));

    [HttpPut("{bankId:long}")]
    public async Task<IActionResult> Update(
        long bankId, [FromBody] SaveBankRequest request, CancellationToken ct) =>
        Respond(await _banks.UpdateBankAsync(bankId, request, ct), NoContent);

    [HttpDelete("{bankId:long}")]
    public async Task<IActionResult> Delete(long bankId, CancellationToken ct) =>
        Respond(await _banks.DeleteBankAsync(bankId, ct), NoContent);

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderRequest request, CancellationToken ct) =>
        Respond(await _banks.ReorderBanksAsync(request, ct), NoContent);

    private IActionResult Respond(SaveBankOutcome outcome, Func<IActionResult> onOk) =>
        BankingResponses.From(this, outcome, onOk);
}

/// <summary>
/// One place that turns an outcome into a response, shared by both controllers
/// so a message written once cannot drift between them.
/// </summary>
public static class BankingResponses
{
    public static IActionResult From(
        ControllerBase controller, SaveBankOutcome outcome, Func<IActionResult> onOk) =>
        outcome switch
        {
            SaveBankOutcome.Ok => onOk(),
            SaveBankOutcome.NotFound => controller.NotFound(),
            SaveBankOutcome.DuplicateCode => Bad(controller, "That bank code is already in use."),
            SaveBankOutcome.DuplicateName => Bad(controller, "That bank name is already in use."),
            SaveBankOutcome.DuplicateAccountNumber => Bad(
                controller, "That account number already exists at this bank."),
            SaveBankOutcome.BankInUse => Bad(
                controller, "Accounts still belong to this bank. Make it inactive instead."),
            SaveBankOutcome.BankRequired => Bad(
                controller, "Choose a bank. Only cash in hand and wallets have none."),
            SaveBankOutcome.OdLimitNotAllowed => Bad(
                controller,
                "Only overdraft, cash credit and credit card accounts can carry a limit."),
            SaveBankOutcome.DefaultAccountLocked => Bad(
                controller,
                "This is the default account. Make another one the default before deactivating it."),
            SaveBankOutcome.LedgerUnavailable => controller.StatusCode(
                StatusCodes.Status409Conflict,
                new MessageResponse
                {
                    Message = "The account was saved but its ledger account could not be created, "
                        + "so it cannot be posted to yet. Use Link ledger to try again.",
                }),
            SaveBankOutcome.InvalidValue => Bad(
                controller, "One of the selected options is not a recognised value."),
            _ => controller.StatusCode(StatusCodes.Status500InternalServerError),
        };

    private static IActionResult Bad(ControllerBase controller, string message) =>
        controller.BadRequest(new MessageResponse { Message = message });
}
