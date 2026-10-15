using Gateway.Api;
using Gateway.Api.Logging;
using Gateway.Repository;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
    options.UseNpgsql(RequiredConnectionString("AdminDatabase"), npgsql => npgsql.CommandTimeout(180));
    options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
});
// The Gateway proxies rather than serving controllers, so there is no
// transaction filter to add and no tenant schema to record into: failures are
// translated and logged, and the response carries a trace id with no error
// reference on it.
builder.Services.AddBillBookErrorHandling();
builder.Services.AddRequestTimeouts();

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

if (allowedOrigins.Length > 0 || builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
        }
    }));
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

app.MapGet("/api/gateway/services", (IConfiguration config) =>
{
    var services = new[]
    {
        new { id = "master", name = "Master", openApi = "/openapi/master/v1.json" },
        new { id = "accounting", name = "Accounting", openApi = "/openapi/accounting/v1.json" },
        new { id = "inventory", name = "Inventory", openApi = "/openapi/inventory/v1.json" },
        new { id = "sales", name = "Sales", openApi = "/openapi/sales/v1.json" },
        new { id = "purchase", name = "Purchase", openApi = "/openapi/purchase/v1.json" },
        new { id = "customer", name = "Customer", openApi = "/openapi/customer/v1.json" },
        new { id = "reporting", name = "Reporting", openApi = "/openapi/reporting/v1.json" },
        new { id = "printing", name = "Printing", openApi = "/openapi/printing/v1.json" },
        new { id = "hrm", name = "Employee (HRM)", openApi = "/openapi/hrm/v1.json" },
        new { id = "payroll", name = "Payroll", openApi = "/openapi/payroll/v1.json" },
        new { id = "timeleave", name = "Time and Leave", openApi = "/openapi/timeleave/v1.json" },
        new { id = "claims", name = "Claims", openApi = "/openapi/claims/v1.json" },
        new { id = "recruitment", name = "Recruitment", openApi = "/openapi/recruitment/v1.json" },
        new { id = "performance", name = "Performance", openApi = "/openapi/performance/v1.json" },
        new { id = "sis", name = "SIS", openApi = "/openapi/sis/v1.json" },
        new { id = "admission", name = "Admission", openApi = "/openapi/admission/v1.json" },
        new { id = "attendance", name = "Attendance", openApi = "/openapi/attendance/v1.json" },
        new { id = "fee", name = "Fee", openApi = "/openapi/fee/v1.json" },
        new { id = "facility", name = "Facility", openApi = "/openapi/facility/v1.json" },
        new { id = "workorder", name = "Work Order", openApi = "/openapi/workorder/v1.json" },
        new { id = "preventive", name = "Preventive Maintenance", openApi = "/openapi/preventive/v1.json" },
        new { id = "amc", name = "Maintenance Contract (AMC)", openApi = "/openapi/amc/v1.json" },
        new { id = "site", name = "Site Services", openApi = "/openapi/site/v1.json" }
    };

    return Results.Ok(services.Select(s =>
    {
        string? destAddress = config[$"ReverseProxy:Clusters:{s.id}:Destinations:d1:Address"];
        int port = 0;
        if (!string.IsNullOrEmpty(destAddress) && Uri.TryCreate(destAddress, UriKind.Absolute, out Uri? uri))
        {
            port = uri.Port;
        }
        return new { s.id, s.name, port, s.openApi };
    }));
});
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// Enable request body buffering so that downstream connection retries can rewind and replay payloads.
app.Use(async (context, next) =>
{
    context.Request.EnableBuffering();
    await next();
});

app.UseRequestTimeouts();

app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.Use((context, next) =>
    {
        ILogger logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Gateway.StartupRetry");
        return GatewayStartupRetryMiddleware.ExecuteAsync(context, next, logger);
    });
    proxyPipeline.UseSessionAffinity();
    proxyPipeline.UseLoadBalancing();
    proxyPipeline.UsePassiveHealthChecks();
});

app.Run();

// Settings with no safe default. Blank is treated as missing: appsettings.json ships
// every key present but empty so the shape is discoverable, which means a `??` fallback
// would never fire and a broken value would flow on silently instead.
string RequiredConnectionString(string name)
{
    string raw = builder.Configuration.GetConnectionString(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException(
            $"ConnectionStrings:{name} is not configured. Set it in appsettings.{{Environment}}.json " +
            $"or via the ConnectionStrings__{name} environment variable.");

    var csb = new NpgsqlConnectionStringBuilder(raw);
    if (csb.Timeout < 180) csb.Timeout = 180;
    if (csb.CommandTimeout < 180) csb.CommandTimeout = 180;
    if (csb.KeepAlive == 0 || csb.KeepAlive > 5) csb.KeepAlive = 5;
    csb.TcpKeepAlive = true;
    csb.TcpKeepAliveTime = 5;
    csb.TcpKeepAliveInterval = 1;
    csb.ConnectionIdleLifetime = 20;
    csb.ConnectionPruningInterval = 5;
    csb.ConnectionLifetime = 120;
    if (csb.MaxPoolSize > 15) csb.MaxPoolSize = 15;
    return csb.ConnectionString;
}
