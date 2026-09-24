using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notification.Worker.Email;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Messaging;
using Shared.Kernel.Tenancy;

namespace Notification.Worker.Reminders;

/// <summary>What one branch's run did.</summary>
public sealed record ReminderRunResult(int Sent, int AlreadyPaid, int NoEmail, bool Stopped);

/// <summary>
/// One branch's payment reminders for one day (TK-20). Runs in a scope whose
/// tenant is the branch, so every read goes through Sales' query filter and
/// row-level security — no <c>IgnoreQueryFilters</c>, and so no way for one
/// branch's profile to reach another branch's invoice.
///
/// <list type="number">
/// <item>Each active profile picks posted invoices at least its trigger's days
/// past due.</item>
/// <item>An invoice reminded under that profile within
/// <see cref="ReminderInterval"/> is left alone.</item>
/// <item>Accounting says what is still owed; a paid invoice gets nothing, and
/// if Accounting cannot be read nobody is reminded this run.</item>
/// <item>Master says where the customer is written to; one with no email is
/// skipped.</item>
/// <item>The mail goes through <see cref="EmailRequestHandler"/> — TK-19's path
/// — under a message id fixed by branch, invoice, profile and day, so a second
/// run on the same day finds it already sent and sends nothing; the
/// <c>ReminderLog</c> row is written in the same save as the others.</item>
/// </list>
/// </summary>
public sealed class PaymentReminderRun
{
    /// <summary>
    /// How long after one reminder the next may go under the same profile.
    /// <c>sal.ReminderProfiles</c> has no field for it, so it is one interval
    /// for every profile — recorded on TK-20 as a gap.
    /// </summary>
    public static readonly TimeSpan ReminderInterval = TimeSpan.FromDays(7);

    private readonly SalesDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IInvoiceSettlements _settlements;
    private readonly IContactEmails _emails;
    private readonly EmailRequestHandler _mail;
    private readonly TimeProvider _clock;
    private readonly ILogger<PaymentReminderRun> _log;

    public PaymentReminderRun(
        SalesDbContext db,
        ITenantContext tenant,
        IInvoiceSettlements settlements,
        IContactEmails emails,
        EmailRequestHandler mail,
        TimeProvider clock,
        ILogger<PaymentReminderRun> log)
    {
        _db = db;
        _tenant = tenant;
        _settlements = settlements;
        _emails = emails;
        _mail = mail;
        _clock = clock;
        _log = log;
    }

    public async Task<ReminderRunResult> RunAsync(CancellationToken ct)
    {
        (Guid customerId, Guid orgId) = _tenant.Require();
        DateTimeOffset now = _clock.GetUtcNow();
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);

        List<ReminderProfile> profiles = await _db.ReminderProfiles
            .Where(p => p.IsActive)
            .OrderBy(p => p.DaysOverdueTrigger)
            .ToListAsync(ct);

        // One candidate per (invoice, profile) still due a reminder.
        var candidates = new List<(Invoice Invoice, ReminderProfile Profile)>();

        foreach (ReminderProfile profile in profiles)
        {
            DateOnly dueBy = today.AddDays(-profile.DaysOverdueTrigger);
            DateTimeOffset since = now - ReminderInterval;

            List<Invoice> overdue = await _db.Invoices
                .AsNoTracking()
                .Where(i => i.Status == DocumentStatus.Posted
                    && i.DueDate != null
                    && i.DueDate <= dueBy
                    && !_db.ReminderLogs.Any(l => l.InvoiceId == i.InvoiceId
                        && l.ReminderProfileId == profile.ReminderProfileId
                        && l.SentAt > since))
                .OrderBy(i => i.DueDate)
                .ToListAsync(ct);

            candidates.AddRange(overdue.Select(i => (i, profile)));
        }

        if (candidates.Count == 0)
        {
            return new ReminderRunResult(0, 0, 0, Stopped: false);
        }

        List<long> invoiceIds = [.. candidates.Select(c => c.Invoice.InvoiceId).Distinct()];

        IReadOnlyDictionary<long, decimal>? outstanding =
            await _settlements.OutstandingAsync(customerId, orgId, invoiceIds, ct);

        if (outstanding is null)
        {
            _log.LogWarning("Accounting could not say what is owed in {OrgId}; no reminders this run.", orgId);
            return new ReminderRunResult(0, 0, 0, Stopped: true);
        }

