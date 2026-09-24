using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Shared.Kernel.Security;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IApiKeyValidator _validator;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyValidator validator)
        : base(options, logger, encoder)
    {
        _validator = validator;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var extractedApiKey))
        {
            return AuthenticateResult.NoResult();
        }

        var apiKey = extractedApiKey.ToString();
        var validationResult = await _validator.ValidateAsync(apiKey, Context.RequestAborted);

        if (!validationResult.IsValid)
        {
            return AuthenticateResult.Fail("Invalid API Key provided.");
        }

        var claims = new List<Claim>
        {
            new Claim("customer_id", validationResult.CustomerId.ToString()),
            new Claim("org_id", validationResult.OrgId.ToString()),
            new Claim("sub", validationResult.ApiClientId.ToString()),
            new Claim("name", validationResult.ClientName),
            new Claim("role", "ApiClient") // Special role for API clients
        };

        claims.AddRange(PermissionClaims(validationResult.Permissions));

        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationOptions.DefaultScheme);
        var identities = new List<ClaimsIdentity> { identity };
        var principal = new ClaimsPrincipal(identities);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationOptions.DefaultScheme);

        return AuthenticateResult.Success(ticket);
    }

    /// <summary>
    /// One <c>permission</c> claim per code of the client's role, the same claim
    /// a user's token carries, so <c>[RequireModulePermission]</c> judges a key
    /// exactly as it judges a person (D-07, TK-29). A <c>platform.*</c> code is
    /// dropped even if Master sent one: an API key is a customer's credential,
    /// and platform access is never a customer's to hand out.
    /// </summary>
    public static IEnumerable<Claim> PermissionClaims(IEnumerable<string>? codes) =>
        (codes ?? [])
            .Where(code => !string.IsNullOrWhiteSpace(code)
                && !code.StartsWith("platform.", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(code => new Claim("permission", code));
}
