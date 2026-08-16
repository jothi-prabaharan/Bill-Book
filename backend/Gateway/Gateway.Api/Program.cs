using Gateway.Api.Logging;
using Gateway.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;
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
builder.Services.AddHostedService<RequestLogWriter>();
builder.Services.AddHostedService<RequestLogPurger>();

WebApplication app = builder.Build();

// First in the pipeline: the duration it records should cover everything the
// gateway does, not just the proxying.
app.UseMiddleware<RequestLoggingMiddleware>();

// Minimal home/status page so hitting the gateway root shows something useful.
// Internal service endpoints (internal/*) are deliberately NOT routed here.
string htmlTemplate =
    """
    <!DOCTYPE html>
    <html lang="en">
    <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>Bill-Book API Gateway</title>
        <style>
            :root {
                --bg-color: #f8fafc;
                --text-main: #0f172a;
                --text-muted: #64748b;
                --card-bg: #ffffff;
                --border-color: #e2e8f0;
                --accent-color: #0284c7;
                --success-color: #10b981;
                --hover-bg: #f1f5f9;
            }
            @media (prefers-color-scheme: dark) {
                :root {
                    --bg-color: #0f172a;
                    --text-main: #f8fafc;
                    --text-muted: #94a3b8;
                    --card-bg: #1e293b;
                    --border-color: #334155;
                    --accent-color: #38bdf8;
                    --hover-bg: #334155;
                }
            }
            * { box-sizing: border-box; margin: 0; padding: 0; }
            body {
                font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
                background-color: var(--bg-color);
                color: var(--text-main);
                line-height: 1.6;
                -webkit-font-smoothing: antialiased;
                padding: 2rem;
                display: flex;
                justify-content: center;
            }
            .container { max-width: 800px; width: 100%; display: flex; flex-direction: column; gap: 2rem; }
            header {
                display: flex; align-items: center; justify-content: space-between;
                padding-bottom: 1.5rem; border-bottom: 1px solid var(--border-color);
            }
            .header-content h1 { font-size: 1.875rem; font-weight: 600; letter-spacing: -0.025em; margin-bottom: 0.5rem; }
            .header-content p { color: var(--text-muted); font-size: 1.125rem; }
            .status-badge {
                display: inline-flex; align-items: center; gap: 0.5rem; padding: 0.5rem 1rem;
                background-color: var(--card-bg); border: 1px solid var(--border-color);
                border-radius: 9999px; font-size: 0.875rem; font-weight: 500;
                box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
            }
            .status-dot { width: 8px; height: 8px; background-color: var(--success-color); border-radius: 50%; position: relative; }
            .status-dot::after {
                content: ''; position: absolute; width: 100%; height: 100%;
                background-color: inherit; border-radius: inherit;
                animation: pulse 2s cubic-bezier(0.4, 0, 0.6, 1) infinite;
            }
            @keyframes pulse {
                0% { transform: scale(1); opacity: 1; }
                100% { transform: scale(3); opacity: 0; }
            }
            .routes-section h2 { font-size: 1.25rem; font-weight: 600; margin-bottom: 1.25rem; color: var(--text-main); }
            .routes-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 1rem; }
            .route-card {
                background-color: var(--card-bg); border: 1px solid var(--border-color);
                border-radius: 12px; padding: 1.5rem; transition: transform 0.2s ease, box-shadow 0.2s ease;
                box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
            }
            .route-card:hover { transform: translateY(-2px); box-shadow: 0 4px 6px rgba(0, 0, 0, 0.05); }
            .route-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; }
            .service-name { font-weight: 600; font-size: 1.125rem; }
            .port-badge {
                font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
                font-size: 0.75rem; background-color: var(--hover-bg); padding: 0.25rem 0.5rem;
                border-radius: 4px; color: var(--text-muted);
            }
            .path-list { list-style: none; display: flex; flex-direction: column; gap: 0.5rem; }
            .path-item {
                font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
                font-size: 0.875rem; color: var(--accent-color); background-color: var(--hover-bg);
                padding: 0.5rem; border-radius: 6px; word-break: break-all;
            }
            .swagger-btn {
                display: {SWAGGER_DISPLAY}; align-items: center; justify-content: center;
                gap: 0.5rem; margin-top: 1.25rem; width: 100%; padding: 0.625rem;
                background-color: var(--accent-color); color: #ffffff;
                text-decoration: none; border-radius: 8px; font-size: 0.875rem;
                font-weight: 600; transition: filter 0.2s, transform 0.1s;
            }
            .swagger-btn:hover { filter: brightness(1.1); transform: translateY(-1px); }
            .swagger-btn svg { width: 16px; height: 16px; }
            footer {
                margin-top: 2rem; padding-top: 1.5rem; border-top: 1px solid var(--border-color);
                display: flex; justify-content: space-between; align-items: center;
                color: var(--text-muted); font-size: 0.875rem;
            }
            .health-link {
                color: var(--text-muted); text-decoration: none; display: inline-flex;
                align-items: center; gap: 0.5rem; transition: color 0.2s;
            }
            .health-link:hover { color: var(--text-main); }
            .health-link svg { width: 16px; height: 16px; }
        </style>
    </head>
    <body>
        <div class="container">
            <header>
                <div class="header-content">
                    <h1>Bill-Book Gateway</h1>
                    <p>Central API Routing Service</p>
                </div>
                <div class="status-badge">
                    <span class="status-dot"></span>
                    YARP is Online
                </div>
            </header>
            <main class="routes-section">
                <h2>Proxied Routes</h2>
                <div class="routes-grid">
                    <div class="route-card">
                        <div class="route-header">
                            <span class="service-name">Accounting</span>
                            <span class="port-badge">Port 5001</span>
                        </div>
                        <ul class="path-list">
                            <li class="path-item">/api/accounts</li>
                            <li class="path-item">/api/journals</li>
                            <li class="path-item">/api/ledger</li>
                        </ul>
                        <a href="{ACCOUNTING_SWAGGER}" class="swagger-btn" target="_blank" title="View Swagger Docs">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path><polyline points="15 3 21 3 21 9"></polyline><line x1="10" y1="14" x2="21" y2="3"></line></svg>
                            Swagger Docs
                        </a>
                    </div>
                    <div class="route-card">
                        <div class="route-header">
                            <span class="service-name">Customer</span>
                            <span class="port-badge">Port 5002</span>
                        </div>
                        <ul class="path-list">
                            <li class="path-item">/api/customers</li>
                        </ul>
                        <a href="{CUSTOMER_SWAGGER}" class="swagger-btn" target="_blank" title="View Swagger Docs">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path><polyline points="15 3 21 3 21 9"></polyline><line x1="10" y1="14" x2="21" y2="3"></line></svg>
                            Swagger Docs
                        </a>
                    </div>
                    <div class="route-card">
                        <div class="route-header">
                            <span class="service-name">Inventory</span>
                            <span class="port-badge">Port 5003</span>
                        </div>
                        <ul class="path-list">
                            <li class="path-item">/api/items</li>
                            <li class="path-item">/api/stock</li>
                            <li class="path-item">/api/warehouses</li>
                        </ul>
                        <a href="{INVENTORY_SWAGGER}" class="swagger-btn" target="_blank" title="View Swagger Docs">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path><polyline points="15 3 21 3 21 9"></polyline><line x1="10" y1="14" x2="21" y2="3"></line></svg>
                            Swagger Docs
                        </a>
                    </div>
                    <div class="route-card">
                        <div class="route-header">
                            <span class="service-name">Master</span>
                            <span class="port-badge">Port 5004</span>
                        </div>
                        <ul class="path-list">
                            <li class="path-item">/api/master</li>
                            <li class="path-item">/api/users</li>
                            <li class="path-item">/api/roles</li>
                        </ul>
                        <a href="{MASTER_SWAGGER}" class="swagger-btn" target="_blank" title="View Swagger Docs">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path><polyline points="15 3 21 3 21 9"></polyline><line x1="10" y1="14" x2="21" y2="3"></line></svg>
                            Swagger Docs
                        </a>
                    </div>
                    <div class="route-card">
                        <div class="route-header">
                            <span class="service-name">Purchase</span>
                            <span class="port-badge">Port 5005</span>
                        </div>
                        <ul class="path-list">
                            <li class="path-item">/api/purchase</li>
                        </ul>
                        <a href="{PURCHASE_SWAGGER}" class="swagger-btn" target="_blank" title="View Swagger Docs">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path><polyline points="15 3 21 3 21 9"></polyline><line x1="10" y1="14" x2="21" y2="3"></line></svg>
                            Swagger Docs
                        </a>
                    </div>
                    <div class="route-card">
                        <div class="route-header">
                            <span class="service-name">Reporting</span>
                            <span class="port-badge">Port 5006</span>
                        </div>
                        <ul class="path-list">
                            <li class="path-item">/api/reports</li>
                        </ul>
                        <a href="{REPORTING_SWAGGER}" class="swagger-btn" target="_blank" title="View Swagger Docs">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path><polyline points="15 3 21 3 21 9"></polyline><line x1="10" y1="14" x2="21" y2="3"></line></svg>
                            Swagger Docs
                        </a>
                    </div>
                    <div class="route-card">
                        <div class="route-header">
                            <span class="service-name">Sales</span>
                            <span class="port-badge">Port 5007</span>
                        </div>
                        <ul class="path-list">
                            <li class="path-item">/api/sales</li>
                        </ul>
                        <a href="{SALES_SWAGGER}" class="swagger-btn" target="_blank" title="View Swagger Docs">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path><polyline points="15 3 21 3 21 9"></polyline><line x1="10" y1="14" x2="21" y2="3"></line></svg>
                            Swagger Docs
                        </a>
                    </div>
                </div>
            </main>
            <footer>
                <span>&copy; Bill-Book Internal API</span>
                <a href="/health" class="health-link">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M22 12h-4l-3 9L9 3l-3 9H2"></path>
                    </svg>
                    System Health
                </a>
            </footer>
        </div>
    </body>
    </html>
    """;

