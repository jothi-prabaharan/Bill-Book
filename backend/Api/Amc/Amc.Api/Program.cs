using System.Text.Json.Serialization;
using Amc.Api.Services;
using Amc.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Numbering;
using Shared.Kernel.Persistence;
using Shared.Kernel.Secrets;
using Shared.Kernel.Security;
using Shared.Kernel.Contacts;
using Shared.Kernel.School;
using Shared.Kernel.Tenancy;

// Amc (S8, TK-68): AMC contracts, their covered assets and visits. Schema amc, port 4522. The scaffold is Hrm's.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

// Enums by name, so the screens send "Male", "Active" rather than numbers.
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

builder.Services.AddDbContext<AmcDbContext>((sp, options) =>
{
    options.UseNpgsql(
        sp.GetRequiredService<ITenantDatabaseResolver>().GetConnectionString(sp.GetService<ITenantContext>()?.CustomerId),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "amc"));
    options.AddInterceptors(
        sp.GetRequiredService<AuditSaveChangesInterceptor>(),
        sp.GetRequiredService<RlsConnectionInterceptor>());
});

// Every write in a transaction, every failure through the SQLSTATE catalogue
// into amc.ErrorLogs (hard rules 13 and 14).
builder.Services.AddBillBookReliability<AmcDbContext>();

builder.Services.AddBillBookAuthentication(builder.Configuration);

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddTransient<InternalKeyHandler>();

// ---- Service registrations ----
builder.Services.AddScoped<AmcSeeder>();
builder.Services.AddScoped<AmcService>();

// The vendor (Master's contacts), the covered assets (Facility) and the work
// orders a visit raises (WorkOrder).
builder.Services.AddHttpClient<IContactDirectory, HttpContactDirectory>(client =>
{
    client.BaseAddress = new Uri(MasterUrl(builder.Configuration));
})
    .AddHttpMessageHandler<InternalKeyHandler>();
builder.Services.AddHttpClient<IFacilityClient, HttpFacilityClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Facility:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:4519");
})
    .AddHttpMessageHandler<InternalKeyHandler>();
builder.Services.AddHttpClient<IWorkOrderClient, HttpWorkOrderClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["WorkOrder:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:4520");
})
    .AddHttpMessageHandler<InternalKeyHandler>();

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
    configuration["Master:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:4504";
