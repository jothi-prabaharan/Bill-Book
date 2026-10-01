using Customer.Entity.Enums;
using Customer.Entity.TableEntities;
using Customer.Repository;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Messaging;
using BillBook.Worker.Notification.Email;

namespace BillBook.Worker.ServiceReminders;

public class ServiceReminderWorker : BranchWorkerBase<ServiceReminderWorker>
{
    private readonly EmailRequestHandler _emailHandler;

    public ServiceReminderWorker(
        IServiceScopeFactory scopes,
        ILogger<ServiceReminderWorker> logger,
        IConfiguration config,
        EmailRequestHandler emailHandler)
        : base(scopes, logger, config)
    {
        _emailHandler = emailHandler;
    }

    protected override string JobName => "ServiceReminders";
    protected override TimeSpan DefaultInterval => TimeSpan.FromHours(1);

    protected override async Task ProcessBranchAsync(ActiveOrganization branch, IServiceScope scope, CancellationToken stoppingToken)
    {
        var db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        var masterDb = scope.ServiceProvider.GetRequiredService<ContactsDbContext>();
        
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Find plans that are due (or overdue) for a reminder
        var duePlans = await db.ServicePlans
            .Where(p => p.Status == ServicePlanStatus.Active &&
                        p.NextDueDate.AddDays(-p.LeadDays) <= today)
            .ToListAsync(stoppingToken);

        foreach (var plan in duePlans)
        {
            // Idempotency: plan ID and NextDueDate
            string messageId = $"SRV-{plan.ServicePlanId}-{plan.NextDueDate:yyyyMMdd}";

            // Contact Person lookup
            var defaultPerson = await masterDb.ContactPersons
                .Where(c => c.ContactId == plan.ContactId && c.IsDefault && c.IsActive)
                .FirstOrDefaultAsync(stoppingToken);

            if (defaultPerson == null || string.IsNullOrWhiteSpace(defaultPerson.Email))
            {
                continue;
            }

            var email = defaultPerson.Email.Trim().ToLowerInvariant();

            // Consent lookup
            var consent = await db.EmailConsents
                .Where(c => c.Email == email)
                .FirstOrDefaultAsync(stoppingToken);

            if (consent != null && consent.Status == ConsentStatus.Unsubscribed)
            {
                continue;
            }

            // A 'Services due' list on the dashboard, a per-branch reminder template, and a staff action to raise a work order. 
            // Email only; no SMS.
            // Wait, the template lookup:
                        var template = await db.EmailTemplates
                .Where(t => t.Name == "ServiceReminder" && t.IsActive)
                .FirstOrDefaultAsync(stoppingToken);

            string subject = template?.Subject ?? "Service Due Reminder";
            string body = template?.HtmlBody ?? "<p>Dear {defaultPerson.FirstName},</p><p>This is a reminder that a service is due on {plan.NextDueDate:MMM dd, yyyy}.</p>";

            // Replace tokens
            subject = subject.Replace("{{FirstName}}", defaultPerson.FirstName);
            body = body.Replace("{{FirstName}}", defaultPerson.FirstName)
                       .Replace("{{NextDueDate}}", plan.NextDueDate.ToString("MMM dd, yyyy"));

            var emailReq = new EmailRequested
            {
                MessageId = messageId,
                RequestedAt = DateTimeOffset.UtcNow,
                CustomerId = branch.CustomerId,
                ToEmail = email,
                ToName = $"{defaultPerson.FirstName} {defaultPerson.LastName}".Trim(),
                Subject = subject,
                HtmlBody = body
            };

            try
            {
                // Let the handler process it (handles deduping and sending)
                await _emailHandler.HandleAsync(emailReq, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send service reminder {MessageId}", messageId);
            }
        }
    }
}