        // An invoice Accounting does not know of is treated as unpaid in full:
        // the ledger has nothing settled against it.
        var owed = candidates
            .Where(c => outstanding.GetValueOrDefault(c.Invoice.InvoiceId, c.Invoice.TotalAmount) > 0)
            .ToList();

        int alreadyPaid = candidates.Count - owed.Count;

        if (owed.Count == 0)
        {
            return new ReminderRunResult(0, alreadyPaid, 0, Stopped: false);
        }

        IReadOnlyDictionary<long, Shared.Kernel.Documents.ContactEmail>? addresses = await _emails.ForAsync(
            customerId, orgId, [.. owed.Select(c => c.Invoice.ContactId).Distinct()], ct);

        if (addresses is null)
        {
            _log.LogWarning("Master could not say where to write in {OrgId}; no reminders this run.", orgId);
            return new ReminderRunResult(0, alreadyPaid, 0, Stopped: true);
        }

        int sent = 0;
        int noEmail = 0;
        bool stopped = false;

        foreach ((Invoice invoice, ReminderProfile profile) in owed)
        {
            if (!addresses.TryGetValue(invoice.ContactId, out Shared.Kernel.Documents.ContactEmail? to))
            {
                noEmail++;
                continue;
            }

            decimal due = outstanding.GetValueOrDefault(invoice.InvoiceId, invoice.TotalAmount);

            EmailHandleOutcome outcome = await _mail.HandleAsync(
                Request(customerId, orgId, invoice, profile, to, due, today, now), ct);

            if (outcome == EmailHandleOutcome.NoMailbox)
            {
                _log.LogWarning("No mailbox to send reminders from for {OrgId}; stopping this run.", orgId);
                stopped = true;
                break;
            }

            if (outcome is EmailHandleOutcome.Sent or EmailHandleOutcome.Duplicate)
            {
                _db.ReminderLogs.Add(new ReminderLog
                {
                    InvoiceId = invoice.InvoiceId,
                    ReminderProfileId = profile.ReminderProfileId,
                    SentAt = now,
                    NotificationType = "Email",
                });

                sent++;
            }
        }

        await _db.SaveChangesAsync(ct);

        return new ReminderRunResult(sent, alreadyPaid, noEmail, stopped);
    }

    /// <summary>
    /// The dedupe key for one reminder: branch, invoice, profile and day. A rerun
    /// on the same day — after a crash between the send and the log — gets the
    /// same key and so sends nothing.
    ///
    /// Hashed, because the four written out run past the 64 characters
    /// <c>ntf.ProcessedMessages</c> keeps; a SHA-256 of them is as fixed and as
    /// distinct, and always fits.
    /// </summary>
    public static string MessageIdFor(Guid orgId, long invoiceId, int profileId, DateOnly day)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"payment-reminder|{orgId:N}|{invoiceId}|{profileId}|{day:yyyyMMdd}"));

        return "rem-" + Convert.ToHexString(hash)[..60].ToLowerInvariant();
    }

    private static EmailRequested Request(
        Guid customerId,
        Guid orgId,
        Invoice invoice,
        ReminderProfile profile,
        Shared.Kernel.Documents.ContactEmail to,
        decimal due,
        DateOnly today,
        DateTimeOffset now)
    {
        string amount = due.ToString("N2", CultureInfo.InvariantCulture);
        string dueDate = invoice.DueDate!.Value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        string name = WebUtility.HtmlEncode(to.DisplayName);
        string number = WebUtility.HtmlEncode(invoice.DocumentNo);

        return new EmailRequested
        {
            MessageId = MessageIdFor(orgId, invoice.InvoiceId, profile.ReminderProfileId, today),
            RequestedAt = now,
            CustomerId = customerId,
            ToEmail = to.Email,
            ToName = to.DisplayName,
            Subject = $"Payment reminder: invoice {invoice.DocumentNo}",
            HtmlBody = $"<p>Dear {name},</p>"
                + $"<p>Invoice <strong>{number}</strong> was due on {dueDate}, and "
                + $"{WebUtility.HtmlEncode(invoice.CurrencyCode)} {amount} is still outstanding.</p>"
                + "<p>If you have already paid, please ignore this reminder.</p>",
            TextBody = $"Dear {to.DisplayName},\n\nInvoice {invoice.DocumentNo} was due on {dueDate}, and "
                + $"{invoice.CurrencyCode} {amount} is still outstanding.\n\n"
                + "If you have already paid, please ignore this reminder.",
        };
    }
}
