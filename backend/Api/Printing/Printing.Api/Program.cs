using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Printing.Api.Rendering;
using Printing.Api.Services;
using Printing.Repository;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Persistence;
using Shared.Kernel.Printing;
using Shared.Kernel.Secrets;
using Shared.Kernel.Security;
using Shared.Kernel.Tenancy;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

builder.Services.AddControllers();

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
// Key Vault when KeyVault:Uri is set, configuration otherwise — and a
// startup failure in Production if neither, rather than serving requests off
// whatever configuration happens to hold. See SecretStoreRegistration.
builder.Services.AddSecretStore(builder.Configuration, builder.Environment);

// One shared tenant database now, so the connection string is fixed at
// startup rather than resolved per request.
builder.Services.AddDbContext<PrintingDbContext>((sp, options) =>
{
    options.UseNpgsql(
        sp.GetRequiredService<ITenantDatabaseResolver>().GetConnectionString(sp.GetService<ITenantContext>()?.CustomerId),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "prt"));
    options.AddInterceptors(
        sp.GetRequiredService<AuditSaveChangesInterceptor>(),
        sp.GetRequiredService<RlsConnectionInterceptor>());
});

// Every write endpoint runs in a transaction and rolls back unless the action
// succeeded, and every failure is translated through the SQLSTATE catalogue,
// recorded in PrintingDbContext's own ErrorLogs table, and answered with the exact
// error in Development or a curated sentence everywhere else. Registered as one
// call because half of it is worse than neither: transactions without the
// handler leak SQL to callers, the handler without transactions reports a
// failure that was partly applied.
builder.Services.AddBillBookReliability<PrintingDbContext>();

builder.Services.AddBillBookAuthentication(builder.Configuration);

// Default deny: a controller added later is authenticated because nobody did
// anything about it.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// The template API and the render endpoint. The renderer holds no state and
// its sanitiser is thread-safe, so one instance serves every request — as it
// did in Master before templates moved here.
builder.Services.AddScoped<PrintTemplateService>();
builder.Services.AddScoped<PrintTemplateSeeder>();
builder.Services.AddSingleton<PrintRenderer>();

builder.Services.AddHostedService<DatabaseMigrationService>();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// First in the pipeline. A failure in authentication or in the tenant
// middleware below is answered in the product's shape rather than by Kestrel.
app.UseBillBookErrorHandling();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<TenantMiddleware>();

app.MapControllers();

app.Run();
