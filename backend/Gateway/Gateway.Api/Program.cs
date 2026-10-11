using Gateway.Api;
using Gateway.Api.Logging;
using Gateway.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddConfigFilter<GatewayTimeoutConfigFilter>()
    .ConfigureHttpClient((context, handler) =>
    {
        handler.ConnectTimeout = TimeSpan.FromMinutes(3);
        handler.PooledConnectionIdleTimeout = TimeSpan.FromMinutes(3);
    });

// ------------------------------------------------------------ request logging
builder.Services.Configure<RequestLogOptions>(
    builder.Configuration.GetSection(RequestLogOptions.SectionName));

// The queue is a singleton and is constructed before options binding is available
// through DI, so its capacity is read directly from configuration.
RequestLogOptions logOptions =
    builder.Configuration.GetSection(RequestLogOptions.SectionName).Get<RequestLogOptions>()
    ?? new RequestLogOptions();

builder.Services.AddSingleton<IRequestLogQueue>(new RequestLogQueue(logOptions));

// The interceptor's two dependencies. Neither was registered, so resolving it
// would have thrown at the first request log rather than at startup — the
// gateway builds its context lazily, and the writer swallows what it cannot
// flush.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICurrentUser, GatewayUser>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();

// The interceptor has to be added to the context as well as registered.
// Registering it alone compiles, resolves and does nothing at all — every
// RequestLog would be written with its four audit columns null, which reads
// exactly like the seed data that is meant to be the only thing with a null
// CreatedBy.
builder.Services.AddDbContext<GatewayDbContext>((sp, options) =>
{
    options.UseNpgsql(RequiredConnectionString("AdminDatabase"));
    options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
});
// The Gateway proxies rather than serving controllers, so there is no
// transaction filter to add and no tenant schema to record into: failures are
// translated and logged, and the response carries a trace id with no error
// reference on it.
builder.Services.AddBillBookErrorHandling();

// ------------------------------------------------------------ cross-origin
//
// The web apps are served from their own origin and call this one, so the browser
// asks first. Without an answer every call from a deployed app fails before it
// leaves the browser — and the dev server never shows it, because it proxies /api
// on the page's own origin.
//
// Origins are named exactly, never "*": the API is called with a bearer token,
// and a wildcard would let any page on the internet script calls with a token it
// had lifted. No credentials mode, because nothing here uses cookies — the token
// travels in the Authorization header, which AllowAnyHeader admits.
//
// Accepts a JSON array or one comma-separated string, because an array is
// natural in appsettings and a single string is natural in an environment
// variable, which is how a container receives it.
const string CorsPolicy = "web-apps";

string[] allowedOrigins =
    builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() is { Length: > 0 } listed
        ? listed
        : (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        // Ten minutes of cached preflight. Every API call from a browser is
        // cross-origin, so without this each one costs two round trips.
        .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));
}

builder.Services.AddHostedService<RequestLogWriter>();
builder.Services.AddHostedService<RequestLogPurger>();

WebApplication app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    GatewayDbContext db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
    await StartupMigration.MigrateAsync(db.Database, app.Logger, "Gateway", CancellationToken.None);
}

// First in the pipeline: the duration it records should cover everything the
// gateway does, not just the proxying.
app.UseBillBookErrorHandling();

// Before request logging, so a preflight is answered here and never reaches the
// log: it carries no user, no tenant and no payload, and logging one per API call
// would double the table for nothing.
if (allowedOrigins.Length > 0)
{
    app.UseCors(CorsPolicy);
}

app.UseMiddleware<RequestLoggingMiddleware>();

// Serve static assets and default index.html from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// Serve the unified Swagger API documentation portal
app.MapGet("/swagger", () => Results.File(Path.Combine(app.Environment.WebRootPath, "swagger.html"), "text/html"));

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapReverseProxy();

app.Run();

// Settings with no safe default. Blank is treated as missing: appsettings.json ships
// every key present but empty so the shape is discoverable, which means a `??` fallback
// would never fire and a broken value would flow on silently instead.
string RequiredConnectionString(string name) =>
    builder.Configuration.GetConnectionString(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException(
            $"ConnectionStrings:{name} is not configured. Set it in appsettings.{{Environment}}.json " +
            $"or via the ConnectionStrings__{name} environment variable.");
