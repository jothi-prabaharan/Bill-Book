using System.Text;
using Azure.Storage.Blobs;
using Master.Api.Services;
using Master.Repository;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shared.Kernel.Interfaces;
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

builder.Services.AddControllers();

// Attaches the shared internal key to every service-to-service call, so a
// guarded endpoint is reachable by this service and by nothing else.
builder.Services.AddTransient<InternalKeyHandler>();

builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();

// ---- The master database: mst. ----
//
// One connection string, fixed. Nothing here is per-customer, so there is no
// tenant to resolve and no RLS to set — this is the database that says which
// other databases exist.
builder.Services.AddDbContext<AdminDbContext>((sp, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("AdminDatabase"));
    options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
});

// ---- The per-customer database: con. ----
//
// Chosen per request from the caller's claims, so the context is built from the
// resolved tenant rather than a configuration value. The tenant directory it
// resolves through is this same service — see InProcessTenantDirectory — but the
// databases are still two, and the interceptors are what keep the second one
// isolated per branch.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddScoped<RlsConnectionInterceptor>();
builder.Services.AddScoped<ITenantConnectionResolver, TenantConnectionResolver>();
builder.Services.AddScoped<ITenantDirectory, InProcessTenantDirectory>();

builder.Services.AddDbContext<ContactsDbContext>((sp, options) =>
{
    ITenantContext tenant = sp.GetRequiredService<ITenantContext>();
    string connectionString = tenant.CustomerId is Guid customerId
        ? sp.GetRequiredService<ITenantConnectionResolver>()
            .ResolveAsync(customerId).GetAwaiter().GetResult()
        // Design-time and unauthenticated paths fall back to the configured value.
        : RequiredConnectionString("DesignTimeDatabase");

    options.UseNpgsql(connectionString);
    options.AddInterceptors(
        sp.GetRequiredService<AuditSaveChangesInterceptor>(),
        sp.GetRequiredService<RlsConnectionInterceptor>());
});

// ---- Auth. ----
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<RoleService>();
builder.Services.AddScoped<UserService>();

// ---- Tenant directory, signup and provisioning. ----
builder.Services.AddScoped<SignupService>();
builder.Services.AddScoped<OrgContextService>();
builder.Services.AddScoped<OrgCurrencyService>();
builder.Services.AddScoped<ConfigurationService>();
builder.Services.AddScoped<SmtpSettingsService>();
builder.Services.AddScoped<OrganizationService>();
builder.Services.AddSingleton<ISecretProtector, AesSecretProtector>();

// A branch GSTIN is checked against its state's code, and so is a contact's.
// Both callers are in this service now, so it reads mst.States directly.
builder.Services.AddScoped<IStateDirectory, StateDirectory>();

// Currency reference data, and the owner user provisioning creates. Both were
// HTTP calls between Platform, Identity and Master; all three are here.
builder.Services.AddScoped<IMasterCurrencies, MasterCurrencies>();
builder.Services.AddScoped<IIdentityAdmin, InProcessIdentityAdmin>();

// Mail is queued and delivered on a background worker, so an SMTP round-trip
// never blocks a request. SmtpEmailSender is resolved by the worker only, and
// the decrypted password never leaves this process — which is what the old
// Identity-to-Platform hop existed to guarantee and now gets for free.
builder.Services.AddScoped<SmtpEmailSender>();
builder.Services.AddSingleton<IEmailQueue, InProcessEmailQueue>();
builder.Services.AddScoped<IEmailSender, QueuedEmailSender>();
builder.Services.AddHostedService<EmailDispatchWorker>();
builder.Services.AddSingleton<IProvisioningQueue, InProcessProvisioningQueue>();
builder.Services.AddHostedService<ProvisioningWorker>();

// Runs database setup and EF core migrations on startup
builder.Services.AddHostedService<DatabaseMigrationService>();

// Writes each service's master data for a new organization. A named client
// rather than a typed one, because one seeder talks to several services and
// sets the base address per call. Still HTTP: those services own their own
// databases and this one must not reach into them.
builder.Services.AddHttpClient("seeding");
builder.Services.AddScoped<ITenantSeeder, HttpTenantSeeder>();

// ---- Contacts. ----
builder.Services.AddScoped<ContactService>();
builder.Services.AddScoped<ContactPersonRoleService>();
builder.Services.AddScoped<ContactAttachmentService>();

// Only Accounting writes ledger rows, so a contact's six sub-accounts are
// created by calling it. This one stays a real HTTP seam — Accounting is still
// a separate service with its own database.
builder.Services.AddHttpClient<IAccountingSubAccounts, AccountingSubAccounts>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Accounting:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

// Uploaded files. Blob storage when a connection string is configured, local
// disk otherwise — chosen by whether the setting is present rather than by an
// environment name, so a developer can point at real storage without pretending
// to be Production and a deployment cannot silently fall back to a disk that
// disappears with the container.
if (builder.Configuration["Storage:ConnectionString"] is { Length: > 0 } storageConnection)
{
    string containerName = builder.Configuration["Storage:Container"] ?? "documents";

    builder.Services.AddSingleton<IFileStorage>(_ =>
    {
        var container = new BlobContainerClient(storageConnection, containerName);

        // Created on startup rather than per upload: it is one call, it is
        // idempotent, and the alternative is every first upload in a fresh
        // deployment failing on a container nobody made.
        container.CreateIfNotExists();

        return new AzureBlobFileStorage(container);
    });
}
else
{
    builder.Services.AddSingleton<IFileStorage, LocalDiskFileStorage>();
}

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

// Dev infrastructure — swap for Key Vault / Service Bus in production.
builder.Services.AddSingleton<ISecretStore, InMemorySecretStore>();
builder.Services.AddSingleton<IEventPublisher, LoggingEventPublisher>();

// This service mints the tokens as well as validating them, so the signing key
// is the one every other service is configured with. Never fall back to a
// constant: every deployment sharing a key means tokens minted anywhere are
// accepted everywhere.
JwtOptions jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.SigningKey))
{
    throw new InvalidOperationException(
        "Jwt:SigningKey is not configured. Set it in appsettings.{Environment}.json or via the " +
        "Jwt__SigningKey environment variable.");
}

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

// After authentication so the claims are available, before any DbContext use.
// Only the contacts context needs it; it is harmless on the master one, which
// takes no tenant.
app.UseMiddleware<TenantMiddleware>();

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

string RequiredConnectionString(string name) =>
    builder.Configuration.GetConnectionString(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException(
            $"ConnectionStrings:{name} is not configured. Set it in appsettings.{{Environment}}.json " +
            $"or via the ConnectionStrings__{name} environment variable.");
