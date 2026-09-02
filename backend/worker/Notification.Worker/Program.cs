using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Notification.Worker;
using Sales.Repository;
using Shared.Kernel.Tenancy;
using Microsoft.Extensions.Configuration;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ICurrentUser, WorkerCurrentUser>();
        services.AddDbContext<SalesDbContext>(options =>
        {
            options.UseNpgsql(hostContext.Configuration.GetConnectionString("TenantDatabase"));
        });
        services.AddHostedService<PaymentReminderWorker>();
    })
    .Build();

await host.RunAsync();
