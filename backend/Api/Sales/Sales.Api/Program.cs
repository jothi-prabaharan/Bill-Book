using System.Text;
using Sales.Api.Services;
using Sales.Repository;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Documents;
using Shared.Kernel.Internal;
using Shared.Kernel.Numbering;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Fail at startup rather than on the first request that needs the missing
// registration. Banking shipped without IBaseCurrencyProvider registered — every
// money document endpoint would have thrown on its first call, and neither the
// build nor the tests could see it, because the build does not resolve DI and
// the tests construct their services by hand. Every service carries this now:
// ValidateOnBuild walks every registration at startup, so a service asking for
// something nobody registered is a container that refuses to build.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

builder.Services.AddControllers();

// Attaches the shared internal key to every service-to-service call, so a
// guarded endpoint is reachable by this service and by nothing else.
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
builder.Services.AddDbContext<SalesDbContext>((sp, options) =>
{
    ITenantContext tenant = sp.GetRequiredService<ITenantContext>();
    string connectionString = tenant.CustomerId is Guid customerId
        ? sp.GetRequiredService<ITenantConnectionResolver>()
            .ResolveAsync(customerId).GetAwaiter().GetResult()
        // Design-time and unauthenticated paths fall back to the configured value.
        : RequiredConnectionString("TenantFallback");

    options.UseNpgsql(connectionString);
    options.AddInterceptors(
        sp.GetRequiredService<AuditSaveChangesInterceptor>(),
        sp.GetRequiredService<RlsConnectionInterceptor>());
});

// T2.2 is schema only. The document services arrive with the screens that use
// them — the quote at T2.3, the order at T2.4 — and are registered here then.
builder.Services.AddScoped<SalesSeeder>();
builder.Services.AddScoped<QuoteService>();
builder.Services.AddScoped<SalesOrderService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<DeliveryChallanService>();
builder.Services.AddScoped<CreditNoteService>();

// The branch's base currency, stamped onto every document's base-currency total.
// Cached per organization: it changes about never, and the alternative is an
// HTTP call on every save.
builder.Services.AddHttpClient<IBaseCurrencyProvider, HttpBaseCurrencyProvider>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Master:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddHttpClient<IBranchSettingsProvider, HttpBranchSettingsProvider>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Master:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

// GST rates, read from Accounting and cached per organization and date. The
// calculator that uses them is pure and lives in Shared.Kernel.Tax, shared with
// Purchase — one component, because the same sale computed two ways is two
// answers and only one of them goes on the return.
builder.Services.AddHttpClient<ITaxRateProvider, HttpTaxRateProvider>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Accounting:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

// The names a document deliberately does not store. SALES.md keeps ContactName,
// ItemCode and ItemName off the row so a correction shows everywhere, including
// on documents already raised — and that only stays affordable if the names come
// back in one call per page rather than one per row. Contacts live in Master
// since the service merge; items in Inventory.
builder.Services.AddHttpClient<IContactNameLookup, HttpContactNameLookup>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Master:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddHttpClient<IItemNameLookup, HttpItemNameLookup>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Inventory:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddHttpClient<ILedgerClient, LedgerClient>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Accounting:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddHttpClient<IInventoryClient, InventoryClient>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Inventory:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

// Numbering. The series table belongs to Accounting, but the generator runs
// against this service's own DbContext so a document number is allocated inside the
// same transaction as the document — a failed insert gives the number back.
builder.Services.Configure<NumberingOptions>(builder.Configuration.GetSection("Numbering"));
// The financial year start month is the branch's own, held in the master
// database. Cached for hours: it changes about never, and an HTTP call on every
// code allocation would be absurd.
builder.Services.AddHttpClient<IFinancialYearProvider, HttpFinancialYearProvider>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Master:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddScoped<INumberGenerator>(sp => new NumberGenerator(
    sp.GetRequiredService<SalesDbContext>(),
    sp.GetRequiredService<IOptions<NumberingOptions>>(),
    sp.GetRequiredService<IFinancialYearProvider>()));

// Must match Master's key exactly: Master mints the tokens, Sales only
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


