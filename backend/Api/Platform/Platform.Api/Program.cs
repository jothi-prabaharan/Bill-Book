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
builder.Services.AddSingleton<IProvisioningQueue, InProcessProvisioningQueue>();
builder.Services.AddHostedService<ProvisioningWorker>();

// Cross-service seams — Platform never touches another service's DbContext.
builder.Services.AddHttpClient<IIdentityAdmin, IdentityAdminClient>(client =>
{
    string baseUrl = builder.Configuration["Identity:BaseUrl"] ?? "http://localhost:5001";
    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddHttpClient<IMasterCurrencies, MasterCurrenciesClient>(client =>
{
    string baseUrl = builder.Configuration["Master:BaseUrl"] ?? "http://localhost:5003";
    client.BaseAddress = new Uri(baseUrl);
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
