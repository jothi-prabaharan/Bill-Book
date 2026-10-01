using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using BillBook.Worker.Notification.Email;
using Shared.Kernel.Messaging;

namespace BillBook.Worker.Notification.Reminders;

public interface IComplianceReminders
{
    Task<IReadOnlyList<ComplianceReminderItem>> ListDueAsync(DateOnly? asOf, CancellationToken ct);
}

public sealed record ComplianceReminderItem(
    Guid CustomerId,
    Guid OrgId,
    string OrgName,
    string FormName,
    string Category,
    string PeriodName,
    string PeriodKey,
    DateOnly DueDate,
    int DaysRemaining,
    string RecipientEmail,
    string RecipientName);

public sealed class HttpComplianceReminders : IComplianceReminders
{
    private readonly HttpClient _http;

    public HttpComplianceReminders(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<ComplianceReminderItem>> ListDueAsync(DateOnly? asOf, CancellationToken ct)
    {
        string url = asOf.HasValue
            ? $"internal/compliance/reminders?asOf={asOf.Value:yyyy-MM-dd}"
            : "internal/compliance/reminders";

        HttpResponseMessage res = await _http.GetAsync(url, ct);
        if (!res.IsSuccessStatusCode)
        {
            return [];
        }

        return await res.Content.ReadFromJsonAsync<List<ComplianceReminderItem>>(ct) ?? [];
    }
}

public sealed class ComplianceReminderRun
{
    private readonly IComplianceReminders _client;
    private readonly EmailRequestHandler _mailer;
    private readonly ILogger<ComplianceReminderRun> _logger;

    public ComplianceReminderRun(
        IComplianceReminders client, EmailRequestHandler mailer, ILogger<ComplianceReminderRun> logger)
    {
        _client = client;
        _mailer = mailer;
        _logger = logger;
    }

    public static string MessageIdFor(Guid orgId, string formName, string periodKey, DateOnly day, string email)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"comp-rem|{orgId:N}|{formName}|{periodKey}|{day:yyyyMMdd}|{email.ToLowerInvariant()}"));

        return "crm-" + Convert.ToHexString(hash)[..60].ToLowerInvariant();
    }

    public async Task<int> RunAsync(CancellationToken ct)
    {
        IReadOnlyList<ComplianceReminderItem> due = await _client.ListDueAsync(null, ct);
        int sent = 0;

        foreach (var item in due)
        {
            string messageId = MessageIdFor(item.OrgId, item.FormName, item.PeriodKey, item.DueDate, item.RecipientEmail);

            string body = $@"Dear {item.RecipientName},

This is an automated reminder that the statutory filing {item.FormName} ({item.Category}) for period {item.PeriodName} is due on {item.DueDate:dd MMMM yyyy} ({item.DaysRemaining} days remaining).

Organization: {item.OrgName}

Please ensure the necessary data and returns are prepared in RetailErp.

Regards,
RetailErp Compliance System";

            var request = new EmailRequested
            {
                MessageId = messageId,
                RequestedAt = DateTimeOffset.UtcNow,
                CustomerId = item.CustomerId,
                ToEmail = item.RecipientEmail,
                ToName = item.RecipientName,
                Subject = $"Statutory Filing Reminder: {item.FormName} due on {item.DueDate:dd MMM yyyy}",
                HtmlBody = $"<p>Dear {WebUtility.HtmlEncode(item.RecipientName)},</p>"
                    + $"<p>This is an automated reminder that the statutory filing <strong>{WebUtility.HtmlEncode(item.FormName)}</strong> ({WebUtility.HtmlEncode(item.Category)}) for period <strong>{WebUtility.HtmlEncode(item.PeriodName)}</strong> is due on <strong>{item.DueDate:dd MMMM yyyy}</strong> ({item.DaysRemaining} days remaining).</p>"
                    + $"<p>Organization: {WebUtility.HtmlEncode(item.OrgName)}</p>"
                    + "<p>Please ensure the necessary data and returns are prepared in RetailErp.</p>",
            };

            EmailHandleOutcome outcome = await _mailer.HandleAsync(request, ct);
            if (outcome == EmailHandleOutcome.Sent)
            {
                sent++;
            }
        }

        return sent;
    }
}

