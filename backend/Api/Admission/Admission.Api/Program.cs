using System.Text.Json.Serialization;
using Admission.Api.Services;
using Admission.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Kernel.Contacts;
using Shared.Kernel.Interfaces;
using Shared.Kernel.School;
using Shared.Kernel.Internal;
using Shared.Kernel.Numbering;
using Shared.Kernel.Persistence;
using Shared.Kernel.Secrets;
using Shared.Kernel.Security;
using Shared.Kernel.Tenancy;

// Admission (S2, TK-62): enquiries, applications and their documents, and admitting a student. Schema adm, port 4516. The scaffold is Hrm's.
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

builder.Services.AddDbContext<AdmissionDbContext>((sp, options) =>
{
    options.UseNpgsql(
        sp.GetRequiredService<ITenantDatabaseResolver>().GetConnectionString(sp.GetService<ITenantContext>()?.CustomerId),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "adm"));
    options.AddInterceptors(
        sp.GetRequiredService<AuditSaveChangesInterceptor>(),
        sp.GetRequiredService<RlsConnectionInterceptor>());
});

// Every write in a transaction, every failure through the SQLSTATE catalogue
// into adm.ErrorLogs (hard rules 13 and 14).
builder.Services.AddBillBookReliability<AdmissionDbContext>();

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
    sp.GetRequiredService<AdmissionDbContext>(),
    sp.GetRequiredService<IOptions<NumberingOptions>>(),
    sp.GetRequiredService<IFinancialYearProvider>()));

// Admitting calls Master for the guardian contact and Sis for the student.
builder.Services.AddHttpClient<IContactDirectory, HttpContactDirectory>(client =>
{
    client.BaseAddress = new Uri(MasterUrl(builder.Configuration));
})
    .AddHttpMessageHandler<InternalKeyHandler>();
builder.Services.AddHttpClient<ISisClient, HttpSisClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Sis:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:4515");
})
    .AddHttpMessageHandler<InternalKeyHandler>();

// ---- Service registrations ----
builder.Services.AddScoped<AdmissionSeeder>();
builder.Services.AddScoped<EnquiryService>();
builder.Services.AddScoped<ApplicationService>();

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
