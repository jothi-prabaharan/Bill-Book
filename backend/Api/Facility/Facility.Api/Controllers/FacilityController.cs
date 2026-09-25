using Facility.Api.Services;
using Facility.Entity.Enums;
using Facility.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Facility.Api.Controllers;

/// <summary>
/// Buildings, spaces and assets (S5, TK-65). Reading takes <c>facility.view</c>,
/// adding <c>facility.create</c>, changing and deactivating <c>facility.edit</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("facility")]
[RequireApp(App.School)]
[Route("api/facility")]
public sealed class FacilityController : ControllerBase
{
    private readonly FacilityService _facility;

    public FacilityController(FacilityService facility) => _facility = facility;

    [HttpGet("buildings")]
    public async Task<IActionResult> Buildings(CancellationToken ct) => Ok(await _facility.BuildingsAsync(ct));

    [HttpPost("buildings")]
    public async Task<IActionResult> CreateBuilding([FromBody] SaveBuildingRequest request, CancellationToken ct) =>
        Answer(await _facility.SaveBuildingAsync(null, request, ct));

    [HttpPut("buildings/{id:long}")]
    public async Task<IActionResult> UpdateBuilding(long id, [FromBody] SaveBuildingRequest request, CancellationToken ct) =>
        Answer(await _facility.SaveBuildingAsync(id, request, ct));

    [HttpGet("spaces")]
    public async Task<IActionResult> Spaces([FromQuery] long? buildingId, CancellationToken ct) => Ok(await _facility.SpacesAsync(buildingId, ct));

    [HttpPost("spaces")]
    public async Task<IActionResult> CreateSpace([FromBody] SaveSpaceRequest request, CancellationToken ct) =>
        Answer(await _facility.SaveSpaceAsync(null, request, ct));

    [HttpPut("spaces/{id:long}")]
    public async Task<IActionResult> UpdateSpace(long id, [FromBody] SaveSpaceRequest request, CancellationToken ct) =>
        Answer(await _facility.SaveSpaceAsync(id, request, ct));

    [HttpGet("assets")]
    public async Task<IActionResult> Assets([FromQuery] long? spaceId, [FromQuery] AssetStatus? status, [FromQuery] string? search, CancellationToken ct) =>
        Ok(await _facility.AssetsAsync(spaceId, status, string.IsNullOrWhiteSpace(search) ? null : search.Trim(), ct));

    [HttpPost("assets")]
    public async Task<IActionResult> CreateAsset([FromBody] SaveAssetRequest request, CancellationToken ct) =>
        Answer(await _facility.SaveAssetAsync(null, request, ct));

    [HttpPut("assets/{id:long}")]
    public async Task<IActionResult> UpdateAsset(long id, [FromBody] SaveAssetRequest request, CancellationToken ct) =>
        Answer(await _facility.SaveAssetAsync(id, request, ct));

    // A row outside the caller's branch is not found: the query filter and RLS hide it.
    private IActionResult Answer(FacilityResult result) => result.Outcome switch
    {
        FacilityOutcome.Ok => Ok(new { id = result.Id }),
        FacilityOutcome.NotFound => NotFound(),
        FacilityOutcome.Duplicate => Conflict(new FacilityMessage(result.Detail!)),
        _ => UnprocessableEntity(new FacilityMessage(result.Detail ?? "That change is not allowed.")),
    };
}
