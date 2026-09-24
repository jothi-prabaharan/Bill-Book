using Hrm.Api.Services;
using Hrm.Entity.Enums;
using Hrm.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Hrm.Api.Controllers;

/// <summary>
/// The shared employee master (H1, TK-48), for HRMS, Payroll and School. The
/// list masks PAN and Aadhaar always; the detail shows them in full only to a
/// holder of <c>payroll.view</c> or to the employee. There is no delete: an
/// employee who leaves is Exited.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("employee")]
[RequireApp(App.Hrms | App.Payroll | App.School)]
[Route("api/hrm/employees")]
public sealed class EmployeesController : ControllerBase
{
    private readonly EmployeeService _employees;

    public EmployeesController(EmployeeService employees) => _employees = employees;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] long? departmentId,
        [FromQuery] EmployeeStatus? status,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        Ok(await _employees.ListAsync(search, departmentId, status, page, pageSize, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        EmployeeDetail? employee = await _employees.GetAsync(id, ct);
        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveEmployeeRequest request, CancellationToken ct)
    {
        EmployeeResult result = await _employees.CreateAsync(request, ct);
        return result.Outcome == EmployeeOutcome.Ok
            ? CreatedAtAction(nameof(Get), new { id = result.EmployeeId }, new { id = result.EmployeeId, code = result.EmployeeCode })
            : Refused(result.Outcome);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveEmployeeRequest request, CancellationToken ct)
    {
        EmployeeResult result = await _employees.UpdateAsync(id, request, ct);
        return result.Outcome == EmployeeOutcome.Ok ? NoContent() : Refused(result.Outcome);
    }

    private IActionResult Refused(EmployeeOutcome outcome) => outcome switch
    {
        EmployeeOutcome.NotFound => NotFound(),
        EmployeeOutcome.UserAlreadyLinked => Conflict(new HrmMessage("That login is already linked to another employee of this branch.")),
        _ => UnprocessableEntity(new HrmMessage(Sentence(outcome))),
    };

    /// <summary>The sentence for each refusal. Public so the tests can check every outcome has one.</summary>
    public static string Sentence(EmployeeOutcome outcome) => outcome switch
    {
        EmployeeOutcome.InvalidReference => "Choose the department, designation, grade, location, cost centre and manager from this branch.",
        EmployeeOutcome.ManagerCycle => "An employee cannot report, directly or through others, to themselves.",
        EmployeeOutcome.TooYoung => "An employee must be at least 14 years old on the joining date.",
        EmployeeOutcome.NomineeShares => "The nominee shares for each kind of nomination must add up to 100 percent.",
        EmployeeOutcome.PrimaryBank => "Mark exactly one bank account as the one salary is paid into.",
        EmployeeOutcome.DuplicateAddress => "Give at most one current and one permanent address.",
        EmployeeOutcome.ExitDateRequired => "Give the exit date for an employee who has left.",
        EmployeeOutcome.NomineeFamilyMember => "Each nominee must be one of the employee's family members.",
        EmployeeOutcome.InvalidIdentityNumber => "Check the PAN (five letters, four digits, a letter) and the Aadhaar (12 digits).",
        EmployeeOutcome.UserAlreadyLinked => "That login is already linked to another employee of this branch.",
        _ => "The employee could not be saved.",
    };
}
