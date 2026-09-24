using Master.Repository;
using Microsoft.EntityFrameworkCore;
using RateSync.Worker;
using RateSync.Worker.Consumers;
using RateSync.Worker.Rbi;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Persistence;

// Fills the rat schema in the master database (TK-24): the RBI reference rates
// daily (TK-26). IBJA's metal rates are TK-25.
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);

// The worker acts as no user, so audit columns are stamped with no id — what
// CLAUDE.md reserves a null CreatedBy for: written by no person.
builder.Services.AddScoped<ICurrentUser, SystemUser>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();

// The master database, which holds no tenant rows: no tenant context, no RLS
// interceptor, the same as Master's own AdminDbContext registration.
builder.Services.AddDbContext<AdminDbContext>((sp, options) =>
{
    options.UseNpgsql(RequiredConnectionString("AdminDatabase"));
    options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
});

builder.Services.AddHttpClient<IReferenceRatePageSource, HttpReferenceRatePageSource>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Rbi:ReferenceRateUrl"));
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; RetailErp-RateSync/1.0)");
});

builder.Services.AddScoped<ExchangeRateSync>();
builder.Services.AddHostedService<RateSyncWorker>();

IHost host = builder.Build();
host.Run();

string RequiredSetting(string key) =>
    builder.Configuration[key] is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException(
            $"{key} is not configured. Set it in appsettings.{{Environment}}.json or via the " +
            $"{key.Replace(":", "__")} environment variable.");

string RequiredConnectionString(string name) =>
    builder.Configuration.GetConnectionString(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException(
            $"ConnectionStrings:{name} is not configured. Set it in " +
            $"appsettings.{{Environment}}.json or via the ConnectionStrings__{name} " +
            "environment variable.");
