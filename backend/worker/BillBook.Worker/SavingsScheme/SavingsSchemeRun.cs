using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using BillBook.Worker.Notification.Email;
using BillBook.Worker.Notification.Reminders;
using Shared.Kernel.Documents;
using Shared.Kernel.Messaging;

namespace BillBook.Worker.SavingsScheme;

/// <summary>What one branch's savings scheme run did.</summary>
public sealed record SavingsSchemeRunResult(int Lapsed, int Sent, int NoEmail, bool Stopped);

/// <summary>
/// One branch's savings scheme day (TK-141). Accounting lapses what is overdue
/// past its grace days and says which instalments to remind; this sends those
/// reminders through TK-19's <see cref="EmailRequestHandler"/>.
///
/// Each reminder's message id is fixed by branch, instalment and kind — "due"
/// before the date, "overdue" after it — so a customer hears about each
/// instalment at most twice, however often the run repeats.
/// </summary>
public sealed class SavingsSchemeRun
{
    public const string AccountingClient = "savings-accounting";

    private readonly HttpClient _accounting;
    private readonly IContactEmails _emails;
    private readonly EmailRequestHandler _mail;
    private readonly TimeProvider _clock;

    public SavingsSchemeRun(IHttpClientFactory factory, IContactEmails emails, EmailRequestHandler mail, TimeProvider clock)
    {
        _accounting = factory.CreateClient(AccountingClient);
        _emails = emails;
        _mail = mail;
        _clock = clock;
    }

    public async Task<SavingsSchemeRunResult> RunAsync(Guid customerId, Guid orgId, CancellationToken ct)
    {
        using HttpResponseMessage response = await _accounting.PostAsJsonAsync(
            "internal/savings-schemes/run", new { customerId, orgId }, ct);
        response.EnsureSuccessStatusCode();

        RunResponse result = await response.Content.ReadFromJsonAsync<RunResponse>(ct)
            ?? new RunResponse(0, []);

        if (result.Reminders.Count == 0)
        {
            return new SavingsSchemeRunResult(result.Lapsed, 0, 0, Stopped: false);
        }

        IReadOnlyDictionary<long, ContactEmail>? addresses = await _emails.ForAsync(
            customerId, orgId, [.. result.Reminders.Select(r => r.ContactId).Distinct()], ct);

        if (addresses is null)
        {
            // Master unreachable: nobody is reminded this run; tomorrow's run asks again.
            return new SavingsSchemeRunResult(result.Lapsed, 0, 0, Stopped: true);
        }

        int sent = 0;
        int noEmail = 0;
        DateTimeOffset now = _clock.GetUtcNow();

        foreach (Reminder reminder in result.Reminders)
        {
            if (!addresses.TryGetValue(reminder.ContactId, out ContactEmail? to))
            {
                noEmail++;
                continue;
            }

            EmailHandleOutcome outcome = await _mail.HandleAsync(Email(customerId, orgId, reminder, to, now), ct);
            if (outcome == EmailHandleOutcome.NoMailbox)
            {
                return new SavingsSchemeRunResult(result.Lapsed, sent, noEmail, Stopped: true);
            }

            if (outcome == EmailHandleOutcome.Sent)
            {
                sent++;
            }
        }

        return new SavingsSchemeRunResult(result.Lapsed, sent, noEmail, Stopped: false);
    }

    /// <summary>Branch, instalment and kind, hashed to fit <c>ntf.ProcessedMessages</c>.</summary>
    public static string MessageIdFor(Guid orgId, long instalmentId, string kind)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"savings-scheme|{orgId:N}|{instalmentId}|{kind}"));
        return "ssi-" + Convert.ToHexString(hash)[..60].ToLowerInvariant();
    }

    private static EmailRequested Email(Guid customerId, Guid orgId, Reminder r, ContactEmail to, DateTimeOffset now)
    {
        string amount = r.Amount.ToString("N2", CultureInfo.InvariantCulture);
        string due = r.DueDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        string scheme = WebUtility.HtmlEncode(r.SchemeName);
        string number = WebUtility.HtmlEncode(r.EnrolmentNo ?? string.Empty);
        string name = WebUtility.HtmlEncode(to.DisplayName);
        bool late = r.Kind == "Overdue";

        string line = late
            ? $"Instalment {r.InstalmentNumber} of your {r.SchemeName} ({r.EnrolmentNo}), {amount}, was due on {due} and has not been paid yet."
            : $"Instalment {r.InstalmentNumber} of your {r.SchemeName} ({r.EnrolmentNo}), {amount}, is due on {due}.";

        return new EmailRequested
        {
            MessageId = MessageIdFor(orgId, r.SchemeInstalmentId, r.Kind),
            RequestedAt = now,
            CustomerId = customerId,
            ToEmail = to.Email,
            ToName = to.DisplayName,
            Subject = late ? $"Instalment overdue: {r.SchemeName}" : $"Instalment due: {r.SchemeName}",
            HtmlBody = $"<p>Dear {name},</p><p>"
                + (late
                    ? $"Instalment {r.InstalmentNumber} of your <strong>{scheme}</strong> ({number}), {amount}, was due on {due} and has not been paid yet. A scheme left unpaid past its grace period lapses and loses its bonus."
                    : $"Instalment {r.InstalmentNumber} of your <strong>{scheme}</strong> ({number}), {amount}, is due on {due}.")
                + "</p><p>If you have already paid, please ignore this message.</p>",
            TextBody = $"Dear {to.DisplayName},\n\n{line}\n\nIf you have already paid, please ignore this message.",
        };
    }

    private sealed record RunResponse(int Lapsed, List<Reminder> Reminders);

    private sealed record Reminder(
        long SchemeInstalmentId, long ContactId, string? EnrolmentNo, string SchemeName,
        int InstalmentNumber, decimal Amount, DateOnly DueDate, string Kind);
}
