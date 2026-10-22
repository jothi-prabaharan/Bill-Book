global using CustomerEntity = Master.Entity.TableEntities.Customer;
using Shared.Kernel.Security;
using System.Text;
using Master.Api.Services;
using Master.Repository.Repositories;
using Master.Repository.Clients;
using Master.Repository;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Secrets;
using Shared.Kernel.Messaging;
using Shared.Kernel.Internal;
using Shared.Kernel.Numbering;
using Shared.Kernel.Persistence;
using Shared.Kernel.Storage;
using Shared.Kernel.Tenancy;

// Master is four services in one: the reference data it always was, the tenant
// directory that was Platform, the users and roles that were Identity, and
// contacts.
//
// It is the only service with two databases. The first three share the master
// database and the mst schema — countries, customers, organizations, users. The
// fourth does not: a contact belongs to one branch's books and lives in that
// customer's own database, behind an OrgId filter and Postgres RLS. Two
// DbContexts, and the difference between them is the whole tenancy model.

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Fail at startup rather than on the first request that needs the missing
// registration. A service shipped without IBaseCurrencyProvider registered once —
// every endpoint that needed it would have thrown on its first call, and neither
// the build nor the tests could see it, because the build does not resolve DI and
// the tests construct their services by hand. This is what closes that gap:
// ValidateOnBuild walks every registration at startup, so a service asking for
// something nobody registered is a container that refuses to build.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()));

// Attaches the shared internal key to every service-to-service call, so a
// guarded endpoint is reachable by this service and by nothing else.
builder.Services.AddTransient<InternalKeyHandler>();

builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ITenantDatabaseResolver, TenantDatabaseResolver>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTenantMigration();

builder.Services.Configure<JwtOptionsRepository>(builder.Configuration.GetSection(JwtOptionsRepository.SectionName));

builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();

// ---- The master database: mst. ----
//
// One connection string, fixed. Nothing here is per-customer, so there is no
// tenant to resolve and no RLS to set — this is the database that says which
// other databases exist.
builder.Services.AddDbContext<AdminDbContext>((sp, options) =>
{
    // History in mst, where AdminDbContextFactory puts it for `dotnet ef`. In
    // public, a database made by `dotnet ef database update` looked unmigrated
    // to this host, which then failed on its first CREATE TABLE (TK-185).
    //
    // No EnableRetryOnFailure, here or on the contacts context: a retrying
    // strategy refuses every BeginTransactionAsync, and every write endpoint,
    // login included, opens one. Startup resilience is StartupMigration's (TK-186).
    options.UseNpgsql(
        sp.GetRequiredService<ITenantDatabaseResolver>().GetAdminConnectionString(),
        npgsql =>
        {
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "mst");
            npgsql.CommandTimeout(180);
        });
    options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
});

// ---- The shared tenant database: con. ----
//
// One shared tenant database now, so the connection string is fixed at
// startup rather than resolved per request — the same shape as AdminDbContext
// above. Still a second physical database from mst, and the interceptors are
// what keep it isolated per customer and per branch.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddScoped<RlsConnectionInterceptor>();

builder.Services.AddDbContext<ContactsDbContext>((sp, options) =>
{
    options.UseNpgsql(
        sp.GetRequiredService<ITenantDatabaseResolver>().GetConnectionString(sp.GetService<ITenantContext>()?.CustomerId, sp.GetService<ITenantContext>()?.DatabaseName),
        npgsql =>
        {
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "con");
            npgsql.CommandTimeout(180);
        });
    options.AddInterceptors(
        sp.GetRequiredService<AuditSaveChangesInterceptor>(),
        sp.GetRequiredService<RlsConnectionInterceptor>());
});

