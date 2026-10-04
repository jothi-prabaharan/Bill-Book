using System.Net;
using System.Security.Cryptography;
using System.Text;
using Customer.Entity.Enums;
using Customer.Entity.Models;
using Customer.Entity.TableEntities;
using Customer.Repository.Clients;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Customer;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Customer.Repository.Repositories;

/// <summary>
/// Service plans and customer reminders (TK-140).
///
/// <list type="bullet">
/// <item><b>Completing a service moves the next due date</b> one interval on
/// from the day it was done, and ends the plan once that passes its end
/// date.</item>
/// <item><b>Reminders are decided here and only sent by the worker.</b> The run
/// picks active plans inside their lead days, asks Master where each customer is
/// written to, drops anyone who unsubscribed, and words each message from the
/// branch's own <c>Service reminder</c> template. Each message id is fixed by
/// branch, plan and due date, so a customer hears once per service however
/// often the run repeats.</item>
/// <item><b>The staff action raises a support ticket</b> for the visit (D-48's
/// default): it carries the customer, an assignee and the branch's SLA, and it
/// is this service's own document.</item>
/// </list>
/// </summary>
public sealed class ServicePlanRepository
{
    /// <summary>The per-branch template's name, as seeded and as looked up.</summary>
    public const string TemplateName = "Service reminder";

    public const string DefaultSubject = "Your {{plan}} is due on {{due_date}}";

    public const string DefaultHtmlBody =
        "<p>Dear {{name}},</p><p>Your <strong>{{plan}}</strong> is due on {{due_date}}. Reply to this email or call us to book a visit.</p>";

    public const string DefaultTextBody =
        "Dear {{name}},\n\nYour {{plan}} is due on {{due_date}}. Reply to this email or call us to book a visit.";

    private readonly CustomerDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IContactsClient _contacts;
    private readonly SlaPolicyRepository _sla;
    private readonly TimeProvider _clock;

    public ServicePlanRepository(
        CustomerDbContext db, ITenantContext tenant, IContactsClient contacts, SlaPolicyRepository sla, TimeProvider clock)
    {
        _db = db;
        _tenant = tenant;
        _contacts = contacts;
        _sla = sla;
        _clock = clock;
    }

