using System;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Shared.Kernel.Internal;

namespace Shared.Kernel.Security;

public static class AuthenticationExtensions
{
    public static AuthenticationBuilder AddBillBookAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        // Register the HTTP client for the API Key validator to call the Master service
        string masterBaseUrl = configuration["Master:BaseUrl"] 
            ?? throw new InvalidOperationException("Master:BaseUrl is not configured.");
            
        services.AddHttpClient<IApiKeyValidator, HttpApiKeyValidator>(client =>
        {
            client.BaseAddress = new Uri(masterBaseUrl);
        })
        .AddHttpMessageHandler<InternalKeyHandler>();

        string signingKey = configuration["Jwt:SigningKey"] 
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

        // We use a custom policy scheme that dynamically chooses JWT or ApiKey based on the request
        return services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = "SmartAuth";
            options.DefaultChallengeScheme = "SmartAuth";
        })
        .AddPolicyScheme("SmartAuth", "JWT or API Key", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                if (context.Request.Headers.ContainsKey("X-Api-Key"))
                {
                    return ApiKeyAuthenticationOptions.DefaultScheme;
                }
                return JwtBearerDefaults.AuthenticationScheme;
            };
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = configuration["Jwt:Issuer"] ?? "bill-book",
                ValidateAudience = true,
                ValidAudience = configuration["Jwt:Audience"] ?? "bill-book",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                ValidateLifetime = true,
            };
        })
        .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationOptions.DefaultScheme, 
            options => { });
    }
}
