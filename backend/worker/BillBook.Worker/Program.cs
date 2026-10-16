using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Secrets;
using Shared.Kernel.Internal;
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

            services.AddSingleton(TimeProvider.System);
            services.AddScoped<TenantContext>();
            services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
            services.AddScoped<ICurrentUser, SystemUser>();
            services.AddScoped<AuditSaveChangesInterceptor>();
            services.AddScoped<RlsConnectionInterceptor>();
            
            services.AddTransient<InternalKeyHandler>();

            services.AddHttpClient<ITenantEnumerator, HttpTenantEnumerator>(client =>
            {
                client.BaseAddress = new Uri(config["Master:BaseUrl"] ?? "http://localhost:4511/");
            }).AddHttpMessageHandler<InternalKeyHandler>();

            services.AddBillBookWorkerErrorAudit<BillBook.Worker.Notification.Persistence.NotificationDbContext>();

            // The master database for RateSync
            var rawAdminConnectionString = config.GetConnectionString("AdminDatabase") ?? "Host=localhost;Database=billbook_master;Username=postgres;Password=postgres";
            var csb = new NpgsqlConnectionStringBuilder(rawAdminConnectionString);
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

            services.AddDbContext<Master.Repository.AdminDbContext>((sp, options) =>
            {
                options.UseNpgsql(csb.ConnectionString, npgsql => npgsql.CommandTimeout(180));
                options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
            });

            services.AddBackgroundJobs(config);
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
