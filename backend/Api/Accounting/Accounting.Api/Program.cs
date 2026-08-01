using System.Text;
using Accounting.Api.Services;
using Accounting.Repository;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);

// Tenancy — resolved per request from the JWT, then used to pick the database.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddScoped<RlsConnectionInterceptor>();
builder.Services.AddScoped<ITenantConnectionResolver, TenantConnectionResolver>();
builder.Services.AddHttpClient<ITenantDirectory, PlatformTenantDirectory>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Platform:BaseUrl"));
});

// TODO: replace with the Key Vault-backed store before production.
builder.Services.AddSingleton<ISecretStore, ConfigurationSecretStore>();

// The connection string is chosen per request, so the context is built from the
// resolved tenant rather than a fixed configuration value.
builder.Services.AddDbContext<AccountingDbContext>((sp, options) =>
{
    ITenantContext tenant = sp.GetRequiredService<ITenantContext>();
    string connectionString = tenant.CustomerId is Guid customerId
        ? sp.GetRequiredService<ITenantConnectionResolver>()
            .ResolveAsync(customerId).GetAwaiter().GetResult()
        // Design-time and unauthenticated paths fall back to the configured value.
        // Guarded, not defaulted: a blank here would hand Npgsql an empty string
        // and the failure would surface as an unrelated connection error.
        : RequiredConnectionString("DesignTimeDatabase");

    options.UseNpgsql(connectionString);
    options.AddInterceptors(
        sp.GetRequiredService<AuditSaveChangesInterceptor>(),
        sp.GetRequiredService<RlsConnectionInterceptor>());
});

builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<SubAccountService>();
builder.Services.AddScoped<TaxMasterService>();
builder.Services.AddScoped<NumberingSeriesService>();
builder.Services.AddScoped<PaymentTermService>();
builder.Services.AddScoped<BankLedgerService>();

// Numbering. Accounting owns the series table and migrates it; the generator is
// registered against this service's own DbContext so a number is allocated
// inside the same transaction as the record that consumes it.
builder.Services.Configure<NumberingOptions>(builder.Configuration.GetSection("Numbering"));
// The financial year start month is the branch's own, held in the master
// database. Cached for hours: it changes about never, and an HTTP call on every
// code allocation would be absurd.
builder.Services.AddHttpClient<IFinancialYearProvider, HttpFinancialYearProvider>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Platform:BaseUrl"));
});

builder.Services.AddScoped<INumberGenerator>(sp => new NumberGenerator(
    sp.GetRequiredService<AccountingDbContext>(),
    sp.GetRequiredService<IOptions<NumberingOptions>>(),
    sp.GetRequiredService<IFinancialYearProvider>()));

// Must match Identity's key exactly: Identity mints the tokens, Accounting only
// validates them. Never fall back to a constant here.
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
builder.Services.AddAuthorization();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

// After authentication so the claims are available, before any DbContext use.
app.UseMiddleware<TenantMiddleware>();

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

string RequiredConnectionString(string name) =>
    builder.Configuration.GetConnectionString(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException(
            $"ConnectionStrings:{name} is not configured. Set it in appsettings.{{Environment}}.json " +
            $"or via the ConnectionStrings__{name} environment variable.");
