using System.Net.Http.Json;
using BillBook.Worker.Notification.Email;
using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel.Messaging;
using Shared.Kernel.Tenancy;

namespace BillBook.Worker.ServiceReminders;

/// <summary>
/// Service plan reminders (TK-140), hourly by default.
///
/// Customer decides who is due, where they are written to, whether they
/// unsubscribed and what the email says (<c>internal/service-plans/reminders</c>);
/// this only sends, through TK-19's <see cref="EmailRequestHandler"/>. Each
/// message id is fixed by branch, plan and due date, so the hourly repeat sends
/// nothing twice. The worker reads no service's tables.
/// </summary>
public sealed class ServiceReminderWorker : BranchWorkerBase<ServiceReminderWorker>
{
    public const string CustomerClient = "service-reminders-customer";

    public ServiceReminderWorker(IServiceScopeFactory scopes, ILogger<ServiceReminderWorker> logger, IConfiguration config)
        : base(scopes, logger, config)
    {
    }

    protected override string JobName => "ServiceReminders";

    protected override TimeSpan DefaultInterval => TimeSpan.FromHours(1);

    protected override async Task ProcessBranchAsync(ActiveOrganization branch, IServiceScope scope, CancellationToken ct)
    {
        if (branch.BusinessType is null)
        {
            return;
        }

        HttpClient customer = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(CustomerClient);
        EmailRequestHandler mail = scope.ServiceProvider.GetRequiredService<EmailRequestHandler>();

        using HttpResponseMessage response = await customer.PostAsJsonAsync(
            "internal/service-plans/reminders", new { customerId = branch.CustomerId, orgId = branch.OrgId }, ct);
        response.EnsureSuccessStatusCode();

        Reminders? due = await response.Content.ReadFromJsonAsync<Reminders>(ct);
        if (due is null || due.Messages.Count == 0)
        {
            return;
        }

        int sent = 0;
        foreach (Message m in due.Messages)
        {
            EmailHandleOutcome outcome = await mail.HandleAsync(new EmailRequested
            {
                MessageId = m.MessageId,
                RequestedAt = DateTimeOffset.UtcNow,
                CustomerId = branch.CustomerId,
                ToEmail = m.ToEmail,
                ToName = m.ToName,
                Subject = m.Subject,
                HtmlBody = m.HtmlBody,
                TextBody = m.TextBody,
            }, ct);

            if (outcome == EmailHandleOutcome.NoMailbox)
            {
                _logger.LogWarning("No mailbox to send service reminders from for {OrgId}; stopping this run.", branch.OrgId);
                break;
            }

            if (outcome == EmailHandleOutcome.Sent)
            {
                sent++;
            }
        }

        if (sent > 0)
        {
            _logger.LogInformation(
                "Service reminders for {OrgId}: {Sent} sent, {NoEmail} without an email, {Unsubscribed} unsubscribed.",
                branch.OrgId, sent, due.NoEmail, due.Unsubscribed);
        }
    }

    private sealed record Reminders(List<Message> Messages, int NoEmail, int Unsubscribed, bool Stopped);

    private sealed record Message(
        string MessageId, long ServicePlanId, string ToEmail, string? ToName, string Subject, string HtmlBody, string? TextBody);
}
