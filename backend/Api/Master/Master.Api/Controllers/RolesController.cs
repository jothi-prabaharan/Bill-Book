using Master.Api.Services;
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
[RequireApp(App.All)]
public sealed class RolesController : ControllerBase
{
    private readonly RoleService _roles;
    private readonly ICurrentUser _currentUser;

    public RolesController(RoleService roles, ICurrentUser currentUser)
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

        return Ok(await _roles.ListAsync(customerId, ct, CallerApp()));
    }

    /// <summary>
    /// The permission matrix for the checkbox grid, grouped by module. With
    /// <c>?app=</c>, only what a role of that app may be granted (TK-42).
    /// </summary>
    [HttpGet("permissions")]
    public async Task<IActionResult> Permissions([FromQuery] string? app, CancellationToken ct)
    {
        App forApp = AppRules.TryParseSingle(app, out App parsed) ? parsed : CallerApp();

        // platform.* is operator-only, so it never reaches a tenant's matrix.
        return Ok(await _roles.PermissionMatrixAsync(includePlatform: false, ct, forApp));
    }

    [HttpGet("{roleId:int}")]
    public async Task<IActionResult> Get(int roleId, CancellationToken ct)
    {
        if (_currentUser.CustomerId is not Guid customerId)
        {
            return Forbid();
        }

        RoleDetail? role = await _roles.GetAsync(customerId, roleId, ct);
        return role is null ? NotFound() : Ok(role);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveRoleRequest request, CancellationToken ct)
    {
        if (_currentUser.CustomerId is not Guid customerId)
        {
            return Forbid();
        }

        // A role made inside an app is that app's, unless the request names one (TK-43).
        request.App ??= CallerApp().ToString();
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

    private App CallerApp()
    {
        App app = RequireAppAttribute.AppOf(User);
        return app == App.None ? App.RetailErp : app;
    }

    private IActionResult Refused(SaveRoleResult result) => result switch
    {
        SaveRoleResult.NotFound => NotFound(),
        SaveRoleResult.PermissionNotInApp => UnprocessableEntity(new MessageResponse
        {
            Message = "A role can be given only permissions that belong to its app.",
        }),
        SaveRoleResult.InvalidApp => BadRequest(new MessageResponse
        {
            Message = "Choose one app for the role: RetailErp, School, Hrms or Payroll.",
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
