using CostingEngine.Worker.Consumers;
using Inventory.Api.Services;
using Inventory.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);

// Tenancy, set per organization by the worker rather than resolved from a
// request. Scoped, so each organization gets its own context and its own
// connection with its own RLS setting.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddScoped<RlsConnectionInterceptor>();

// The worker acts as no user, so audit columns are stamped with no id. That is
// exactly what CLAUDE.md reserves a null CreatedBy for: written by no person.
builder.Services.AddScoped<ICurrentUser, SystemUser>();

// Attaches the shared internal key to every service-to-service call, so a
// guarded endpoint is reachable by this service and by nothing else.
builder.Services.AddTransient<InternalKeyHandler>();

builder.Services.AddHttpClient<ITenantEnumerator, HttpTenantEnumerator>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Master:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddSingleton<ISecretStore, ConfigurationSecretStore>();

// One shared tenant database now, so the connection string is fixed at
// startup rather than resolved per organization.
builder.Services.AddDbContext<InventoryDbContext>((sp, options) =>
{
    options.UseNpgsql(
        RequiredConnectionString("TenantDatabase"),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "inv"));
    options.AddInterceptors(
        sp.GetRequiredService<AuditSaveChangesInterceptor>(),
        sp.GetRequiredService<RlsConnectionInterceptor>());
});

builder.Services.AddScoped<CostingService>();

// Posting to the ledger. A second pass over the same table rather than work
// done inside the costing transaction: tying a stock movement's fate to
// Accounting being reachable at that instant would either roll back a settled
// cost or lose the posting it owed.
builder.Services.AddScoped<StockLedgerPoster>();
builder.Services.AddHttpClient<IAccountingLedger, AccountingLedger>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Accounting:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddHostedService<CostingWorker>();

IHost host = builder.Build();
host.Run();

// Settings with no safe default, matching the services: blank counts as missing,
// so a broken value fails loudly at startup rather than flowing on silently.
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
            $"ConnectionStrings:{name} is not configured. Set it in " +
            $"appsettings.{{Environment}}.json or via the ConnectionStrings__{name} " +
            "environment variable.");
