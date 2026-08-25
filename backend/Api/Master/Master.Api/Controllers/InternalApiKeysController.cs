using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Master.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel.Security;
using Shared.Kernel.Tenancy;
using BCrypt.Net;

namespace Master.Api.Controllers;

[ApiController]
[Route("api/internal/api-keys")]
[AllowAnonymous] 
public class InternalApiKeysController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TenantContext _tenantContext;

    public InternalApiKeysController(IServiceProvider serviceProvider, TenantContext tenantContext)
    {
        _serviceProvider = serviceProvider;
        _tenantContext = tenantContext;
    }

    [HttpPost("validate")]
    public async Task<ActionResult<ApiKeyValidationResult>> Validate([FromBody] ValidateApiKeyRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey) || !request.ApiKey.StartsWith("bb_"))
        {
            return new ApiKeyValidationResult { IsValid = false };
        }

        var parts = request.ApiKey.Split('_');
        if (parts.Length != 3 || !Guid.TryParseExact(parts[1], "N", out var customerId))
        {
            return new ApiKeyValidationResult { IsValid = false };
        }

        // Set the CustomerId on the tenant context so the DbContext connects to the correct database!
        _tenantContext.CustomerId = customerId;

        // Now resolve the DbContext, which will use the CustomerId we just set.
        var context = _serviceProvider.GetRequiredService<ContactsDbContext>();

        // Find all active API clients for this customer.
        // We use raw list because we cannot decrypt hashes in DB, we must fetch and verify in memory.
        // Since API clients are few, we can just fetch all or we can fetch by Customer? 
        // Wait, if there are many API keys per customer, fetching all is bad.
        // But BCrypt doesn't allow DB-side matching. We must fetch all for the org, or we need to look it up!
        // To fix this at scale, we usually store a hash (SHA256) of the raw key in the DB for exact match, 
        // OR we store the API Key ID in the prefix as well! e.g. bb_{customerId}_{apiKeyId}_{secret}
        // Then we can query by ID!
        // Let's assume we fetch all for now, or since we only just wrote ApiClientsController, 
        // we can fetch all Active ones and verify.
        
        var apiClients = await context.ApiClients
            .IgnoreQueryFilters() // Bypass RLS for internal lookup
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        var apiClient = apiClients.FirstOrDefault(x => BCrypt.Net.BCrypt.Verify(request.ApiKey, x.HashedApiKey));

        if (apiClient == null)
        {
            return new ApiKeyValidationResult { IsValid = false };
        }

        apiClient.LastUsedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return new ApiKeyValidationResult
        {
            IsValid = true,
            CustomerId = customerId,
            OrgId = apiClient.OrgId,
            ApiClientId = apiClient.Id,
            ClientName = apiClient.Name
        };
    }
}

public class ValidateApiKeyRequest
{
    public string ApiKey { get; set; } = string.Empty;
}
