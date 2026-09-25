using System.Text.Json.Serialization;
using WorkOrder.Api.Services;
using WorkOrder.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Numbering;
using Shared.Kernel.Persistence;
using Shared.Kernel.Secrets;
using Shared.Kernel.Security;
using Shared.Kernel.Employees;
using Shared.Kernel.School;
using Shared.Kernel.Stock;
using Shared.Kernel.Tenancy;

// WorkOrder (S6, TK-66): work orders, their tasks and the parts issued from Inventory. Schema wrk, port 4520. The scaffold is Hrm's.
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

builder.Services.AddDbContext<WorkOrderDbContext>((sp, options) =>
{
    options.UseNpgsql(
        sp.GetRequiredService<ITenantDatabaseResolver>().GetConnectionString(sp.GetService<ITenantContext>()?.CustomerId),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "wrk"));
    options.AddInterceptors(
        sp.GetRequiredService<AuditSaveChangesInterceptor>(),
        sp.GetRequiredService<RlsConnectionInterceptor>());
});

// Every write in a transaction, every failure through the SQLSTATE catalogue
// into wrk.ErrorLogs (hard rules 13 and 14).
builder.Services.AddBillBookReliability<WorkOrderDbContext>();

builder.Services.AddBillBookAuthentication(builder.Configuration);

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddTransient<InternalKeyHandler>();

// Codes come from the shared numbering table, allocated in this service's own
// transaction; the financial year is the branch's, from Master.
builder.Services.Configure<NumberingOptions>(builder.Configuration.GetSection("Numbering"));
builder.Services.AddHttpClient<IFinancialYearProvider, HttpFinancialYearProvider>(client =>
{
    client.BaseAddress = new Uri(MasterUrl(builder.Configuration));
})
    .AddHttpMessageHandler<InternalKeyHandler>();
builder.Services.AddScoped<INumberGenerator>(sp => new NumberGenerator(
    sp.GetRequiredService<WorkOrderDbContext>(),
    sp.GetRequiredService<IOptions<NumberingOptions>>(),
    sp.GetRequiredService<IFinancialYearProvider>()));

// ---- Service registrations ----
builder.Services.AddScoped<WorkOrderSeeder>();
builder.Services.AddScoped<WorkOrderService>();

// Where the work is (Facility), who does it (Hrm), and the parts it uses (Inventory).
builder.Services.AddHttpClient<IFacilityClient, HttpFacilityClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Facility:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:4519");
})
    .AddHttpMessageHandler<InternalKeyHandler>();
builder.Services.AddHttpClient<IEmployeeDirectory, HttpEmployeeDirectory>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Hrm:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:4509");
})
    .AddHttpMessageHandler<InternalKeyHandler>();
builder.Services.AddHttpClient<IStockClient, HttpStockClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Inventory:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:4503");
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
