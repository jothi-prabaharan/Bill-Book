using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Student.Api.Services;
using Student.Entity.Enums;
using Student.Entity.Models;

namespace Student.Api.Controllers;

/// <summary>
/// Exams and marks (S1, TK-61). Entering marks takes <c>sis.edit</c>, which a
/// teacher holds; opening, publishing and locking an exam take <c>sis.approve</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("sis")]
[RequireApp(App.School)]
[Route("api/sis/exams")]
public sealed class ExamsController : ControllerBase
{
    private readonly ExamService _exams;

    public ExamsController(ExamService exams) => _exams = exams;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] long? academicYearId, CancellationToken ct) =>
        Ok(await _exams.ListAsync(academicYearId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveExamRequest request, CancellationToken ct) =>
        this.Answer(await _exams.SaveAsync(null, request, ct));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveExamRequest request, CancellationToken ct) =>
        this.Answer(await _exams.SaveAsync(id, request, ct));

    [HttpPost("{id:long}/status")]
    [PermissionAction("approve")]
    public async Task<IActionResult> Move(long id, [FromBody] ExamStatusRequest request, CancellationToken ct) =>
        this.Answer(await _exams.MoveAsync(id, request.ExamStatus, ct));

    [HttpGet("{id:long}/marks")]
    public async Task<IActionResult> Sheet(long id, [FromQuery] long examSubjectId, [FromQuery] long sectionId, CancellationToken ct) =>
        await _exams.SheetAsync(examSubjectId, sectionId, ct) is List<MarkRow> rows ? Ok(rows) : NotFound();

    [HttpPut("{id:long}/marks")]
    [PermissionAction("edit")]
    public async Task<IActionResult> SaveMarks(long id, [FromBody] SaveMarksRequest request, CancellationToken ct) =>
        this.Answer(await _exams.SaveMarksAsync(id, request, ct));
}
