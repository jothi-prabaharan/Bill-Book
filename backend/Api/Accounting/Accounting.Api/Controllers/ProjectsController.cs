using Accounting.Api.Services;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Accounting.Api.Controllers;

/// <summary>
/// Accounting › Projects (TK-104): the project master, and the ledger rows
/// tagged with each project. Reading takes <c>projects.view</c>; creating and
/// saving take <c>projects.create</c> and <c>projects.edit</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("projects")]
[Route("api/projects")]
[RequireApp(App.RetailErp)]
public sealed class ProjectsController : ControllerBase
{
    private readonly ProjectService _projects;

    public ProjectsController(ProjectService projects) => _projects = projects;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] long? contactId, CancellationToken ct) =>
        Ok(await _projects.ListAsync(status, contactId, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        await _projects.GetAsync(id, ct) is { } project ? Ok(project) : NotFound();

    /// <summary>The ledger rows tagged with the project: what it earned and cost, row by row.</summary>
    [HttpGet("{id:long}/ledger")]
    public async Task<IActionResult> Ledger(long id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        await _projects.LedgerAsync(id, from, to, ct) is { } rows ? Ok(rows) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveProjectRequest request, CancellationToken ct)
    {
        SaveProjectResult result = await _projects.SaveAsync(null, request, ct);
        return result.Outcome == SaveProjectOutcome.Ok
            ? CreatedAtAction(nameof(Get), new { id = result.ProjectId }, new { projectId = result.ProjectId })
            : Respond(result);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveProjectRequest request, CancellationToken ct)
    {
        SaveProjectResult result = await _projects.SaveAsync(id, request, ct);
        return result.Outcome == SaveProjectOutcome.Ok ? NoContent() : Respond(result);
    }

    private IActionResult Respond(SaveProjectResult result) => result.Outcome switch
    {
        SaveProjectOutcome.NotFound => NotFound(),
        SaveProjectOutcome.SeriesMissing => Conflict(new MessageResponse { Message = result.Detail ?? "The project series is missing." }),
        _ => UnprocessableEntity(new MessageResponse { Message = result.Detail ?? "The project could not be saved." }),
    };
}
