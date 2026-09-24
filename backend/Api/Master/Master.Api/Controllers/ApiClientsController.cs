using System.Security.Cryptography;
using Master.Api.Services;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Apps;

namespace Master.Api.Controllers;

/// <summary>
/// API keys a customer issues to its own integrations.
///
/// <b>Guarded as a setting, because minting one is granting access.</b> The key
/// this returns carries the org's own authority for as long as it is active, so
/// the authority to create one belongs with the other branch-level settings
/// rather than with anyone who happens to hold a session — which is what it
/// meant when this controller carried no permission attribute at all and the
/// app's default-deny policy was the only thing in front of it.
///
/// <b>A key acts as a role</b> (D-07, TK-29): its requests carry that role's
/// <c>{module}.{action}</c> permissions, exactly as a user's token does. Only a
/// role <see cref="ApiClientRoles.Assignable"/> offers can be chosen — never one
/// granting <c>platform.*</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("settings")]
[Route("api/master/api-clients")]
[RequireApp(App.All)]
public class ApiClientsController : ControllerBase
{
    private readonly ContactsDbContext _context;
    private readonly AdminDbContext _admin;
    private readonly ITenantContext _tenant;

    public ApiClientsController(ContactsDbContext context, AdminDbContext admin, ITenantContext tenant)
    {
        _context = context;
        _admin = admin;
        _tenant = tenant;
    }

    /// <summary>This branch's keys, with the role each acts as.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var clients = await _context.ApiClients.AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        int[] roleIds = clients.Select(c => c.RoleId).Distinct().ToArray();
        Dictionary<int, string> roleNames = await _admin.Roles.AsNoTracking()
            .Where(r => roleIds.Contains(r.RoleId))
            .ToDictionaryAsync(r => r.RoleId, r => r.DisplayName, ct);

        return Ok(clients.Select(c => Dto(c, roleNames.GetValueOrDefault(c.RoleId))));
    }

    /// <summary>The roles a key may be given: this customer's and the system's, none granting platform access.</summary>
    [HttpGet("roles")]
    public async Task<IActionResult> Roles(CancellationToken ct)
    {
        (Guid customerId, _) = _tenant.Require();

        List<ApiClientRoleOption> roles = await ApiClientRoles.Assignable(_admin, customerId)
            .OrderByDescending(r => r.IsSystemRole)
            .ThenBy(r => r.DisplayName)
            .Select(r => new ApiClientRoleOption(r.RoleId, r.DisplayName, r.IsSystemRole))
            .ToListAsync(ct);

        return Ok(roles);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApiClientRequest request, CancellationToken ct)
    {
        (Guid customerId, Guid orgId) = _tenant.Require();

        if (!await ApiClientRoles.IsAssignableAsync(_admin, customerId, request.RoleId, ct))
        {
            return UnprocessableEntity(RoleRefused());
        }

        // Generate a new secure API key
        var rawKey = GenerateSecureKey();

        // Encode the CustomerId into the prefix so it can be routed by the validator!
        // Format: bb_{customerId:N}_{rawKey}
        var fullApiKey = $"bb_{customerId:N}_{rawKey}";

        var apiClient = new ApiClient
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = request.Name.Trim(),
            HashedApiKey = BCrypt.Net.BCrypt.HashPassword(fullApiKey),
            RoleId = request.RoleId,
            IsActive = true,
        };

        _context.ApiClients.Add(apiClient);
        await _context.SaveChangesAsync(ct);

        string? roleName = await _admin.Roles.Where(r => r.RoleId == request.RoleId)
            .Select(r => r.DisplayName).FirstOrDefaultAsync(ct);

        // Return the plain text key ONCE.
        return Ok(new CreatedApiClient(fullApiKey, Dto(apiClient, roleName)));
    }

    /// <summary>Changes the role a key acts as. A service holding the key's answer cached sees it within five minutes.</summary>
    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> SetRole(Guid id, [FromBody] SetApiClientRoleRequest request, CancellationToken ct)
    {
        (Guid customerId, _) = _tenant.Require();

        ApiClient? client = await _context.ApiClients.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (client is null)
        {
            return NotFound();
        }

        if (!await ApiClientRoles.IsAssignableAsync(_admin, customerId, request.RoleId, ct))
        {
            return UnprocessableEntity(RoleRefused());
        }

        client.RoleId = request.RoleId;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Revokes a key. The row stays, so its history does.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        ApiClient? client = await _context.ApiClients.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (client is null)
        {
            return NotFound();
        }

        client.IsActive = false;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private static ApiClientDto Dto(ApiClient c, string? roleName) =>
        new(c.Id, c.Name, c.RoleId, roleName, c.IsActive, c.LastUsedAt);

    private static MessageResponse RoleRefused() => new()
    {
        Message = "Choose one of this business's roles, or a standard role. A role with platform access cannot be given to a key.",
    };

    private static string GenerateSecureKey()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
