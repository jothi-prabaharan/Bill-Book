using CostingEngine.Worker.Consumers;
using Inventory.Api.Services;
using Inventory.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;
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
builder.Services.AddScoped<ITenantConnectionResolver, TenantConnectionResolver>();

// The worker acts as no user, so audit columns are stamped with no id. That is
// exactly what CLAUDE.md reserves a null CreatedBy for: written by no person.
builder.Services.AddScoped<ICurrentUser, SystemUser>();

builder.Services.AddHttpClient<ITenantDirectory, PlatformTenantDirectory>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Platform:BaseUrl"));
});

builder.Services.AddHttpClient<ITenantEnumerator, HttpTenantEnumerator>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Platform:BaseUrl"));
});

// Reused from Inventory rather than reimplemented: the worker resolves tenant
// connections exactly the way the service does, and two copies of that logic is
// two chances for them to disagree about which database to open.
builder.Services.AddSingleton<ISecretStore, ConfigurationSecretStore>();

builder.Services.AddDbContext<InventoryDbContext>((sp, options) =>
{
    ITenantContext tenant = sp.GetRequiredService<ITenantContext>();

    string connectionString = tenant.CustomerId is Guid customerId
        ? sp.GetRequiredService<ITenantConnectionResolver>()
            .ResolveAsync(customerId).GetAwaiter().GetResult()
        : RequiredConnectionString("DesignTimeDatabase");

    options.UseNpgsql(connectionString);
    options.AddInterceptors(
        sp.GetRequiredService<AuditSaveChangesInterceptor>(),
        sp.GetRequiredService<RlsConnectionInterceptor>());
});

builder.Services.AddScoped<CostingService>();
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
