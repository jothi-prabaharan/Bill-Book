using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SavingsScheme.Entity.Enums;
using SavingsScheme.Repository;
using Shared.Kernel.Messaging;
using Shared.Kernel.Tenancy;
using BillBook.Worker.Notification.Email;
using Master.Repository;

namespace BillBook.Worker.SavingsScheme;

public class SavingsSchemeWorker : BranchWorkerBase<SavingsSchemeWorker>
{
    private readonly EmailRequestHandler _emailHandler;

    public SavingsSchemeWorker(
        IServiceScopeFactory scopes,
        ILogger<SavingsSchemeWorker> logger,
        IConfiguration config,
        EmailRequestHandler emailHandler)
        : base(scopes, logger, config)
    {
        _emailHandler = emailHandler;
    }

    protected override string JobName => "SavingsScheme";
    protected override TimeSpan DefaultInterval => TimeSpan.FromHours(12);

    protected override async Task ProcessBranchAsync(ActiveOrganization branch, IServiceScope scope, CancellationToken stoppingToken)
    {
        var db = scope.ServiceProvider.GetRequiredService<SavingsSchemeDbContext>();
        var masterDb = scope.ServiceProvider.GetRequiredService<ContactsDbContext>();
        
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Find instalments due soon (e.g., in 3 days) or overdue
        var dueInstalments = await db.SchemeInstalments
            .Include(i => i.SchemeEnrolment)
            .Where(i => i.SchemeEnrolment.Status == EnrolmentStatus.Active &&
                        i.PaidDate == null &&
                        i.DueDate <= today.AddDays(3))
            .ToListAsync(stoppingToken);

        foreach (var inst in dueInstalments)
        {
            // Idempotency
            string messageId = $"SAV-{inst.SchemeInstalmentId}-{today:yyyyMMdd}";

            var defaultPerson = await masterDb.ContactPersons
                .Where(c => c.ContactId == inst.SchemeEnrolment.ContactId && c.IsDefault && c.IsActive)
                .FirstOrDefaultAsync(stoppingToken);

            if (defaultPerson == null || string.IsNullOrWhiteSpace(defaultPerson.Email)) continue;

            string email = defaultPerson.Email.Trim().ToLowerInvariant();

            string subject = "Instalment Reminder";
            string body = $"<p>Dear {defaultPerson.FirstName},</p><p>Your scheme instalment of {inst.AmountDue} is due on {inst.DueDate:MMM dd, yyyy}.</p>";

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
                await _emailHandler.HandleAsync(emailReq, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send instalment reminder {MessageId}", messageId);
            }
        }
    }
}
