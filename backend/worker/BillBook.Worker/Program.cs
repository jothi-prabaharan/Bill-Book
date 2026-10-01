using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;
using BillBook.Worker;
using BillBook.Worker.Extensions;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting BillBook.Worker");

    IHost host = Host.CreateDefaultBuilder(args)
        .UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console())
        .ConfigureServices((hostContext, services) =>
        {
            var config = hostContext.Configuration;
            var connectionString = config.GetConnectionString("TenantDatabase") ?? "Host=localhost;Database=billbook_tenant;Username=postgres;Password=postgres";

            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService("BillBook.Worker"))
                .WithTracing(tracing => tracing
                    .AddSource("BillBook.Worker")
                    .AddOtlpExporter())
                .WithMetrics(metrics => metrics
                    .AddMeter("BillBook.Worker")
                    .AddRuntimeInstrumentation()
                    .AddOtlpExporter());

            services.AddScoped<TenantContext>();
            services.AddScoped<AuditSaveChangesInterceptor>();
            services.AddScoped<RlsConnectionInterceptor>();

            services.AddDbContext<TenantDbContext>((sp, options) =>
            {
                options.UseNpgsql(connectionString);
                options.AddInterceptors(
                    sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                    sp.GetRequiredService<RlsConnectionInterceptor>());
            });

            services.AddHostedService<BackgroundWorkerService>();
            services.AddBackgroundJobs();
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "BillBook.Worker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

