using Microsoft.EntityFrameworkCore;
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
using Azure.Identity;
using Azure.Messaging.ServiceBus;

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
                client.BaseAddress = new Uri(config["Master:BaseUrl"] ?? "http://localhost:4504/");
            }).AddHttpMessageHandler<InternalKeyHandler>();

            services.AddDbContext<TenantDbContext>((sp, options) =>
            {
                options.UseNpgsql(connectionString);
                options.AddInterceptors(
                    sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                    sp.GetRequiredService<RlsConnectionInterceptor>());
            });

            services.AddBillBookWorkerErrorAudit<TenantDbContext>();

            // The master database for RateSync
            var adminConnectionString = config.GetConnectionString("AdminDatabase") ?? "Host=localhost;Database=billbook_master;Username=postgres;Password=postgres";
            services.AddDbContext<Master.Repository.AdminDbContext>((sp, options) =>
            {
                options.UseNpgsql(adminConnectionString);
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
