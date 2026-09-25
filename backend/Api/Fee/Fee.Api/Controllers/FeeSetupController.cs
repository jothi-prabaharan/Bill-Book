using Fee.Api.Services;
using Fee.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Fee.Api.Controllers;

/// <summary>
/// Fee heads, structures and concessions (S4, TK-64), and the accounts they may
/// name. Reading takes <c>fee.view</c>, adding <c>fee.create</c>, changing
/// <c>fee.edit</c>; approving a concession also needs <c>fee.approve</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("fee")]
[RequireApp(App.School)]
[Route("api/fee")]
public sealed class FeeSetupController : ControllerBase
{
    private readonly FeeSetupService _setup;
    private readonly ICallerPermissions _caller;

    public FeeSetupController(FeeSetupService setup, ICallerPermissions caller)
    {
        _setup = setup;
        _caller = caller;
    }

    [HttpGet("heads")]
    public async Task<IActionResult> Heads(CancellationToken ct) => Ok(await _setup.HeadsAsync(ct));

    [HttpPost("heads")]
    public async Task<IActionResult> CreateHead([FromBody] SaveFeeHeadRequest request, CancellationToken ct) =>
        this.Answer(await _setup.SaveHeadAsync(null, request, ct));

    [HttpPut("heads/{id:long}")]
    public async Task<IActionResult> UpdateHead(long id, [FromBody] SaveFeeHeadRequest request, CancellationToken ct) =>
        this.Answer(await _setup.SaveHeadAsync(id, request, ct));

    [HttpGet("structures")]
    public async Task<IActionResult> Structures([FromQuery] long? academicYearId, CancellationToken ct) =>
        Ok(await _setup.StructuresAsync(academicYearId, ct));

    [HttpPost("structures")]
    public async Task<IActionResult> CreateStructure([FromBody] SaveFeeStructureRequest request, CancellationToken ct) =>
        this.Answer(await _setup.SaveStructureAsync(null, request, ct));

    [HttpPut("structures/{id:long}")]
    public async Task<IActionResult> UpdateStructure(long id, [FromBody] SaveFeeStructureRequest request, CancellationToken ct) =>
        this.Answer(await _setup.SaveStructureAsync(id, request, ct));

    [HttpGet("concessions")]
    public async Task<IActionResult> Concessions([FromQuery] long? studentId, CancellationToken ct) =>
        Ok(await _setup.ConcessionsAsync(studentId, ct));

    [HttpPost("concessions")]
    public async Task<IActionResult> CreateConcession([FromBody] SaveConcessionRequest request, CancellationToken ct) =>
        this.Answer(await _setup.SaveConcessionAsync(null, request, _caller.Has("fee.approve"), ct));

    [HttpPut("concessions/{id:long}")]
    public async Task<IActionResult> UpdateConcession(long id, [FromBody] SaveConcessionRequest request, CancellationToken ct) =>
        this.Answer(await _setup.SaveConcessionAsync(id, request, _caller.Has("fee.approve"), ct));

    /// <summary>The income and liability accounts a fee head may post to, from Accounting.</summary>
    [HttpGet("postable-accounts")]
    public async Task<IActionResult> PostableAccounts(CancellationToken ct) => Ok(await _setup.PostableAccountsAsync(ct));

    /// <summary>The bank and cash accounts a receipt may land in, from Accounting.</summary>
    [HttpGet("bank-accounts")]
    public async Task<IActionResult> BankAccounts(CancellationToken ct) => Ok(await _setup.BankAccountsAsync(ct));
}
