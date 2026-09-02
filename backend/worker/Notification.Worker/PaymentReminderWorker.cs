using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sales.Repository;
using Shared.Kernel.Tenancy;

namespace Notification.Worker;

public class PaymentReminderWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PaymentReminderWorker> _logger;

    public PaymentReminderWorker(IServiceProvider serviceProvider, ILogger<PaymentReminderWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("PaymentReminderWorker running at: {time}", DateTimeOffset.Now);

            try
            {
                await ProcessRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred processing payment reminders.");
            }

            // Run on a daily schedule. Delaying for 24 hours.
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task ProcessRemindersAsync(CancellationToken ct)
    {
        // Background workers need their own scope because DbContext is scoped.
        using var scope = _serviceProvider.CreateScope();
        var salesDb = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
        
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // A full multi-tenant worker would typically iterate all tenants.
        // For demonstration within single execution context, we simulate fetching overdue invoices.
        // In a real system, we must ensure OrgId context is set or bypass it securely.
        
        // This is a naive implementation assuming the worker has a way to bypass RLS or iterate orgs.
        // In this specific task, we'll query assuming DbContext can see everything (or we iterate).
        // Since TenantDbContext restricts by OrgId, we'd normally need to iterate Orgs.
        // Let's assume we use IgnoreQueryFilters for the background worker finding candidates.

        var profiles = await salesDb.ReminderProfiles.IgnoreQueryFilters().Where(p => p.IsActive).ToListAsync(ct);
        
        foreach (var profile in profiles)
        {
            var triggerDate = today.AddDays(-profile.DaysOverdueTrigger);
            
            var overdueInvoices = await salesDb.Invoices
                .IgnoreQueryFilters()
                .Where(i => i.OrgId == profile.OrgId &&
                            i.DueDate <= triggerDate && 
                            i.Status == Shared.Kernel.Documents.DocumentStatus.Posted)
                .ToListAsync(ct);

            foreach (var invoice in overdueInvoices)
            {
                var recentLog = await salesDb.ReminderLogs
                    .IgnoreQueryFilters()
                    .Where(l => l.InvoiceId == invoice.InvoiceId && l.ReminderProfileId == profile.ReminderProfileId)
                    .OrderByDescending(l => l.SentAt)
                    .FirstOrDefaultAsync(ct);

                // If no reminder was sent, or it was sent more than X days ago (prevent spamming every day)
                if (recentLog == null || (DateTimeOffset.UtcNow - recentLog.SentAt).TotalDays > 7)
                {
                    _logger.LogInformation("Triggering reminder for Invoice {InvoiceId} in Org {OrgId}", invoice.InvoiceId, invoice.OrgId);

                    salesDb.ReminderLogs.Add(new Sales.Entity.TableEntities.ReminderLog
                    {
                        OrgId = invoice.OrgId,
                        CustomerId = invoice.CustomerId,
                        InvoiceId = invoice.InvoiceId,
                        ReminderProfileId = profile.ReminderProfileId,
                        SentAt = DateTimeOffset.UtcNow,
                        NotificationType = "Email"
                    });
                }
            }
        }

        await salesDb.SaveChangesAsync(ct);
    }
}
