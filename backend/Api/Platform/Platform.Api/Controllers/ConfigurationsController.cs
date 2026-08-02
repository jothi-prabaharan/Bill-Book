using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Services;
using Shared.Kernel.Internal;

namespace Platform.Api.Controllers;

/// <summary>
/// Configuration values per organization. Keys are seed data — this endpoint
/// edits values and clears overrides, but never adds or deletes keys.
/// </summary>
[ApiController]
[Authorize]
[OrgRouteMustMatchToken]
[Route("api/organizations/{orgId:guid}/configurations")]
public sealed class ConfigurationsController : ControllerBase
{
    private readonly ConfigurationService _config;

    public ConfigurationsController(ConfigurationService config) => _config = config;

    [HttpGet]
    public async Task<IActionResult> List(Guid orgId, CancellationToken ct) =>
        Ok(await _config.ListAsync(orgId, ct));

    [HttpPut("{code}")]
    public async Task<IActionResult> Set(
        Guid orgId, string code, [FromBody] Entity.Models.SetConfigurationRequest request, CancellationToken ct)
    {
        bool ok = await _config.SetAsync(orgId, code, request.Value, ct);
        return ok
            ? NoContent()
            : NotFound(new MessageResponse { Message = $"Unknown configuration key '{code}'." });
    }

    /// <summary>Clears the org override so the key falls back to the shipped default.</summary>
    [HttpDelete("{code}")]
    public async Task<IActionResult> Reset(Guid orgId, string code, CancellationToken ct)
    {
        bool ok = await _config.ResetAsync(orgId, code, ct);
        return ok ? NoContent() : NotFound();
    }
}