// ---- Auth. ----
builder.Services.AddScoped<IPasswordHasherRepository, BcryptPasswordHasher>();
builder.Services.AddScoped<ITokenRepository, JwtTokenRepository>();
builder.Services.AddScoped<IOtpRepository, OtpService>();
// WhatsApp and SMS codes (TK-196). No provider is chosen yet (D-66), so
// Development writes the code to the log and every other environment refuses
// to send: a trial signup then says it cannot send a code, and a password reset
// still answers its usual 200. A provider is one more implementation here.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IMobileMessageClient, LoggingMobileMessageClient>();
}
else
{
    builder.Services.AddScoped<IMobileMessageClient, UnconfiguredMobileMessageClient>();
}
builder.Services.AddScoped<AuthRepository>();
builder.Services.AddScoped<RoleRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<PlatformOperatorRepository>();
builder.Services.AddScoped<RateRepository>();

// ---- Tenant directory, signup and provisioning. ----
builder.Services.AddScoped<SignupRepository>();
builder.Services.AddScoped<DemoCustomerRepository>();
builder.Services.AddScoped<SampleDataRepository>();
// Picks the physical database a new customer's books go in, and claims the
// capacity for it with a guarded update. The sharded-tenancy work built the
// registry and the resolver and left this step out, which is why every signup
// died on the not-null DatabaseName column.
builder.Services.AddScoped<ITenantShardProvisioner, TenantShardProvisioner>();
builder.Services.AddScoped<ITenantDatabaseAllocator>(sp => new TenantDatabaseAllocatorRepository(
    sp.GetRequiredService<Master.Repository.AdminDbContext>(),
    sp.GetRequiredService<ILogger<TenantDatabaseAllocatorRepository>>(),
    sp.GetRequiredService<ITenantShardProvisioner>(),
    sp.GetRequiredService<IConfiguration>()));
builder.Services.AddScoped<OrgContextRepository>();
builder.Services.AddScoped<OrgCurrencyRepository>();
builder.Services.AddScoped<ConfigurationRepository>();
builder.Services.AddScoped<RestaurantSettingsRepository>();
builder.Services.AddScoped<BranchStaffRepository>();
builder.Services.AddScoped<FormatSettingsRepository>();
builder.Services.AddScoped<SmtpSettingsRepository>();
builder.Services.AddScoped<OrganizationRepository>();
builder.Services.AddScoped<LicenseRepository>();
builder.Services.AddScoped<PlanRepository>();
builder.Services.AddScoped<PlanPriceRepository>();
builder.Services.AddSingleton<ISecretProtectorRepository, AesSecretProtector>();

// A branch GSTIN is checked against its state's code, and so is a contact's.
// Both callers are in this service now, so it reads mst.States directly.
builder.Services.AddScoped<IStateDirectory, StateDirectoryRepository>();

// Currency reference data, and the owner user provisioning creates. Both were
// HTTP calls between Platform, Identity and Master; all three are here.
builder.Services.AddScoped<IMasterCurrenciesRepository, MasterCurrencies>();
builder.Services.AddScoped<IIdentityAdminRepository, InProcessIdentityAdmin>();

// Mail is queued and delivered on a background worker, so an SMTP round-trip
// never blocks a request. SmtpEmailSenderRepository is resolved by the worker only, and
// the decrypted password never leaves this process — which is what the old
// Identity-to-Platform hop existed to guarantee and now gets for free.
builder.Services.AddScoped<SmtpEmailSenderRepository>();

// Two paths (TK-19). With the notification worker running, a mail is an
// EmailRequested event and the worker sends it, surviving a restart here.
// Without it — local development, or a deployment where the worker is not
// running — the in-process queue sends it, as it always has.
if (EmailDelivery.UseWorker(builder.Configuration))
{
    builder.Services.AddScoped<IEmailSender, EventEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailQueue, InProcessEmailQueue>();
    builder.Services.AddScoped<IEmailSender, QueuedEmailSender>();
    builder.Services.AddHostedService<EmailDispatchWorker>();
}
builder.Services.AddSingleton<IProvisioningQueue, InProcessProvisioningQueue>();
builder.Services.AddHostedService<ProvisioningWorker>();
// Demo customers on start, only with Bootstrap:SeedDemoCustomers (TK-154).
builder.Services.AddHostedService<DemoCustomerBootstrapWorker>();
// The sample customer, one branch per business type, only with
// Bootstrap:SeedSampleData (TK-185).
builder.Services.AddHostedService<SampleDataBootstrapWorker>();
builder.Services.AddHostedService<HeadOfficeSeedWorker>();