    private DateOnly Today => DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);

    /// <summary>One interval on from <paramref name="from"/>.</summary>
    public static DateOnly NextAfter(DateOnly from, ServicePlanInterval interval, int every) => interval switch
    {
        ServicePlanInterval.Daily => from.AddDays(every),
        ServicePlanInterval.Weekly => from.AddDays(7 * every),
        ServicePlanInterval.Monthly => from.AddMonths(every),
        ServicePlanInterval.Yearly => from.AddYears(every),
        _ => throw new ArgumentOutOfRangeException(nameof(interval), interval, "Unknown interval."),
    };

    /// <summary>
    /// Plans, soonest due first. <paramref name="dueWithinDays"/> narrows to the
    /// active plans due within that many days or already late — the dashboard's
    /// "Services due" list.
    /// </summary>
    public async Task<List<ServicePlanView>> ListAsync(
        ServicePlanStatus? status, int? dueWithinDays, long? contactId, CancellationToken ct)
    {
        DateOnly today = Today;
        IQueryable<ServicePlan> query = _db.ServicePlans.AsNoTracking();

        if (status is ServicePlanStatus wanted)
        {
            query = query.Where(p => p.Status == wanted);
        }

        if (dueWithinDays is int days)
        {
            DateOnly by = today.AddDays(days);
            query = query.Where(p => p.Status == ServicePlanStatus.Active && p.NextDueDate <= by);
        }

        if (contactId is long contact)
        {
            query = query.Where(p => p.ContactId == contact);
        }

        var rows = await (
            from p in query
            join t in _db.Tickets on p.LastTicketId equals t.TicketId into tickets
            from t in tickets.DefaultIfEmpty()
            orderby p.NextDueDate, p.ServicePlanId
            select new { Plan = p, TicketStatus = (TicketStatus?)t.Status })
            .ToListAsync(ct);

        return [.. rows.Select(r => View(r.Plan, r.TicketStatus, today))];
    }

    public async Task<ServicePlanView?> GetAsync(long planId, CancellationToken ct) =>
        (await ListAsync(null, null, null, ct)).FirstOrDefault(p => p.ServicePlanId == planId);

    public async Task<ServicePlanResult> CreateAsync(SaveServicePlanRequest request, CancellationToken ct)
    {
        if (Refusal(request) is string refused)
        {
            return new ServicePlanResult(ServicePlanOutcome.Invalid, Detail: refused);
        }

        if (!await _contacts.ExistsInCallerOrgAsync(request.ContactId, ct))
        {
            return new ServicePlanResult(ServicePlanOutcome.Invalid, Detail: "That customer is not in this branch.");
        }

        ServicePlan plan = new() { Status = ServicePlanStatus.Active };
        Apply(plan, request);
        plan.NextDueDate = request.NextDueDate ?? request.StartDate;
        EndIfPast(plan);

        _db.ServicePlans.Add(plan);
        await _db.SaveChangesAsync(ct);

        return new ServicePlanResult(ServicePlanOutcome.Ok, plan.ServicePlanId);
    }

    public async Task<ServicePlanResult> UpdateAsync(long planId, SaveServicePlanRequest request, CancellationToken ct)
    {
        ServicePlan? plan = await _db.ServicePlans.FirstOrDefaultAsync(p => p.ServicePlanId == planId, ct);
        if (plan is null)
        {
            return new ServicePlanResult(ServicePlanOutcome.NotFound);
        }

        if (Refusal(request) is string refused)
        {
            return new ServicePlanResult(ServicePlanOutcome.Invalid, Detail: refused);
        }

        if (plan.ContactId != request.ContactId && !await _contacts.ExistsInCallerOrgAsync(request.ContactId, ct))
        {
            return new ServicePlanResult(ServicePlanOutcome.Invalid, Detail: "That customer is not in this branch.");
        }

        Apply(plan, request);
        if (request.NextDueDate is DateOnly next)
        {
            plan.NextDueDate = next;
        }

        if (plan.Status != ServicePlanStatus.Ended)
        {
            EndIfPast(plan);
        }

        await _db.SaveChangesAsync(ct);
        return new ServicePlanResult(ServicePlanOutcome.Ok, planId);
    }

    public Task<ServicePlanResult> PauseAsync(long planId, CancellationToken ct) => SetStatusAsync(planId, ServicePlanStatus.Paused, ct);

    public Task<ServicePlanResult> ResumeAsync(long planId, CancellationToken ct) => SetStatusAsync(planId, ServicePlanStatus.Active, ct);

    public Task<ServicePlanResult> EndAsync(long planId, CancellationToken ct) => SetStatusAsync(planId, ServicePlanStatus.Ended, ct);

    private async Task<ServicePlanResult> SetStatusAsync(long planId, ServicePlanStatus to, CancellationToken ct)
    {
        ServicePlan? plan = await _db.ServicePlans.FirstOrDefaultAsync(p => p.ServicePlanId == planId, ct);
        if (plan is null)
        {
            return new ServicePlanResult(ServicePlanOutcome.NotFound);
        }

        if (plan.Status == ServicePlanStatus.Ended)
        {
            return new ServicePlanResult(ServicePlanOutcome.Conflict, Detail: "This plan has ended. Start a new one instead.");
        }

        plan.Status = to;
        await _db.SaveChangesAsync(ct);
        return new ServicePlanResult(ServicePlanOutcome.Ok, planId);
    }

    /// <summary>
    /// Records a service as done and moves the next due date one interval on
    /// from that day. A plan past its end date ends.
    /// </summary>
    public async Task<ServicePlanResult> CompleteAsync(long planId, CompleteServiceRequest request, CancellationToken ct)
    {
        ServicePlan? plan = await _db.ServicePlans.FirstOrDefaultAsync(p => p.ServicePlanId == planId, ct);
        if (plan is null)
        {
            return new ServicePlanResult(ServicePlanOutcome.NotFound);
        }

        if (plan.Status == ServicePlanStatus.Ended)
        {
            return new ServicePlanResult(ServicePlanOutcome.Conflict, Detail: "This plan has ended.");
        }

        if (request.CompletedOn > Today)
        {
            return new ServicePlanResult(ServicePlanOutcome.Invalid, Detail: "A service cannot be completed on a future date.");
        }

        plan.LastServicedOn = request.CompletedOn;
        plan.NextDueDate = NextAfter(request.CompletedOn, plan.Interval, plan.Every);
        EndIfPast(plan);

        await _db.SaveChangesAsync(ct);
        return new ServicePlanResult(ServicePlanOutcome.Ok, planId);
    }

    /// <summary>
    /// Raises a support ticket for the next visit (D-48), at the branch's SLA for
    /// a medium-priority ticket, and records it on the plan.
    /// </summary>
    public async Task<ServicePlanResult> RaiseTicketAsync(long planId, CancellationToken ct)
    {
        ServicePlan? plan = await _db.ServicePlans.FirstOrDefaultAsync(p => p.ServicePlanId == planId, ct);
        if (plan is null)
        {
            return new ServicePlanResult(ServicePlanOutcome.NotFound);
        }

        if (plan.Status != ServicePlanStatus.Active)
        {
            return new ServicePlanResult(ServicePlanOutcome.Conflict, Detail: "Only an active plan can raise a visit.");
        }

        if (plan.LastTicketId is long open
            && await _db.Tickets.AnyAsync(t => t.TicketId == open && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed, ct))
        {
            return new ServicePlanResult(ServicePlanOutcome.Conflict, open, "A visit is already open for this plan.");
        }

        Ticket ticket = new()
        {
            ContactId = plan.ContactId,
            Subject = Clip($"Service due: {plan.Name} on {plan.NextDueDate:dd MMM yyyy}", 200),
            Description = plan.Notes,
            Priority = TicketPriority.Medium,
            Channel = TicketChannel.Web,
            Status = TicketStatus.Open,
            SlaDueAt = await _sla.DueAtAsync(TicketPriority.Medium, _clock.GetUtcNow(), ct),
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync(ct);

        plan.LastTicketId = ticket.TicketId;
        await _db.SaveChangesAsync(ct);

        return new ServicePlanResult(ServicePlanOutcome.Ok, ticket.TicketId);
    }

    // ---- Reminders ---------------------------------------------------------

    /// <summary>
    /// The reminders due today: active plans whose next due date is within their
    /// lead days, addressed through Master and worded from the branch's
    /// template. Anyone unsubscribed or without an email is counted, not sent.
    /// </summary>
    public async Task<RunServiceRemindersResponse> RemindersAsync(DateOnly? asOf, CancellationToken ct)
    {
        DateOnly today = asOf ?? Today;
        RunServiceRemindersResponse response = new();

        // Due within the lead days, and not more than a week late: a reminder
        // for a service missed a month ago is noise, and a staff call is due.
        List<ServicePlan> due = await _db.ServicePlans.AsNoTracking()
            .Where(p => p.Status == ServicePlanStatus.Active
                && p.NextDueDate >= today.AddDays(-7)
                && p.NextDueDate.AddDays(-p.LeadDays) <= today)
            .OrderBy(p => p.NextDueDate)
            .ToListAsync(ct);

        if (due.Count == 0)
        {
            return response;
        }

        (Guid customerId, Guid orgId) = _tenant.Require();
        IReadOnlyDictionary<long, ContactEmail>? addresses =
            await _contacts.EmailsAsync(customerId, orgId, [.. due.Select(p => p.ContactId).Distinct()], ct);

        if (addresses is null)
        {
            response.Stopped = true;
            return response;
        }

        HashSet<string> unsubscribed = [.. await _db.EmailConsents.AsNoTracking()
            .Where(c => c.Status == ConsentStatus.Unsubscribed || c.Status == ConsentStatus.Bounced)
            .Select(c => c.Email.ToLower())
            .ToListAsync(ct)];

        EmailTemplate? template = await _db.EmailTemplates.AsNoTracking()
            .Where(t => t.Name == TemplateName && t.IsActive)
            .OrderByDescending(t => t.EmailTemplateId)
            .FirstOrDefaultAsync(ct);

        foreach (ServicePlan plan in due)
        {
            if (!addresses.TryGetValue(plan.ContactId, out ContactEmail? to) || string.IsNullOrWhiteSpace(to.Email))
            {
                response.NoEmail++;
                continue;
            }

            if (unsubscribed.Contains(to.Email.Trim().ToLowerInvariant()))
            {
                response.Unsubscribed++;
                continue;
            }

            response.Messages.Add(new ServiceReminderMessage
            {
                MessageId = MessageIdFor(orgId, plan.ServicePlanId, plan.NextDueDate),
                ServicePlanId = plan.ServicePlanId,
                ToEmail = to.Email.Trim(),
                ToName = to.DisplayName,
                Subject = Fill(template?.Subject ?? DefaultSubject, plan, to.DisplayName, html: false),
                HtmlBody = Fill(template?.HtmlBody ?? DefaultHtmlBody, plan, to.DisplayName, html: true),
                TextBody = template is null
                    ? Fill(DefaultTextBody, plan, to.DisplayName, html: false)
                    : template.TextBody is { } text ? Fill(text, plan, to.DisplayName, html: false) : null,
            });
        }

        return response;
    }

    /// <summary>Branch, plan and due date, hashed to fit <c>ntf.ProcessedMessages</c>.</summary>
    public static string MessageIdFor(Guid orgId, long planId, DateOnly due)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"service-reminder|{orgId:N}|{planId}|{due:yyyyMMdd}"));
        return "srv-" + Convert.ToHexString(hash)[..60].ToLowerInvariant();
    }

    /// <summary>Seeds the branch's editable reminder template, once.</summary>
    public async Task<int> SeedTemplateAsync(CancellationToken ct)
    {
        if (await _db.EmailTemplates.AnyAsync(t => t.Name == TemplateName, ct))
        {
            return 0;
        }

        _db.EmailTemplates.Add(new EmailTemplate
        {
            Name = TemplateName,
            Subject = DefaultSubject,
            HtmlBody = DefaultHtmlBody,
            TextBody = DefaultTextBody,
            IsActive = true,
        });

        await _db.SaveChangesAsync(ct);
        return 1;
    }

    // ---- Helpers -----------------------------------------------------------

    private static string Fill(string text, ServicePlan plan, string name, bool html)
    {
        string Encode(string value) => html ? WebUtility.HtmlEncode(value) : value;

        return text
            .Replace("{{name}}", Encode(name), StringComparison.OrdinalIgnoreCase)
            .Replace("{{first_name}}", Encode(name), StringComparison.OrdinalIgnoreCase)
            .Replace("{{plan}}", Encode(plan.Name), StringComparison.OrdinalIgnoreCase)
            .Replace("{{due_date}}", plan.NextDueDate.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
    }

    private static string? Refusal(SaveServicePlanRequest request) =>
        request.EndDate is DateOnly end && end < request.StartDate
            ? "The end date cannot be before the start date."
            : request.NextDueDate is DateOnly next && next < request.StartDate
                ? "The next service cannot be before the plan starts."
                : null;

    private static void Apply(ServicePlan plan, SaveServicePlanRequest request)
    {
        plan.Name = request.Name.Trim();
        plan.ContactId = request.ContactId;
        plan.ItemId = request.ItemId;
        plan.AssetId = request.AssetId;
        plan.Interval = request.Interval;
        plan.Every = request.Every;
        plan.StartDate = request.StartDate;
        plan.EndDate = request.EndDate;
        plan.LeadDays = request.LeadDays;
        plan.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
    }

    private static void EndIfPast(ServicePlan plan)
    {
        if (plan.EndDate is DateOnly end && plan.NextDueDate > end)
        {
            plan.Status = ServicePlanStatus.Ended;
        }
    }

    private static ServicePlanView View(ServicePlan p, TicketStatus? ticketStatus, DateOnly today) => new()
    {
        ServicePlanId = p.ServicePlanId,
        Name = p.Name,
        ContactId = p.ContactId,
        ItemId = p.ItemId,
        AssetId = p.AssetId,
        Interval = p.Interval.ToString(),
        Every = p.Every,
        StartDate = p.StartDate,
        EndDate = p.EndDate,
        LeadDays = p.LeadDays,
        NextDueDate = p.NextDueDate,
        LastServicedOn = p.LastServicedOn,
        LastTicketId = p.LastTicketId,
        LastTicketStatus = ticketStatus?.ToString(),
        Notes = p.Notes,
        Status = p.Status.ToString(),
        DaysToDue = p.NextDueDate.DayNumber - today.DayNumber,
    };

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];
}
