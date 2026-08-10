using System.Text;
using Master.Api.Services;
using Master.Repository;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Fail at startup rather than on the first request that needs the missing
// registration. Banking shipped without IBaseCurrencyProvider registered — every
// money document endpoint would have thrown on its first call, and neither the
// build nor the tests could see it, because the build does not resolve DI and
// the tests construct their services by hand. This is what closes that gap:
// ValidateOnBuild walks every registration at startup, so a service asking for
// something nobody registered is a container that refuses to build.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped<ICurrentUser, AnonymousCurrentUser>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<MasterDbContext>((sp, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("MasterDatabase"));
    options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
});

// Must match Identity's key exactly: Identity mints the tokens, Master only
// validates them. Never fall back to a constant here.
//
// Master had no authentication at all until now, on the reasoning that
// countries and GST codes are not secret. They are not — but "not secret" is not
// the same as "serve to anyone who finds the port", and the HSN/SAC importer on
// this service was reachable by anybody at all.
string signingKey = RequiredSetting("Jwt:SigningKey");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "bill-book",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "bill-book",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
        };
    });

// Default deny: a controller added later is authenticated because nobody did
// anything, which is the opposite of how this service got here. The endpoints
// that genuinely predate a token say so with [AllowAnonymous].
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Settings with no safe default. Blank is treated as missing: appsettings.json ships
// every key present but empty so the shape is discoverable, which means a `??` fallback
// would never fire and a broken value would flow on silently instead.
string RequiredSetting(string key) =>
    builder.Configuration[key] is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException(
            $"{key} is not configured. Set it in appsettings.{{Environment}}.json or via the " +
            $"{key.Replace(':', '_').Replace("_", "__")} environment variable.");
