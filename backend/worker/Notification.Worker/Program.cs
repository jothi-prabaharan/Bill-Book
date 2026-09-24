using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Notification.Worker;
using Notification.Worker.Consumers;
using Notification.Worker.Email;
using Notification.Worker.Persistence;
using Sales.Repository;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        IConfiguration config = hostContext.Configuration;
        string? tenantDatabase = config.GetConnectionString("TenantDatabase");

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ICurrentUser, WorkerCurrentUser>();
        services.AddDbContext<SalesDbContext>(options => options.UseNpgsql(tenantDatabase));

        // The worker's own schema: which email message ids have been sent, so a
        // redelivery sends nothing (TK-19). Migrated first, before any consumer.
        services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(
            tenantDatabase,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "ntf")));
        services.AddHostedService<NotificationMigrationService>();

        // Email (TK-19): the customer's mailbox is Master's to resolve, over its
        // internal API; the SMTP conversation is Shared.Kernel's SmtpMailer.
        services.AddTransient<InternalKeyHandler>();
        services.AddHttpClient<ISmtpDirectory, MasterSmtpDirectory>(client =>
        {
            client.BaseAddress = new Uri(config["Master:BaseUrl"] ?? "http://localhost:4504/");
        })
            .AddHttpMessageHandler<InternalKeyHandler>();
        services.AddSingleton<IMailTransport, SmtpMailTransport>();
        services.AddScoped<IProcessedMessageStore, ProcessedMessageStore>();
        services.AddScoped<EmailRequestHandler>();

        // Only with a broker. Without one Master sends in process and there is
        // nothing to consume — which is local development and any deployment
        // that has not switched Notification:EmailWorker on in Master.
        if (config["ServiceBus:Namespace"] is { Length: > 0 } ns)
        {
            services.AddSingleton(_ => new ServiceBusClient(ns, new DefaultAzureCredential()));
            services.AddHostedService<EmailRequestedConsumer>();
        }

        services.AddHostedService<PaymentReminderWorker>();
    })
    .Build();

await host.RunAsync();
