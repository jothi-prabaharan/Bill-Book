using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Sis.Api.Services;
using Sis.Entity.Enums;
using Sis.Entity.Models;

namespace Sis.Api.Controllers;

/// <summary>
/// Students, their guardians and enrolments (S1, TK-61). A student admitted
/// directly is created here, optionally enrolled in the same request.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("sis")]
[RequireApp(App.School)]
[Route("api/sis")]
public sealed class StudentsController : ControllerBase
{
    private readonly StudentService _students;

    public StudentsController(StudentService students) => _students = students;

    [HttpGet("students")]
    public async Task<IActionResult> List(
        [FromQuery] string? search, [FromQuery] StudentStatus? status, [FromQuery] long? sectionId, CancellationToken ct) =>
        Ok(await _students.ListAsync(search, status, sectionId, ct));

    [HttpGet("students/{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        await _students.GetAsync(id, ct) is StudentView view ? Ok(view) : NotFound();

    [HttpPost("students")]
    public async Task<IActionResult> Create([FromBody] SaveStudentRequest request, CancellationToken ct) =>
        this.Answer(await _students.CreateAsync(request, ct));

    [HttpPut("students/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveStudentRequest request, CancellationToken ct) =>
        this.Answer(await _students.UpdateAsync(id, request, ct));

    [HttpPost("enrolments")]
    public async Task<IActionResult> Enrol([FromBody] EnrolRequest request, CancellationToken ct) =>
        this.Answer(await _students.EnrolAsync(request, ct));

    [HttpGet("sections/{sectionId:long}/roll")]
    public async Task<IActionResult> Roll(long sectionId, CancellationToken ct) =>
        Ok(await _students.RollAsync(sectionId, ct));
}
