using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Numbering;
using Shared.Kernel.Persistence;
using Shared.Kernel.Secrets;
using Shared.Kernel.Security;
using Shared.Kernel.Storage;
using Shared.Kernel.Tenancy;
using Site.Api.Services;
using Site.Repository;
using Site.Repository.Repositories;

// Site (D-58, TK-168, TK-169): site services for service vertical. Schema sit, port 4523.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

// Enums by name on the wire
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ITenantDatabaseResolver, TenantDatabaseResolver>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddScoped<RlsConnectionInterceptor>();
builder.Services.AddSecretStore(builder.Configuration, builder.Environment);

builder.Services.AddDbContext<SiteDbContext>((sp, options) =>
{
    options.UseNpgsql(
        sp.GetRequiredService<ITenantDatabaseResolver>().GetConnectionString(sp.GetService<ITenantContext>()?.CustomerId),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "sit"));
    options.AddInterceptors(
        sp.GetRequiredService<AuditSaveChangesInterceptor>(),
        sp.GetRequiredService<RlsConnectionInterceptor>());
});

// Every write in a transaction, every failure into sit.ErrorLogs
builder.Services.AddBillBookReliability<SiteDbContext>();

builder.Services.AddBillBookAuthentication(builder.Configuration);

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddTransient<InternalKeyHandler>();

// Codes come from the shared numbering table, allocated in this service's own transaction
builder.Services.Configure<NumberingOptions>(builder.Configuration.GetSection("Numbering"));
builder.Services.AddHttpClient<IFinancialYearProvider, HttpFinancialYearProvider>(client =>
{
    client.BaseAddress = new Uri(MasterUrl(builder.Configuration));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddScoped<INumberGenerator>(sp => new NumberGenerator(
    sp.GetRequiredService<SiteDbContext>(),
    sp.GetRequiredService<IOptions<NumberingOptions>>(),
    sp.GetRequiredService<IFinancialYearProvider>()));

// ---- Service registrations ----
builder.Services.AddHttpClient<Site.Repository.Clients.IInventoryWarehouseClient, Site.Repository.Clients.HttpInventoryWarehouseClient>(client =>
{
    string url = builder.Configuration["Inventory:BaseUrl"] ?? "http://localhost:4509";
    client.BaseAddress = new Uri(url);
})
    .AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddHttpClient<Site.Repository.Clients.ISalesInvoiceClient, Site.Repository.Clients.HttpSalesInvoiceClient>(client =>
{
    string url = builder.Configuration["Sales:BaseUrl"] ?? "http://localhost:4504";
    client.BaseAddress = new Uri(url);
})
    .AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddFileStorage(builder.Configuration);

builder.Services.AddScoped<SiteSeederRepository>();
builder.Services.AddScoped<SiteRepository>();
builder.Services.AddScoped<SiteMarginRepository>();
builder.Services.AddScoped<SiteAttendanceRepository>();
builder.Services.AddScoped<SiteStockRepository>();
builder.Services.AddScoped<SiteBillingRepository>();

builder.Services.AddHostedService<DatabaseMigrationService>();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseBillBookErrorHandling();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<TenantMiddleware>();

app.MapControllers();

app.Run();

static string MasterUrl(IConfiguration configuration) =>
    configuration["Master:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:4511";
