using System.Text;
using Identity.Api.Services;
using Identity.Repository;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Attaches the shared internal key to every service-to-service call, so a
// guarded endpoint is reachable by this service and by nothing else.
builder.Services.AddTransient<InternalKeyHandler>();

builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

// Persistence — idn schema, with the audit interceptor.
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<IdentityDbContext>((sp, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("MasterDatabase"));
    options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
});

// Auth services.
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<RoleService>();
builder.Services.AddScoped<UserService>();

// Mail goes through Platform, which owns plt.SmtpSettings — the decrypted
// password never leaves that service. Platform queues and delivers it.
builder.Services.AddHttpClient<IEmailSender, PlatformEmailSender>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Platform:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

// Cross-service seam to Platform.
builder.Services.AddHttpClient<IPlatformDirectory, PlatformDirectory>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Platform:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

// JWT bearer for protected endpoints (the auth endpoints themselves are anonymous).
JwtOptions jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.SigningKey))
{
    throw new InvalidOperationException(
        "Jwt:SigningKey is not configured. Set it in appsettings.{Environment}.json or via the " +
        "Jwt__SigningKey environment variable. Never fall back to a constant: every deployment " +
        "sharing a signing key means tokens minted anywhere are accepted everywhere.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
        };
    });
// Default deny: a controller added later is authenticated because nobody did
// anything about it. Endpoints that genuinely run before a token exists —
// signup, login, the internal service-to-service ones — say so with
// [AllowAnonymous], which makes the exception visible on the controller itself.
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
