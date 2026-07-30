WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

WebApplication app = builder.Build();

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
          <li><code>/api/customers</code> → Platform (5002)</li>
          <li><code>/api/master</code> → Master (5003)</li>
        </ul>
        <p><a href="/health">/health</a></p>
      </body>
    </html>
    """, "text/html"));

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapReverseProxy();

app.Run();
