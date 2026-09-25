using System.Text.Json.Serialization;
using Claims.Api.Services;
using Claims.Repository;
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

builder.Services.AddDbContext<ClaimsDbContext>((sp, options) =>
{
    options.UseNpgsql(
        sp.GetRequiredService<ITenantDatabaseResolver>().GetConnectionString(sp.GetService<ITenantContext>()?.CustomerId),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "clm"));
    options.AddInterceptors(
        sp.GetRequiredService<AuditSaveChangesInterceptor>(),
        sp.GetRequiredService<RlsConnectionInterceptor>());
});

builder.Services.AddBillBookReliability<ClaimsDbContext>();
builder.Services.AddBillBookAuthentication(builder.Configuration);

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddTransient<InternalKeyHandler>();

// Numbering generator
builder.Services.Configure<NumberingOptions>(builder.Configuration.GetSection("Numbering"));
builder.Services.AddHttpClient<IFinancialYearProvider, HttpFinancialYearProvider>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Master:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:4504");
}).AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddScoped<INumberGenerator>(sp => new NumberGenerator(
    sp.GetRequiredService<ClaimsDbContext>(),
    sp.GetRequiredService<IOptions<NumberingOptions>>(),
    sp.GetRequiredService<IFinancialYearProvider>()));

builder.Services.AddHttpClient<IHrmClient, HrmClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Hrm:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:4509");
}).AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddHttpClient<IAccountingClient, AccountingClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Accounting:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:4501");
}).AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddHttpClient<IPayrollClient, PayrollClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Payroll:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:4511");
}).AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddScoped<ClaimService>();
builder.Services.AddScoped<ClaimsSeeder>();

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
