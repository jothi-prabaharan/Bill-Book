using Contacts.Api.Services;
using Contacts.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Contacts.Api.Controllers;

/// <summary>
/// The contact person role master. Served to a popup on the contact list rather
/// than a page of its own — it exists so somebody filling in a contact can add a
/// missing role without abandoning the form.
/// </summary>
[ApiController]
[Authorize]
[Route("api/contact-person-roles")]
public sealed class ContactPersonRolesController : ControllerBase
{
    private readonly ContactPersonRoleService _roles;

    public ContactPersonRolesController(ContactPersonRoleService roles) => _roles = roles;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive, CancellationToken ct) =>
        Ok(await _roles.ListAsync(includeInactive, ct));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SaveContactPersonRoleRequest request, CancellationToken ct) =>
        Respond(await _roles.CreateAsync(request, ct), () => StatusCode(StatusCodes.Status201Created));

    [HttpPut("{roleId:long}")]
    public async Task<IActionResult> Update(
        long roleId, [FromBody] SaveContactPersonRoleRequest request, CancellationToken ct) =>
        Respond(await _roles.UpdateAsync(roleId, request, ct), NoContent);

    [HttpPut("{roleId:long}/default")]
    public async Task<IActionResult> SetDefault(long roleId, CancellationToken ct) =>
        Respond(await _roles.SetDefaultAsync(roleId, ct), NoContent);

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderRequest request, CancellationToken ct) =>
        Respond(await _roles.ReorderAsync(request, ct), NoContent);

    [HttpDelete("{roleId:long}")]
    public async Task<IActionResult> Delete(long roleId, CancellationToken ct) =>
        Respond(await _roles.DeleteAsync(roleId, ct), NoContent);

    private IActionResult Respond(SaveRoleOutcome outcome, Func<IActionResult> onOk) =>
        outcome switch
        {
            SaveRoleOutcome.Ok => onOk(),
            SaveRoleOutcome.NotFound => NotFound(),
            SaveRoleOutcome.DuplicateName => BadRequest(new MessageResponse
            {
                Message = "Another role already uses that name.",
            }),
            SaveRoleOutcome.SystemRoleUndeletable => BadRequest(new MessageResponse
            {
                Message = "A built-in role cannot be deleted. Rename it, or make it inactive.",
            }),
            SaveRoleOutcome.RoleInUse => BadRequest(new MessageResponse
            {
                Message = "Contact people still hold this role. Make it inactive instead — "
                    + "existing contacts keep it so their history still reads correctly.",
            }),
            SaveRoleOutcome.DefaultRoleMustStayActive => BadRequest(new MessageResponse
            {
                Message = "This is the default role. Make another role the default first, "
                    + "or new contact people would open with an inactive one selected.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}