// Runs database setup and EF core migrations on startup
builder.Services.AddHostedService<DatabaseMigrationService>();

// Writes each service's master data for a new organization. A named client
// rather than a typed one, because one seeder talks to several services and
// sets the base address per call. Still HTTP: those services own their own
// databases and this one must not reach into them.
builder.Services.AddHttpClient("seeding");
builder.Services.AddScoped<ITenantSeeder, HttpTenantSeeder>();

// ---- Contacts. ----
builder.Services.AddScoped<ContactRepository>();

// Revocable portal links and the one-hour sessions they open (TK-94).
builder.Services.AddScoped<PortalAccessRepository>();
builder.Services.AddScoped<IPortalLinkLifetime, ConfigurationPortalLinkLifetime>();
builder.Services.AddScoped<IDiscountLimitSetting, ConfigurationDiscountLimitSetting>();
builder.Services.AddPortalRateLimit();
// Ten code requests and checks a minute per address (TK-196).
builder.Services.AddOtpRateLimit();
builder.Services.AddScoped<ContactPersonRoleRepository>();
builder.Services.AddScoped<ContactAttachmentRepository>();

// ---- Menu. ----
builder.Services.AddScoped<MenuRepository>();

// Only Accounting writes ledger rows, so a contact's six sub-accounts are
// created by calling it. This one stays a real HTTP seam — Accounting is still
// a separate service with its own database.
// The approval engine (D-26, TK-99). Employee answers the employee approver kinds;
// with Employee:BaseUrl unset, those levels are simply unresolved.
builder.Services.AddScoped<ApprovalChainResolverRepository>();
builder.Services.AddScoped<ApprovalWorkflowRepository>();
builder.Services.AddScoped<CustomFieldRepository>();
builder.Services.AddScoped<Shared.Kernel.CustomFields.ICustomFieldDefinitions, LocalCustomFieldDefinitionsRepository>();
builder.Services.AddHttpClient<IEmployeeApproverClient, EmployeeApproverClient>(client =>
{
    if (builder.Configuration["Employee:BaseUrl"] is { Length: > 0 } hrm)
    {
        client.BaseAddress = new Uri(hrm);
    }
})
    .AddHttpMessageHandler<InternalKeyHandler>();

builder.Services.AddHttpClient<IAccountingSubAccounts, AccountingSubAccounts>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Accounting:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

// A contact's TDS section names one of Accounting's TDS sections (TK-117).
builder.Services.AddHttpClient<Shared.Kernel.Tax.ITdsSectionDirectory, Shared.Kernel.Tax.HttpTdsSectionDirectory>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Accounting:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

// Uploaded files. Blob Storage, Cloud Storage or local disk, chosen by which
// setting is present rather than by an environment name. The choice itself lives
// in Shared.Kernel because this block was previously copied between here and the
// other service that needed it.
builder.Services.AddFileStorage(builder.Configuration);

// Numbering. The series table belongs to Accounting, but the generator runs
// against this service's contacts context so a contact code is allocated inside
// the same transaction as the contact — a failed insert gives the number back.
builder.Services.Configure<NumberingOptions>(builder.Configuration.GetSection("Numbering"));

// The financial year start month is the branch's own, and this is the service
// that holds it. Other services read it from the internal org-context endpoint
// over HTTP and cache it; here it is a column away.
builder.Services.AddScoped<IFinancialYearProvider, InProcessFinancialYearProvider>();

