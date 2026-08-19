using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Reporting.Api.Services;
using Reporting.Api.Services.Sources;
using Reporting.Repository;
using Reporting.Repository.SeedData;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Fail at startup rather than on the first request that needs the missing
// registration. ValidateOnBuild walks every registration, so a service asking for
// something nobody registered is a container that refuses to build — which the
// build cannot see, because it does not resolve DI, and the tests cannot see,
// because they construct their services by hand.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

builder.Services.AddControllers();

// Attaches the shared internal key to every service-to-service call.
builder.Services.AddTransient<InternalKeyHandler>();

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
builder.Services.AddHttpClient<ITenantDirectory, MasterTenantDirectory>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Master:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

// TODO: replace with the Key Vault-backed store before production.
builder.Services.AddSingleton<ISecretStore, ConfigurationSecretStore>();

// The connection string is chosen per request, so the context is built from the
// resolved tenant rather than a fixed configuration value.
builder.Services.AddDbContext<ReportingDbContext>((sp, options) =>
{
    ITenantContext tenant = sp.GetRequiredService<ITenantContext>();
    string connectionString = tenant.CustomerId is Guid customerId
        ? sp.GetRequiredService<ITenantConnectionResolver>()
            .ResolveAsync(customerId).GetAwaiter().GetResult()
        // Design-time and unauthenticated paths fall back to the configured value.
        : RequiredConnectionString("DesignTimeDatabase");

    options.UseNpgsql(connectionString);

    // RlsConnectionInterceptor is what sets app.current_org_id, and it sets it
    // transaction-locally. Never connection-level: pooled connections are reused
    // across requests, so a connection-scoped org context leaks to whoever gets
    // that connection next — which is gate G3 in Reporting.md §9.1.
    options.AddInterceptors(
        sp.GetRequiredService<AuditSaveChangesInterceptor>(),
        sp.GetRequiredService<RlsConnectionInterceptor>());
});

// The branch's base currency, which names every money column's header. Cached
// per organization for the same reason the financial year is: it changes about
// never, and the alternative is an HTTP call on every report run.
builder.Services.AddHttpClient<IBaseCurrencyProvider, HttpBaseCurrencyProvider>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Master:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

// Every report, registered as an IReportSource. The catalog service discovers
// them from DI, so adding a report is adding a line here and a seed entry —
// there is no registry to keep in step.
builder.Services.AddHttpClient<BatchedNameResolver>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Master:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();
builder.Services.AddScoped<IReportSource, AccountMovementSource>();
builder.Services.AddScoped<IReportSource, AccountTransactionSource>();
builder.Services.AddScoped<IReportSource, TrialBalanceSource>();
builder.Services.AddScoped<IReportSource, GeneralLedgerSummarySource>();
builder.Services.AddScoped<IReportSource, JournalReportSource>();
builder.Services.AddScoped<IReportSource, BankSummarySource>();
builder.Services.AddScoped<IReportSource, ReconciliationSource>();
builder.Services.AddScoped<IReportSource, InventoryAgingSource>();
builder.Services.AddScoped<IReportSource, ItemListSource>();
builder.Services.AddScoped<IReportSource, ItemDetailSource>();
builder.Services.AddScoped<IReportSource, ItemSummarySource>();
builder.Services.AddScoped<IReportSource, BatchTrackingStatusSource>();
builder.Services.AddScoped<IReportSource, BatchTrackingDetailSource>();
builder.Services.AddScoped<IReportSource, SerialTrackingStatusSource>();
builder.Services.AddScoped<IReportSource, SerialTrackingDetailSource>();
builder.Services.AddScoped<IReportSource, WarehouseTrackingStatusSource>();
builder.Services.AddScoped<IReportSource, WarehouseTrackingDetailSource>();
builder.Services.AddScoped<IReportSource, SalesRegisterSource>();

builder.Services.AddScoped<ReportCatalogService>();
builder.Services.AddScoped<ReportRunner>();
builder.Services.AddScoped<SavedViewService>();
builder.Services.AddScoped<ReportCatalogSeeder>();

// Must match Master's key exactly: Master mints the tokens, Reporting only
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

// Default deny: a controller added later is authenticated because nobody did
// anything about it. The internal seed endpoint says [AllowAnonymous] explicitly,
// which makes the exception visible on the controller itself.
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

