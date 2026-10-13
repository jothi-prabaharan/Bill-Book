using Shared.Kernel.Security;
using System.Text;
using Customer.Repository;
using Customer.Repository.Clients;
using Customer.Repository.Repositories;
using Customer.Repository.Security;
using Customer.Api.Services;
using Shared.Kernel.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Secrets;
using Shared.Kernel.Internal;
using Shared.Kernel.Numbering;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddTransient<InternalKeyHandler>();

builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ITenantDatabaseResolver, TenantDatabaseResolver>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<SlaPolicyRepository>();
builder.Services.AddScoped<ServicePlanRepository>();
builder.Services.AddScoped<LoyaltyRepository>();
builder.Services.AddScoped<PortalTicketRepository>();
builder.Services.AddSingleton<IUnsubscribeTokenSigner, UnsubscribeTokenSigner>();
builder.Services.AddSingleton<ICampaignTrackingTokenSigner, CampaignTrackingTokenSigner>();
builder.Services.AddScoped<EmailTemplateRepository>();
builder.Services.AddScoped<EmailConsentRepository>();
builder.Services.AddScoped<AudienceRepository>();
builder.Services.AddScoped<CampaignRepository>();
builder.Services.AddScoped<AutomationRepository>();
builder.Services.AddEventPublisher(builder.Configuration);
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddScoped<RlsConnectionInterceptor>();
// Key Vault when KeyVault:Uri is set, configuration otherwise — and a
// startup failure in Production if neither, rather than serving requests off
// whatever configuration happens to hold. See SecretStoreRegistration.
builder.Services.AddSecretStore(builder.Configuration, builder.Environment);

// One shared tenant database now, so the connection string is fixed at
// startup rather than resolved per request.
builder.Services.AddDbContext<CustomerDbContext>((sp, options) =>
{
    options.UseNpgsql(
        sp.GetRequiredService<ITenantDatabaseResolver>().GetConnectionString(sp.GetService<ITenantContext>()?.CustomerId, sp.GetService<ITenantContext>()?.DatabaseName),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "cus"));
    options.AddInterceptors(
        sp.GetRequiredService<AuditSaveChangesInterceptor>(),
        sp.GetRequiredService<RlsConnectionInterceptor>());
});

// Every write endpoint runs in a transaction and rolls back unless the action
// succeeded, and every failure is translated through the SQLSTATE catalogue,
// recorded in CustomerDbContext's own ErrorLogs table, and answered with the exact
// error in Development or a curated sentence everywhere else. Registered as one
// call because half of it is worse than neither: transactions without the
// handler leak SQL to callers, the handler without transactions reports a
// failure that was partly applied.
builder.Services.AddBillBookReliability<CustomerDbContext>();

builder.Services.AddBillBookAuthentication(builder.Configuration);

// Default deny: a controller added later is authenticated because nobody did
// anything about it.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Contacts, over HTTP, because `con.Contacts` belongs to Master and this service
// does not reach into another service's DbContext (CLAUDE.md 8). Both things it
// is used for — proving a ContactId belongs to the caller's branch before a lead
// or a ticket references it, and creating a contact from a lead — are questions
// only Contacts can answer correctly, because only Contacts has the query filter
// and the RLS policy that scope the answer.
builder.Services.AddHttpClient<IContactsClient, ContactsClient>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Master:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddHttpClient<IReportingClient, ReportingClient>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Reporting:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddHttpClient<ITenantEnumerator, HttpTenantEnumerator>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Master:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddHostedService<CampaignSendingWorker>();
builder.Services.AddHostedService<DatabaseMigrationService>();

WebApplication app = builder.Build();

app.MapOpenApi().AllowAnonymous();

// First in the pipeline. A failure in authentication or in the tenant
// middleware below is answered in the product's shape rather than by Kestrel.
app.UseBillBookErrorHandling();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<TenantMiddleware>();

app.MapControllers();

app.Run();

string RequiredSetting(string key) =>
    builder.Configuration[key] is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException(
            $"{key} is not configured. Set it in appsettings.{{Environment}}.json or via the " +
            $"{key.Replace(':', '_').Replace("_", "__")} environment variable.");
