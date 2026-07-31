using Gateway.Logging;
using Gateway.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// ------------------------------------------------------------ request logging
builder.Services.Configure<RequestLogOptions>(
    builder.Configuration.GetSection(RequestLogOptions.SectionName));

// The queue is a singleton and is constructed before options binding is available
// through DI, so its capacity is read directly from configuration.
RequestLogOptions logOptions =
    builder.Configuration.GetSection(RequestLogOptions.SectionName).Get<RequestLogOptions>()
    ?? new RequestLogOptions();

builder.Services.AddSingleton<IRequestLogQueue>(new RequestLogQueue(logOptions));

builder.Services.AddDbContext<GatewayDbContext>(options =>
    options.UseNpgsql(RequiredConnectionString("MasterDatabase")));

builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddHostedService<RequestLogWriter>();
builder.Services.AddHostedService<RequestLogPurger>();

WebApplication app = builder.Build();

// First in the pipeline: the duration it records should cover everything the
// gateway does, not just the proxying.
app.UseMiddleware<RequestLoggingMiddleware>();

// Minimal home/status page so hitting the gateway root shows something useful.
// Internal service endpoints (internal/*) are deliberately NOT routed here.
app.MapGet("/", () => Results.Content(
    """
    <!doctype html>
    <html lang="en">
      <head><meta charset="utf-8"><title>Bill-Book Gateway</title></head>
      <body style="font-family: system-ui; margin: 3rem auto; max-width: 40rem;">
        <h1>Bill-Book Gateway</h1>
        <p>YARP is up. Proxied routes:</p>
        <ul>
          <li><code>/api/auth, /api/users, /api/roles</code> → Identity (5001)</li>
          <li><code>/api/customers, /api/organizations, /api/smtp-settings</code> → Platform (5002)</li>
          <li><code>/api/master</code> → Master (5003)</li>
          <li><code>/api/accounts, /api/sub-accounts, /api/tax-masters</code> → Accounting (5004)</li>
        </ul>
        <p><a href="/health">/health</a></p>
      </body>
    </html>
    """, "text/html"));

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
