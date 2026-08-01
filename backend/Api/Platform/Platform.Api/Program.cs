using Microsoft.EntityFrameworkCore;
using Platform.Api.Services;
using Platform.Repository;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);

// Persistence — plt schema, with the audit interceptor.
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<PlatformDbContext>((sp, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("MasterDatabase"));
    options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
});

// Signup + provisioning pipeline.
builder.Services.AddScoped<SignupService>();
builder.Services.AddScoped<OrgContextService>();
builder.Services.AddScoped<OrgCurrencyService>();
builder.Services.AddScoped<ConfigurationService>();
builder.Services.AddScoped<SmtpSettingsService>();
builder.Services.AddSingleton<ISecretProtector, AesSecretProtector>();

// Mail is queued and delivered on a background worker, so an SMTP round-trip
// never blocks a request. SmtpEmailSender is resolved by the worker only.
builder.Services.AddScoped<SmtpEmailSender>();
builder.Services.AddSingleton<IEmailQueue, InProcessEmailQueue>();
builder.Services.AddScoped<IEmailSender, QueuedEmailSender>();
builder.Services.AddHostedService<EmailDispatchWorker>();
builder.Services.AddSingleton<IProvisioningQueue, InProcessProvisioningQueue>();
builder.Services.AddHostedService<ProvisioningWorker>();

// Writes each service's master data for a new organization. A named client
// rather than a typed one, because one seeder talks to several services and
// sets the base address per call.
builder.Services.AddHttpClient("seeding");
builder.Services.AddScoped<ITenantSeeder, HttpTenantSeeder>();

// Cross-service seams — Platform never touches another service's DbContext.
builder.Services.AddHttpClient<IIdentityAdmin, IdentityAdminClient>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Identity:BaseUrl"));
});
builder.Services.AddHttpClient<IMasterCurrencies, MasterCurrenciesClient>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Master:BaseUrl"));
});

// Dev infrastructure — swap for Key Vault / Service Bus in production.
builder.Services.AddSingleton<ISecretStore, InMemorySecretStore>();
builder.Services.AddSingleton<IEventPublisher, LoggingEventPublisher>();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

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
