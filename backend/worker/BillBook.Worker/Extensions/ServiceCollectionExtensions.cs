using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel.Internal;

// Namespaces for Notification
using BillBook.Worker.Notification;
using BillBook.Worker.RecurringBilling;
using BillBook.Worker.SavingsScheme;
using BillBook.Worker.ServiceReminders;
using BillBook.Worker.Notification.Consumers;
using BillBook.Worker.Notification.Email;
using BillBook.Worker.Notification.Persistence;
using BillBook.Worker.Notification.Reminders;

// Namespaces for Costing
using BillBook.Worker.Costing.Consumers;
using Inventory.Repository.Repositories;
using Inventory.Repository;

// Namespaces for RateSync
using BillBook.Worker.RateSync;
using BillBook.Worker.RateSync.Ibja;
using BillBook.Worker.RateSync.Rbi;
using BillBook.Worker.RateSync.Consumers;
using Shared.Kernel.School;

namespace BillBook.Worker.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration config)
    {
        // --------------------------------------------------------
        // 1. Notification Worker Jobs
        // --------------------------------------------------------
        string? tenantDatabase = config.GetConnectionString("TenantDatabase");

        services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(
            tenantDatabase,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "ntf")));
        services.AddHostedService<NotificationMigrationService>();

        services.AddHttpClient<ISmtpDirectory, MasterSmtpDirectory>(client =>
        {
            client.BaseAddress = new Uri(config["Master:BaseUrl"] ?? "http://localhost:4511/");
        })
        .AddHttpMessageHandler<InternalKeyHandler>();

        services.AddSingleton<IMailTransport, SmtpMailTransport>();
        services.AddScoped<IProcessedMessageStore, ProcessedMessageStore>();
        services.AddScoped<EmailRequestHandler>();

        if (config["ServiceBus:Namespace"] is { Length: > 0 } ns)
        {
            services.AddSingleton(_ => new ServiceBusClient(ns, new DefaultAzureCredential()));
            services.AddHostedService<EmailRequestedConsumer>();
        }

        services.AddHttpClient<IContactEmails, HttpContactEmails>(client =>
        {
            client.BaseAddress = new Uri(config["Master:BaseUrl"] ?? "http://localhost:4511/");
        })
        .AddHttpMessageHandler<InternalKeyHandler>();

        services.AddHttpClient<IInvoiceSettlements, HttpInvoiceSettlements>(client =>
        {
            client.BaseAddress = new Uri(config["Accounting:BaseUrl"] ?? "http://localhost:4501/");
        })
        .AddHttpMessageHandler<InternalKeyHandler>();
        
        services.AddScoped<PaymentReminderRun>();
        services.AddHostedService<PaymentReminderWorker>();

        services.AddHttpClient<IAmcRenewals, HttpAmcRenewals>(client =>
        {
            client.BaseAddress = new Uri(config["MaintenanceContract:BaseUrl"] ?? "http://localhost:4510/");
        })
        .AddHttpMessageHandler<InternalKeyHandler>();
        
        services.AddScoped<AmcRenewalReminderRun>();
        services.AddHostedService<AmcRenewalReminderWorker>();

        services.AddHttpClient<IComplianceReminders, HttpComplianceReminders>(client =>
        {
            client.BaseAddress = new Uri(config["Master:BaseUrl"] ?? "http://localhost:4511/");
        })
        .AddHttpMessageHandler<InternalKeyHandler>();
        
        services.AddScoped<ComplianceReminderRun>();
        services.AddHostedService<ComplianceReminderWorker>();


        // --------------------------------------------------------
        // 2. Costing Worker Jobs
        // --------------------------------------------------------
        services.AddDbContext<InventoryDbContext>((sp, options) =>
        {
            options.UseNpgsql(
                tenantDatabase,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "inv"));
            options.AddInterceptors(
                sp.GetRequiredService<Shared.Kernel.Persistence.AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<Shared.Kernel.Tenancy.RlsConnectionInterceptor>());
        });

        services.AddScoped<CostingRepository>();
        services.AddScoped<WeightedAverageRecostingRepository>();
        
        services.AddHttpClient<IAccountingPeriodLock, AccountingPeriodLockRepository>(client =>
        {
            client.BaseAddress = new Uri(config["Accounting:BaseUrl"] ?? "http://localhost:4501/");
        })
        .AddHttpMessageHandler<InternalKeyHandler>();

        services.AddScoped<StockLedgerPosterRepository>();
        
        services.AddHttpClient<IAccountingLedger, AccountingLedgerRepository>(client =>
        {
            client.BaseAddress = new Uri(config["Accounting:BaseUrl"] ?? "http://localhost:4501/");
        })
        .AddHttpMessageHandler<InternalKeyHandler>();

        services.AddHostedService<CostingWorker>();


        // --------------------------------------------------------
        // 3. RateSync Worker Jobs
        // --------------------------------------------------------
        services.AddHttpClient<IReferenceRatePageSource, HttpReferenceRatePageSource>(client =>
        {
            client.BaseAddress = new Uri(config["Rbi:ReferenceRateUrl"] ?? "https://www.rbi.org.in/");
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; RetailErp-RateSync/1.0)");
        });

        services.AddHttpClient<IIbjaClient, HttpIbjaClient>(client =>
        {
            client.BaseAddress = new Uri(config["Ibja:ApiUrl"] ?? "https://ibjarates.com/API/GoldRates/");
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; RetailErp-RateSync/1.0)");
        });

        services.AddScoped<ExchangeRateSync>();
        services.AddScoped<IbjaMetalRateSync>();
        services.AddHostedService<RateSyncWorker>();

        // --------------------------------------------------------
        // 4. Recurring billing (TK-139): Sales and Purchase raise the documents
        // --------------------------------------------------------
        services.AddHttpClient(RecurringBillingClients.SalesClient, client =>
        {
            client.BaseAddress = new Uri(new Uri(config["Sales:BaseUrl"] ?? "http://localhost:4519/"), "internal/recurring-invoices/");
            client.Timeout = TimeSpan.FromMinutes(5);
        })
        .AddHttpMessageHandler<InternalKeyHandler>();

        services.AddHttpClient(RecurringBillingClients.PurchaseClient, client =>
        {
            client.BaseAddress = new Uri(new Uri(config["Purchase:BaseUrl"] ?? "http://localhost:4516/"), "internal/recurring-bills/");
            client.Timeout = TimeSpan.FromMinutes(5);
        })
        .AddHttpMessageHandler<InternalKeyHandler>();

        services.AddScoped<RecurringBillingClients>();
        services.AddHostedService<RecurringBillingWorker>();

        // --------------------------------------------------------
        // Service plan reminders (TK-140): Customer decides, this sends
        // --------------------------------------------------------
        services.AddHttpClient(ServiceReminderWorker.CustomerClient, client =>
        {
            client.BaseAddress = new Uri(config["Customer:BaseUrl"] ?? "http://localhost:4505/");
        })
        .AddHttpMessageHandler<InternalKeyHandler>();

        services.AddHostedService<ServiceReminderWorker>();

        // --------------------------------------------------------
        // 5. Savings schemes (TK-141): Accounting decides, this sends
        // --------------------------------------------------------
        services.AddHttpClient(SavingsSchemeRun.AccountingClient, client =>
        {
            client.BaseAddress = new Uri(config["Accounting:BaseUrl"] ?? "http://localhost:4501/");
        })
        .AddHttpMessageHandler<InternalKeyHandler>();

        services.AddScoped<SavingsSchemeRun>();
        services.AddHostedService<SavingsSchemeWorker>();

        services.AddHttpClient();

        return services;
    }
}







