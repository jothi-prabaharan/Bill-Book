using System.Text.Json.Serialization;
using Fee.Api.Services;
using Fee.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Kernel.Contacts;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Ledgers;
using Shared.Kernel.School;
using Shared.Kernel.Internal;
using Shared.Kernel.Numbering;
using Shared.Kernel.Persistence;
using Shared.Kernel.Secrets;
using Shared.Kernel.Security;
using Shared.Kernel.Tenancy;

// Fee (S4, TK-64): fee heads, structures, concessions, demands, receipts and their allocation, posted to the ledger. Schema fee, port 4518. The scaffold is Employee's.
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

builder.Services.AddDbContext<FeeDbContext>((sp, options) =>
{
    options.UseNpgsql(
        sp.GetRequiredService<ITenantDatabaseResolver>().GetConnectionString(sp.GetService<ITenantContext>()?.CustomerId),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "fee"));
    options.AddInterceptors(
        sp.GetRequiredService<AuditSaveChangesInterceptor>(),
        sp.GetRequiredService<RlsConnectionInterceptor>());
});

// Every write in a transaction, every failure through the SQLSTATE catalogue
// into fee.ErrorLogs (hard rules 13 and 14).
builder.Services.AddBillBookReliability<FeeDbContext>();

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
    sp.GetRequiredService<FeeDbContext>(),
    sp.GetRequiredService<IOptions<NumberingOptions>>(),
    sp.GetRequiredService<IFinancialYearProvider>()));

// Students and enrolments from Student, guardians from Master, accounts and the
// ledger from Accounting, the base currency from Master's org context.
string accountingUrl = builder.Configuration["Accounting:BaseUrl"] is { Length: > 0 } acc ? acc : "http://localhost:4501";
builder.Services.AddHttpClient<IStudentClient, HttpStudentClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Student:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:4515");
})
    .AddHttpMessageHandler<InternalKeyHandler>();
builder.Services.AddHttpClient<IContactDirectory, HttpContactDirectory>(client => client.BaseAddress = new Uri(MasterUrl(builder.Configuration)))
    .AddHttpMessageHandler<InternalKeyHandler>();
builder.Services.AddHttpClient<IBaseCurrencyProvider, HttpBaseCurrencyProvider>(client => client.BaseAddress = new Uri(MasterUrl(builder.Configuration)))
    .AddHttpMessageHandler<InternalKeyHandler>();
builder.Services.AddHttpClient<IAccountDirectory, HttpAccountDirectory>(client => client.BaseAddress = new Uri(accountingUrl))
    .AddHttpMessageHandler<InternalKeyHandler>();
builder.Services.AddHttpClient<IFeeLedger, HttpFeeLedger>(client => client.BaseAddress = new Uri(accountingUrl))
    .AddHttpMessageHandler<InternalKeyHandler>();

// ---- Service registrations ----
builder.Services.AddScoped<FeeSeeder>();
builder.Services.AddScoped<ICallerPermissions, HttpCallerPermissions>();
builder.Services.AddScoped<FeeSetupService>();
builder.Services.AddScoped<DemandService>();
builder.Services.AddScoped<ReceiptService>();
builder.Services.AddScoped<PortalFeeService>();

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
