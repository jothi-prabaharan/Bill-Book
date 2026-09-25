using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Student.Api.Services;
using Student.Entity.Models;

namespace Student.Api.Controllers;

/// <summary>
/// Academic setup (S1, TK-61): school years, classes, sections and subjects.
/// Reading takes <c>sis.view</c>, adding <c>sis.create</c> and changing <c>sis.edit</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("sis")]
[RequireApp(App.School)]
[Route("api/sis")]
public sealed class AcademicController : ControllerBase
{
    private readonly AcademicService _academic;

    public AcademicController(AcademicService academic) => _academic = academic;

    [HttpGet("years")]
    public async Task<IActionResult> Years(CancellationToken ct) => Ok(await _academic.YearsAsync(ct));

    [HttpPost("years")]
    public async Task<IActionResult> CreateYear([FromBody] SaveAcademicYearRequest request, CancellationToken ct) =>
        this.Answer(await _academic.SaveYearAsync(null, request, ct));

    [HttpPut("years/{id:long}")]
    public async Task<IActionResult> UpdateYear(long id, [FromBody] SaveAcademicYearRequest request, CancellationToken ct) =>
        this.Answer(await _academic.SaveYearAsync(id, request, ct));

    [HttpGet("classes")]
    public async Task<IActionResult> Classes(CancellationToken ct) => Ok(await _academic.ClassesAsync(ct));

    [HttpPost("classes")]
    public async Task<IActionResult> CreateClass([FromBody] SaveSchoolClassRequest request, CancellationToken ct) =>
        this.Answer(await _academic.SaveClassAsync(null, request, ct));

    [HttpPut("classes/{id:long}")]
    public async Task<IActionResult> UpdateClass(long id, [FromBody] SaveSchoolClassRequest request, CancellationToken ct) =>
        this.Answer(await _academic.SaveClassAsync(id, request, ct));

    [HttpGet("sections")]
    public async Task<IActionResult> Sections([FromQuery] long? academicYearId, CancellationToken ct) =>
        Ok(await _academic.SectionsAsync(academicYearId, ct));

    [HttpPost("sections")]
    public async Task<IActionResult> CreateSection([FromBody] SaveSectionRequest request, CancellationToken ct) =>
        this.Answer(await _academic.SaveSectionAsync(null, request, ct));

    [HttpPut("sections/{id:long}")]
    public async Task<IActionResult> UpdateSection(long id, [FromBody] SaveSectionRequest request, CancellationToken ct) =>
        this.Answer(await _academic.SaveSectionAsync(id, request, ct));

    [HttpGet("subjects")]
    public async Task<IActionResult> Subjects(CancellationToken ct) => Ok(await _academic.SubjectsAsync(ct));

    [HttpPost("subjects")]
    public async Task<IActionResult> CreateSubject([FromBody] SaveSubjectRequest request, CancellationToken ct) =>
        this.Answer(await _academic.SaveSubjectAsync(null, request, ct));

    [HttpPut("subjects/{id:long}")]
    public async Task<IActionResult> UpdateSubject(long id, [FromBody] SaveSubjectRequest request, CancellationToken ct) =>
        this.Answer(await _academic.SaveSubjectAsync(id, request, ct));
}
