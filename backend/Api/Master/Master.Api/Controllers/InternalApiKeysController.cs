using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Master.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel.Internal;
using Shared.Kernel.Security;
using Shared.Kernel.Tenancy;
using BCrypt.Net;

namespace Master.Api.Controllers;

/// <summary>
/// Resolves an API key to the customer, organization and client it belongs to.
///
/// <b>Anonymous but not open</b>: like every other <c>internal/</c> route it
/// takes the shared key, because the caller is another service rather than a
/// user. It shipped with <c>[AllowAnonymous]</c> and no <c>[InternalOnly]</c>
/// beside it, which made key validation — and the key-guessing it enables —
/// callable by anything that could reach the port.
/// </summary>
[ApiController]
[Route("api/internal/api-keys")]
[AllowAnonymous]
[InternalOnly]
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

        // A BCrypt hash cannot be matched in SQL, so the candidates have to be
        // verified in memory — but only this customer's. The filter is bypassed
        // because the organization is not known until the key is matched, and
        // the filter needs both halves; the CustomerId taken from the key is
        // put back by hand so the bypass cannot widen past it. Without that
        // Where, every active key of every customer on the platform was fetched
        // and BCrypt-verified against whatever string the caller sent.
        //
        // Still linear in one customer's keys. If that becomes the cost, the
        // fix is to carry the client id in the key's prefix — bb_{customer}_
        // {client}_{secret} — so a single row is fetched and one hash verified.
        var apiClients = await context.ApiClients
            .IgnoreQueryFilters()
            .Where(x => x.CustomerId == customerId && x.IsActive)
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
