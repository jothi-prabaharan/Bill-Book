using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;
using BCrypt.Net;

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
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("settings")]
[Route("api/master/api-clients")]
public class ApiClientsController : ControllerBase
{
    private readonly ContactsDbContext _context;
    private readonly ITenantContext _tenant;

    public ApiClientsController(ContactsDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateApiClientRequest request, CancellationToken cancellationToken)
    {
        // Generate a new secure API key
        var rawKey = GenerateSecureKey();
        
        // Encode the CustomerId into the prefix so it can be routed by the validator!
        // Format: bb_{customerId:N}_{rawKey}
        var fullApiKey = $"bb_{_tenant.CustomerId:N}_{rawKey}";
        
        var hashedKey = BCrypt.Net.BCrypt.HashPassword(fullApiKey);

        var apiClient = new ApiClient
        {
            Id = Guid.NewGuid(),
            OrgId = _tenant.OrgId!.Value,
            Name = request.Name,
            HashedApiKey = hashedKey,
            IsActive = true
        };

        _context.ApiClients.Add(apiClient);
        await _context.SaveChangesAsync(cancellationToken);

        // Return the plain text key ONCE.
        return Ok(new { ApiKey = fullApiKey, ApiClient = apiClient });
    }

    private static string GenerateSecureKey()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}

public class CreateApiClientRequest
{
    public string Name { get; set; } = string.Empty;
}
