using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Performance.Api.Services;
using Performance.Repository;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Persistence;
using Shared.Kernel.Secrets;
using Shared.Kernel.Security;
using Shared.Kernel.Tenancy;

// Performance (H11, TK-58): performance review cycles, goals, competencies, self-evaluation,
// multi-level appraisal approvals, calibration, release and salary revisions.
// Schema prf, port 4513.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

// Enums by name
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
builder.Services.AddScoped<ICallerPermissions, HttpCallerPermissions>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddScoped<RlsConnectionInterceptor>();
builder.Services.AddSecretStore(builder.Configuration, builder.Environment);

builder.Services.AddDbContext<PerformanceDbContext>((sp, options) =>
{
    options.UseNpgsql(
        sp.GetRequiredService<ITenantDatabaseResolver>().GetConnectionString(sp.GetService<ITenantContext>()?.CustomerId),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "prf"));
    options.AddInterceptors(
        sp.GetRequiredService<AuditSaveChangesInterceptor>(),
        sp.GetRequiredService<RlsConnectionInterceptor>());
});

builder.Services.AddBillBookReliability<PerformanceDbContext>();
builder.Services.AddBillBookAuthentication(builder.Configuration);

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddTransient<InternalKeyHandler>();

builder.Services.AddHttpClient<IEmployeeClient, EmployeeClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Employee:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:4509");
}).AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddHttpClient<IMasterClient, MasterClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Master:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:4504");
}).AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddHttpClient<IPayrollClient, PayrollClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Payroll:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:4511");
}).AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddScoped<PerformanceService>();
builder.Services.AddScoped<PerformanceSeeder>();

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