app.MapGet("/", () => 
{
    string htmlContent = htmlTemplate
        .Replace("{SWAGGER_DISPLAY}", app.Environment.IsProduction() ? "none" : "inline-flex")
        .Replace("{ACCOUNTING_SWAGGER}", builder.Configuration["SwaggerEndpoints:Accounting"] ?? "http://localhost:5001/swagger")
        .Replace("{CUSTOMER_SWAGGER}", builder.Configuration["SwaggerEndpoints:Customer"] ?? "http://localhost:5002/swagger")
        .Replace("{INVENTORY_SWAGGER}", builder.Configuration["SwaggerEndpoints:Inventory"] ?? "http://localhost:5003/swagger")
        .Replace("{MASTER_SWAGGER}", builder.Configuration["SwaggerEndpoints:Master"] ?? "http://localhost:5004/swagger")
        .Replace("{PURCHASE_SWAGGER}", builder.Configuration["SwaggerEndpoints:Purchase"] ?? "http://localhost:5005/swagger")
        .Replace("{REPORTING_SWAGGER}", builder.Configuration["SwaggerEndpoints:Reporting"] ?? "http://localhost:5006/swagger")
        .Replace("{SALES_SWAGGER}", builder.Configuration["SwaggerEndpoints:Sales"] ?? "http://localhost:5007/swagger");
        
    return Results.Content(htmlContent, "text/html");
});

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
