using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Notification.Worker.Email;
using Shared.Kernel.Messaging;
using Shared.Kernel.School;
using Shared.Kernel.Tenancy;

namespace Notification.Worker.Reminders;

/// <summary>What one branch's AMC renewal run did.</summary>
public sealed record AmcRenewalRunResult(int Sent, int NoEmail, bool Stopped);

/// <summary>
/// One branch's AMC renewal reminders for one day (S8, TK-68). MaintenanceContract says which
/// contracts are in their reminder window; each is written to the address on
/// the contract through <see cref="EmailRequestHandler"/> under a message id
/// fixed by branch, contract and end date, so a contract is reminded once per
/// term however many daily runs fall in its window. A renewed contract is a new
/// contract, with its own id and end date, and is reminded in its own turn.
/// </summary>
public sealed class AmcRenewalReminderRun
{
    private readonly ITenantContext _tenant;
    private readonly IAmcRenewals _renewals;
    private readonly EmailRequestHandler _mail;
    private readonly TimeProvider _clock;
    private readonly ILogger<AmcRenewalReminderRun> _log;

    public AmcRenewalReminderRun(
        ITenantContext tenant, IAmcRenewals renewals, EmailRequestHandler mail, TimeProvider clock, ILogger<AmcRenewalReminderRun> log)
    {
        _tenant = tenant;
        _renewals = renewals;
        _mail = mail;
        _clock = clock;
        _log = log;
    }

    public async Task<AmcRenewalRunResult> RunAsync(CancellationToken ct)
    {
        (Guid customerId, Guid orgId) = _tenant.Require();
        DateTimeOffset now = _clock.GetUtcNow();
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);

        IReadOnlyList<AmcRenewalDue>? due = await _renewals.DueAsync(customerId, orgId, today, ct);
        if (due is null)
        {
            _log.LogWarning("MaintenanceContract could not say which contracts are due in {OrgId}; tomorrow's run retries.", orgId);
            return new AmcRenewalRunResult(0, 0, Stopped: true);
        }

        int sent = 0;
        int noEmail = 0;
        foreach (AmcRenewalDue contract in due)
        {
            if (string.IsNullOrWhiteSpace(contract.ReminderEmail))
            {
                noEmail++;
                continue;
            }

            EmailHandleOutcome outcome = await _mail.HandleAsync(Request(customerId, orgId, contract, today, now), ct);
            if (outcome == EmailHandleOutcome.NoMailbox)
            {
                _log.LogWarning("No mailbox to send AMC reminders from for {OrgId}; stopping this run.", orgId);
                return new AmcRenewalRunResult(sent, noEmail, Stopped: true);
            }

            if (outcome == EmailHandleOutcome.Sent)
            {
                sent++;
            }
        }

        return new AmcRenewalRunResult(sent, noEmail, Stopped: false);
    }

    /// <summary>
    /// The dedupe key for one contract's reminder: branch, contract and end date.
    /// Hashed so it always fits the 64 characters <c>ntf.ProcessedMessages</c> keeps.
    /// </summary>
    public static string MessageIdFor(Guid orgId, long contractId, DateOnly endDate)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"amc-renewal|{orgId:N}|{contractId}|{endDate:yyyyMMdd}"));
        return "amc-" + Convert.ToHexString(hash)[..60].ToLowerInvariant();
    }

    private static EmailRequested Request(Guid customerId, Guid orgId, AmcRenewalDue contract, DateOnly today, DateTimeOffset now)
    {
        string ends = contract.EndDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        int days = contract.EndDate.DayNumber - today.DayNumber;
        string when = days switch
        {
            0 => "today",
            1 => "tomorrow",
            _ => $"in {days} days",
        };
        string value = contract.ContractValue.ToString("N2", CultureInfo.InvariantCulture);

        return new EmailRequested
        {
            MessageId = MessageIdFor(orgId, contract.AmcContractId, contract.EndDate),
            RequestedAt = now,
            CustomerId = customerId,
            ToEmail = contract.ReminderEmail!,
            Subject = $"AMC renewal due: contract {contract.ContractNo} ends {ends}",
            HtmlBody = $"<p>The maintenance contract <strong>{WebUtility.HtmlEncode(contract.ContractNo)}</strong> with "
                + $"{WebUtility.HtmlEncode(contract.VendorName)} ends {when}, on {ends}.</p>"
                + $"<p>Its value was {value}. Renew it before then to keep its assets covered.</p>",
            TextBody = $"The maintenance contract {contract.ContractNo} with {contract.VendorName} ends {when}, on {ends}.\n\n"
                + $"Its value was {value}. Renew it before then to keep its assets covered.",
        };
    }
}