builder.Services.AddScoped<INumberGenerator>(sp => new NumberGenerator(
    sp.GetRequiredService<ContactsDbContext>(),
    sp.GetRequiredService<IOptions<NumberingOptions>>(),
    sp.GetRequiredService<IFinancialYearProvider>()));

// Secrets and events, both chosen from configuration in Shared.Kernel.
//
// Key Vault when KeyVault:Uri is set, configuration otherwise — and a startup
// failure in Production if neither. Master is the one service that *writes* a
// secret (the SMTP-password key), and only the vault-backed store can: the
// configuration store refuses rather than pretending, which is why the
// in-memory dictionary that used to stand here was worse than nothing — it
// accepted every write and lost it on the next restart.
//
// Events go to Service Bus when ServiceBus:Namespace is set, and are logged and
// dropped otherwise.
builder.Services.AddSecretStore(builder.Configuration, builder.Environment);
builder.Services.AddEventPublisher(builder.Configuration);

// This service mints the tokens as well as validating them, so the signing key
// is the one every other service is configured with. Never fall back to a
// constant: every deployment sharing a key means tokens minted anywhere are
// accepted everywhere.
JwtOptionsRepository jwt = builder.Configuration.GetSection(JwtOptionsRepository.SectionName).Get<JwtOptionsRepository>() ?? new JwtOptionsRepository();
if (string.IsNullOrWhiteSpace(jwt.SigningKey))
{
    throw new InvalidOperationException(
        "Jwt:SigningKey is not configured. Set it in appsettings.{Environment}.json or via the " +
        "Jwt__SigningKey environment variable.");
}

// Two DbContexts, so two transactions per write request, settled independently —
// mst and con are different physical databases and there is no two-phase commit
// here. An operation writing to both can still half-succeed; that is a reason to
// not write to both in one action, not a reason to skip the transaction.
//
// The error log goes to ContactsDbContext because it is the tenant-scoped one:
// ErrorLog is an OrgScopedEntity and mst has no CustomerId to scope a row by.
// The consequence is stated on ErrorLog itself and it bites hardest here — a
// failed sign-in has no tenant yet, so it is logged and not recorded.
builder.Services.AddBillBookReliability<ContactsDbContext>();
builder.Services.AddBillBookReliability<AdminDbContext>(writesErrorLog: false);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
        };
    });

// Default deny: a controller added later is authenticated because nobody did
// anything about it. Endpoints that genuinely run before a token exists — signup,
// login, the country and state lists the signup form reads, the internal
// service-to-service ones — say so with [AllowAnonymous], which makes the
// exception visible on the controller itself.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

WebApplication app = builder.Build();

app.MapOpenApi().AllowAnonymous();

// First in the pipeline. A failure in authentication or in the tenant
// middleware below is answered in the product's shape rather than by Kestrel.
app.UseBillBookErrorHandling();

app.UseAuthentication();
app.UseAuthorization();

// After authentication so the claims are available, before any DbContext use.
// Only the contacts context needs it; it is harmless on the master one, which
// takes no tenant.
app.UseMiddleware<TenantMiddleware>();
app.UseTenantMigration<ContactsDbContext>();

// Only the portal session exchange names a policy (TK-94).
app.UseRateLimiter();

app.MapControllers();

app.Run();

// Settings with no safe default. Blank is treated as missing: appsettings.json ships
// every key present but empty so the shape is discoverable, which means a `??` fallback
// would never fire and a broken value would flow on silently instead.
string RequiredSetting(string key) =>
    builder.Configuration[key] is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException(
            $"{key} is not configured. Set it in appsettings.{{Environment}}.json or via the " +
            $"{key.Replace(':', '_').Replace("_", "__")} environment variable.");

#pragma warning disable CS8321
string RequiredConnectionString(string name) =>
    builder.Configuration.GetConnectionString(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException(
            $"ConnectionStrings:{name} is not configured. Set it in appsettings.{{Environment}}.json " +
            $"or via the ConnectionStrings__{name} environment variable.");


