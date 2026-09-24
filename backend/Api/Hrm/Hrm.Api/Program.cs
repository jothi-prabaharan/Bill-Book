using System.Text.Json.Serialization;
using Hrm.Api.Services;
using Hrm.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Numbering;
using Shared.Kernel.Persistence;
using Shared.Kernel.Secrets;
using Shared.Kernel.Security;
using Shared.Kernel.Tenancy;

// Hrm (H1, TK-48): organisation setup and the shared employee master, schema
// hrm, port 4509. The scaffold is Printing's, the newest service.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

// Enums by name: the screens send "Male", "Permanent", "Spouse" (TK-124's
// lesson, applied from the first day).
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

builder.Services.AddDbContext<HrmDbContext>((sp, options) =>
{
    options.UseNpgsql(
        sp.GetRequiredService<ITenantDatabaseResolver>().GetConnectionString(sp.GetService<ITenantContext>()?.CustomerId),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "hrm"));
    options.AddInterceptors(
        sp.GetRequiredService<AuditSaveChangesInterceptor>(),
        sp.GetRequiredService<RlsConnectionInterceptor>());
});

// Every write in a transaction, every failure through the SQLSTATE catalogue
// into hrm.ErrorLogs (hard rules 13 and 14).
builder.Services.AddBillBookReliability<HrmDbContext>();

builder.Services.AddBillBookAuthentication(builder.Configuration);

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Employee codes come from the shared numbering table, allocated in this
// service's own transaction; the financial year is the branch's, from Master.
builder.Services.Configure<NumberingOptions>(builder.Configuration.GetSection("Numbering"));
builder.Services.AddTransient<InternalKeyHandler>();
builder.Services.AddHttpClient<IFinancialYearProvider, HttpFinancialYearProvider>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Master:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:4504");
})
    .AddHttpMessageHandler<InternalKeyHandler>();
builder.Services.AddScoped<INumberGenerator>(sp => new NumberGenerator(
    sp.GetRequiredService<HrmDbContext>(),
    sp.GetRequiredService<IOptions<NumberingOptions>>(),
    sp.GetRequiredService<IFinancialYearProvider>()));

builder.Services.AddHttpClient<IMasterUserClient, MasterUserClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Master:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:4504");
})
    .AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddScoped<OrganisationService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<NoticeService>();
builder.Services.AddScoped<LifecycleService>();
builder.Services.AddScoped<HrmSeeder>();

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
