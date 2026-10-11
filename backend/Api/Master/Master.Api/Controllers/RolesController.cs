using Master.Api.Services;
using Master.Repository.Repositories;
using Master.Repository.Clients;
using Master.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("settings")]
[Route("api/roles")]
[RequireBusinessType(AllowStandalone = true)]
public sealed class RolesController : ControllerBase
{
    private readonly RoleRepository _roles;
    private readonly ICurrentUser _currentUser;

    public RolesController(RoleRepository roles, ICurrentUser currentUser)
    {
        _roles = roles;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (_currentUser.CustomerId is not Guid customerId)
        {
            return Forbid();
        }

        return Ok(await _roles.ListAsync(customerId, _currentUser.OrgId, ct));
    }

    /// <summary>
    /// The permission matrix for the checkbox grid, grouped by module: the
    /// permissions the current branch's business type and add-ons allow (D-50).
    /// </summary>
    [HttpGet("permissions")]
    public async Task<IActionResult> Permissions(CancellationToken ct) =>
        // platform.* is operator-only, so it never reaches a tenant's matrix.
        Ok(await _roles.PermissionMatrixAsync(
            includePlatform: false, ScopeClaims.BusinessTypeOf(User), ScopeClaims.AddOnsOf(User), ct));

    [HttpGet("{roleId:int}")]
    public async Task<IActionResult> Get(int roleId, CancellationToken ct)
    {
        if (_currentUser.CustomerId is not Guid customerId)
        {
            return Forbid();
        }

        RoleDetail? role = await _roles.GetAsync(customerId, roleId, _currentUser.OrgId, ct);
        return role is null ? NotFound() : Ok(role);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveRoleRequest request, CancellationToken ct)
    {
        if (_currentUser.CustomerId is not Guid customerId)
        {
            return Forbid();
        }

        (SaveRoleResult result, int roleId) = await _roles.CreateAsync(customerId, request, ct);
        return result == SaveRoleResult.Ok
            ? CreatedAtAction(nameof(Get), new { roleId }, new { roleId })
            : Refused(result);
    }

    [HttpPut("{roleId:int}")]
    public async Task<IActionResult> Update(
        int roleId, [FromBody] SaveRoleRequest request, CancellationToken ct)
    {
        if (_currentUser.CustomerId is not Guid customerId)
        {
            return Forbid();
        }

        SaveRoleResult result = await _roles.UpdateAsync(customerId, roleId, request, ct);
        return result == SaveRoleResult.Ok ? NoContent() : Refused(result);
    }

    private IActionResult Refused(SaveRoleResult result) => result switch
    {
        SaveRoleResult.NotFound => NotFound(),
        SaveRoleResult.SystemRole => BadRequest(new MessageResponse
        {
            Message = "System default roles cannot be edited or modified.",
        }),
        _ => StatusCode(StatusCodes.Status500InternalServerError),
    };

    [HttpDelete("{roleId:int}")]
    public async Task<IActionResult> Delete(int roleId, CancellationToken ct)
    {
        if (_currentUser.CustomerId is not Guid customerId)
        {
            return Forbid();
        }

        DeleteRoleResult result = await _roles.DeleteAsync(customerId, roleId, ct);
        return result switch
        {
            DeleteRoleResult.Ok => NoContent(),
            DeleteRoleResult.NotFound => NotFound(),
            DeleteRoleResult.SystemRole => BadRequest(new MessageResponse
            {
                Message = "System roles cannot be deleted.",
            }),
            DeleteRoleResult.InUse => Conflict(new MessageResponse
            {
                Message = "This role is assigned to active users.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}
