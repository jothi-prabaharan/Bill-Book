using Master.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Master.Api.Controllers;

/// <summary>
/// The display formats for the signed-in user's branch.
///
/// <b>Guarded by authentication alone, like the menu.</b> Every other staff
/// route names a module permission, and this one deliberately does not: formats
/// are the shell's data rather than a module's, and every role needs them to
/// render any screen at all. There is no module permission that would work —
/// <c>settings.view</c> is held by Owner, Administrator and Viewer but not by
/// Accountant or Sales, so gating on it would leave a salesperson unable to
/// draw a date. <c>MenuController</c> is the precedent and the same argument:
/// per-user session data, no module authority involved.
///
/// The org is taken from the token inside the service, so there is no id in the
/// route for a caller to substitute.
/// </summary>
[ApiController]
[Route("api/formats")]
[Authorize]
public sealed class FormatsController : ControllerBase
{
    private readonly FormatSettingsService _formats;

    public FormatsController(FormatSettingsService formats) => _formats = formats;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(await _formats.GetAsync(ct));
}
