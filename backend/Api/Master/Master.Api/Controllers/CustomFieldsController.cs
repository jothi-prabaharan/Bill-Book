using Master.Api.Services;
using Master.Repository.Repositories;
using Master.Repository.Clients;
using Master.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.CustomFields;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

/// <summary>
/// Settings › Custom fields (TK-110): every kind's fields for the branch, in one
/// list. Reading takes <c>settings.view</c>; creating and saving take
/// <c>settings.create</c> and <c>settings.edit</c>. There is no delete: a field
/// is deactivated, and keeps every value stored under it.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("settings")]
[RequireApp(App.All)]
[Route("api/custom-fields")]
public sealed class CustomFieldsController : ControllerBase
{
    private readonly CustomFieldRepository _fields;

    public CustomFieldsController(CustomFieldRepository fields) => _fields = fields;

    /// <summary>The branch's fields, every kind or one (<c>?kind=Contact</c>), inactive ones included.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? kind, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return Ok(await _fields.ListAsync(null, ct));
        }

        return Enum.TryParse(kind, ignoreCase: true, out CustomFieldEntityKind parsed) && Enum.IsDefined(parsed)
            ? Ok(await _fields.ListAsync(parsed, ct))
            : BadRequest(new MessageResponse { Message = "That is not a kind of record custom fields belong to." });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveCustomFieldRequest request, CancellationToken ct) =>
        Answer(await _fields.SaveAsync(null, request, ct), created: true);

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveCustomFieldRequest request, CancellationToken ct) =>
        Answer(await _fields.SaveAsync(id, request, ct), created: false);

    private IActionResult Answer(CustomFieldResult result, bool created) => result.Outcome switch
    {
        CustomFieldOutcome.Ok => created ? Ok(new { id = result.Id }) : NoContent(),
        CustomFieldOutcome.NotFound => NotFound(),
        CustomFieldOutcome.Duplicate => Conflict(new MessageResponse { Message = result.Detail! }),
        _ => UnprocessableEntity(new MessageResponse { Message = result.Detail ?? "The field was refused." }),
    };
}
