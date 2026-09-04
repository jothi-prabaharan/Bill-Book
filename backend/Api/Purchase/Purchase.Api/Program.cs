using Shared.Kernel.Security;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Purchase.Api.Services;
using Purchase.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Secrets;
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
// the tests construct their services by hand. Every service carries this now.
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
builder.Services.AddSingleton<ITenantDatabaseResolver, TenantDatabaseResolver>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);

// Tenancy — resolved per request from the JWT, then used to pick the database.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddScoped<RlsConnectionInterceptor>();
// Key Vault when KeyVault:Uri is set, configuration otherwise — and a
// startup failure in Production if neither, rather than serving requests off
// whatever configuration happens to hold. See SecretStoreRegistration.
builder.Services.AddSecretStore(builder.Configuration, builder.Environment);

// One shared tenant database now, so the connection string is fixed at
// startup rather than resolved per request.
builder.Services.AddDbContext<PurchaseDbContext>((sp, options) =>
{
    options.UseNpgsql(
        sp.GetRequiredService<ITenantDatabaseResolver>().GetConnectionString(sp.GetService<ITenantContext>()?.CustomerId),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "pur"));
    options.AddInterceptors(
        sp.GetRequiredService<AuditSaveChangesInterceptor>(),
        sp.GetRequiredService<RlsConnectionInterceptor>());
});

builder.Services.AddScoped<PurchaseSeeder>();
builder.Services.AddScoped<PurchaseOrderService>();
builder.Services.AddScoped<GoodsReceiptService>();
builder.Services.AddScoped<BillService>();
builder.Services.AddScoped<DebitNoteService>();

// The due date a payment term implies. Asked rather than computed: the rule is
// Accounting's, and a second implementation here would disagree with it the
// first time a day-of-month term was used.
builder.Services.AddHttpClient<IPaymentTermClient, PaymentTermClient>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Accounting:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

// Stock, moved synchronously. A receipt applies its quantity and opens its cost
// layer inside the request — two people receiving the same delivery is a real
// thing, and an eventual answer cannot refuse the second one.
builder.Services.AddHttpClient<IInventoryClient, InventoryClient>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Inventory:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

// The general ledger. Purchase decides which accounts a receipt touches —
// it is the only service that knows the vendor and which part of the figure is
// reclaimable tax — and Accounting decides whether the result is a legal posting.
builder.Services.AddHttpClient<ILedgerClient, LedgerClient>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Accounting:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

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
// Sales — one component, because the same supply computed two ways is two
// answers and only one of them goes on the return. Here the result is Input GST,
// an asset; on the sales side the identical arithmetic yields a liability.
builder.Services.AddHttpClient<ITaxRateProvider, HttpTaxRateProvider>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Accounting:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

// The names a document deliberately does not store. PURCHASE.md keeps
// ContactName, ItemCode and ItemName off the row so a correction shows
// everywhere, including on orders already raised — and that only stays
// affordable if the names come back in one call per page rather than one per
// row. Contacts live in Master since the service merge; items in Inventory.
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

// Numbering. The series table belongs to Accounting, but the generator runs
// against this service's own DbContext so a document number is allocated inside
// the same transaction as the document — a failed insert gives the number back.
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
    sp.GetRequiredService<PurchaseDbContext>(),
    sp.GetRequiredService<IOptions<NumberingOptions>>(),
    sp.GetRequiredService<IFinancialYearProvider>()));

// Must match Master's key exactly: Master mints the tokens, Purchase only
// validates them. Never fall back to a constant here.
builder.Services.AddBillBookAuthentication(builder.Configuration);

// Default deny: a controller added later is authenticated because nobody did
// anything about it. Endpoints that genuinely run before a token exists — the
// internal service-to-service ones — say so with [AllowAnonymous], which makes
// the exception visible on the controller itself.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddHostedService<DatabaseMigrationService>();

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

#pragma warning disable CS8321
string RequiredConnectionString(string name) =>
    builder.Configuration.GetConnectionString(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException(
            $"ConnectionStrings:{name} is not configured. Set it in appsettings.{{Environment}}.json " +
            $"or via the ConnectionStrings__{name} environment variable.");

