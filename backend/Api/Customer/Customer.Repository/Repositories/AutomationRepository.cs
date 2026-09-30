using Customer.Entity.Enums;
using Customer.Entity.Models;
using Customer.Entity.TableEntities;
using Customer.Repository.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Customer;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Messaging;
using Shared.Kernel.Tenancy;

namespace Customer.Repository.Repositories;

public sealed class AutomationRepository
{
    private readonly CustomerDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly EmailConsentRepository _consentRepo;
    private readonly IUnsubscribeTokenSigner _unsubSigner;
    private readonly IEventPublisher _events;
    private readonly IConfiguration _config;
    private readonly ILogger<AutomationRepository> _log;

    public AutomationRepository(
        CustomerDbContext db,
        ITenantContext tenant,
        EmailConsentRepository consentRepo,
        IUnsubscribeTokenSigner unsubSigner,
        IEventPublisher events,
        IConfiguration config,
        ILogger<AutomationRepository> log)
    {
        _db = db;
        _tenant = tenant;
        _consentRepo = consentRepo;
        _unsubSigner = unsubSigner;
        _events = events;
        _config = config;
        _log = log;
    }

    public async Task<List<AutomationDto>> ListAsync(CancellationToken ct)
    {
        var automations = await _db.Automations
            .AsNoTracking()
            .Include(a => a.Steps)
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

        var templateNames = await _db.EmailTemplates
            .AsNoTracking()
            .ToDictionaryAsync(t => t.EmailTemplateId, t => t.Name, ct);

        var activeEnrolmentCounts = await _db.AutomationEnrolments
            .AsNoTracking()
            .Where(e => e.Status == AutomationEnrolmentStatus.Active)
            .GroupBy(e => e.AutomationId)
            .Select(g => new { AutomationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AutomationId, x => x.Count, ct);

        return automations.Select(a => MapToDto(a, templateNames, activeEnrolmentCounts.GetValueOrDefault(a.AutomationId, 0))).ToList();
    }

    public async Task<AutomationDto?> GetByIdAsync(long id, CancellationToken ct)
    {
        var a = await _db.Automations
            .AsNoTracking()
            .Include(x => x.Steps)
            .FirstOrDefaultAsync(x => x.AutomationId == id, ct);

        if (a is null) return null;

        var templateNames = await _db.EmailTemplates
            .AsNoTracking()
            .ToDictionaryAsync(t => t.EmailTemplateId, t => t.Name, ct);

        int activeCount = await _db.AutomationEnrolments
            .AsNoTracking()
            .CountAsync(e => e.AutomationId == id && e.Status == AutomationEnrolmentStatus.Active, ct);

        return MapToDto(a, templateNames, activeCount);
    }

    public async Task<AutomationDto> CreateAsync(SaveAutomationRequest request, CancellationToken ct)
    {
        var automation = new Automation
        {
            Name = request.Name.Trim(),
            TriggerType = request.TriggerType,
            TriggerSource = string.IsNullOrWhiteSpace(request.TriggerSource) ? null : request.TriggerSource.Trim(),
            TriggerStatus = string.IsNullOrWhiteSpace(request.TriggerStatus) ? null : request.TriggerStatus.Trim(),
            IsActive = request.IsActive,
            Steps = request.Steps
                .OrderBy(s => s.Sequence)
                .Select((s, index) => new AutomationStep
                {
                    Sequence = s.Sequence > 0 ? s.Sequence : index + 1,
                    Kind = s.Kind,
                    WaitDays = s.WaitDays,
                    EmailTemplateId = s.EmailTemplateId
                }).ToList()
        };

        _db.Automations.Add(automation);
        await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(automation.AutomationId, ct))!;
    }

    public async Task<AutomationDto?> UpdateAsync(long id, SaveAutomationRequest request, CancellationToken ct)
    {
        var automation = await _db.Automations
            .Include(a => a.Steps)
            .FirstOrDefaultAsync(a => a.AutomationId == id, ct);

        if (automation is null) return null;

        automation.Name = request.Name.Trim();
        automation.TriggerType = request.TriggerType;
        automation.TriggerSource = string.IsNullOrWhiteSpace(request.TriggerSource) ? null : request.TriggerSource.Trim();
        automation.TriggerStatus = string.IsNullOrWhiteSpace(request.TriggerStatus) ? null : request.TriggerStatus.Trim();
        automation.IsActive = request.IsActive;

        // Replace steps
        _db.AutomationSteps.RemoveRange(automation.Steps);
        automation.Steps = request.Steps
            .OrderBy(s => s.Sequence)
            .Select((s, index) => new AutomationStep
            {
                AutomationId = id,
                Sequence = s.Sequence > 0 ? s.Sequence : index + 1,
                Kind = s.Kind,
                WaitDays = s.WaitDays,
                EmailTemplateId = s.EmailTemplateId
            }).ToList();

        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct)
    {
        var automation = await _db.Automations.FirstOrDefaultAsync(a => a.AutomationId == id, ct);
        if (automation is null) return false;

        _db.Automations.Remove(automation);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<AutomationEnrolmentDto>> ListEnrolmentsAsync(long automationId, CancellationToken ct)
    {
        var enrolments = await _db.AutomationEnrolments
            .AsNoTracking()
            .Where(e => e.AutomationId == automationId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);

        return enrolments.Select(e => new AutomationEnrolmentDto
        {
            AutomationEnrolmentId = e.AutomationEnrolmentId,
            AutomationId = e.AutomationId,
            LeadId = e.LeadId,
            ContactId = e.ContactId,
            Email = e.Email,
            CurrentStep = e.CurrentStep,
            NextRunAt = e.NextRunAt,
            Status = e.Status,
            ExitReason = e.ExitReason,
            CreatedAt = e.CreatedAt,
            CompletedAt = e.CompletedAt
        }).ToList();
    }

    public async Task TriggerLeadCreatedAsync(long leadId, string? email, string? name, LeadSource source, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email)) return;
        string cleanEmail = email.Trim().ToLowerInvariant();

        if (await _consentRepo.IsSuppressedAsync(cleanEmail, ct)) return;

        var automations = await _db.Automations
            .AsNoTracking()
            .Include(a => a.Steps)
            .Where(a => a.IsActive && a.TriggerType == AutomationTriggerType.LeadCreated)
            .ToListAsync(ct);

        foreach (var auto in automations)
        {
            if (!string.IsNullOrEmpty(auto.TriggerSource) &&
                !string.Equals(auto.TriggerSource, source.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool alreadyActive = await _db.AutomationEnrolments
                .AnyAsync(e => e.AutomationId == auto.AutomationId && e.LeadId == leadId && e.Status == AutomationEnrolmentStatus.Active, ct);

            if (alreadyActive) continue;

            var firstStep = auto.Steps.OrderBy(s => s.Sequence).FirstOrDefault();
            if (firstStep is null) continue;

            DateTimeOffset nextRun = firstStep.Kind == AutomationStepKind.Wait
                ? DateTimeOffset.UtcNow.AddDays(firstStep.WaitDays)
                : DateTimeOffset.UtcNow;

            var enrolment = new AutomationEnrolment
            {
                AutomationId = auto.AutomationId,
                LeadId = leadId,
                Email = cleanEmail,
                CurrentStep = firstStep.Sequence,
                NextRunAt = nextRun,
                Status = AutomationEnrolmentStatus.Active
            };

            _db.AutomationEnrolments.Add(enrolment);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task TriggerLeadStatusChangedAsync(long leadId, LeadStatus newStatus, CancellationToken ct)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Check exit conditions for existing active enrolments for this lead
        if (newStatus == LeadStatus.Converted || newStatus == LeadStatus.Lost)
        {
            var activeEnrolments = await _db.AutomationEnrolments
                .Where(e => e.LeadId == leadId && e.Status == AutomationEnrolmentStatus.Active)
                .ToListAsync(ct);

            foreach (var e in activeEnrolments)
            {
                e.Status = AutomationEnrolmentStatus.Exited;
                e.ExitReason = newStatus.ToString();
                e.CompletedAt = now;
            }

            await _db.SaveChangesAsync(ct);
            return;
        }

        // Trigger automations listening for LeadStatusChanged
        var lead = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.LeadId == leadId, ct);
        if (lead is null || string.IsNullOrWhiteSpace(lead.Email)) return;
        string cleanEmail = lead.Email.Trim().ToLowerInvariant();

        if (await _consentRepo.IsSuppressedAsync(cleanEmail, ct)) return;

        var automations = await _db.Automations
            .AsNoTracking()
            .Include(a => a.Steps)
            .Where(a => a.IsActive && a.TriggerType == AutomationTriggerType.LeadStatusChanged)
            .ToListAsync(ct);

        foreach (var auto in automations)
        {
            if (!string.IsNullOrEmpty(auto.TriggerStatus) &&
                !string.Equals(auto.TriggerStatus, newStatus.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool alreadyActive = await _db.AutomationEnrolments
                .AnyAsync(e => e.AutomationId == auto.AutomationId && e.LeadId == leadId && e.Status == AutomationEnrolmentStatus.Active, ct);

            if (alreadyActive) continue;

            var firstStep = auto.Steps.OrderBy(s => s.Sequence).FirstOrDefault();
            if (firstStep is null) continue;

            DateTimeOffset nextRun = firstStep.Kind == AutomationStepKind.Wait
                ? now.AddDays(firstStep.WaitDays)
                : now;

            var enrolment = new AutomationEnrolment
            {
                AutomationId = auto.AutomationId,
                LeadId = leadId,
                Email = cleanEmail,
                CurrentStep = firstStep.Sequence,
                NextRunAt = nextRun,
                Status = AutomationEnrolmentStatus.Active
            };

            _db.AutomationEnrolments.Add(enrolment);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task TriggerContactBecameCustomerAsync(long contactId, string email, string? name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email)) return;
        string cleanEmail = email.Trim().ToLowerInvariant();

        if (await _consentRepo.IsSuppressedAsync(cleanEmail, ct)) return;

        var automations = await _db.Automations
            .AsNoTracking()
            .Include(a => a.Steps)
            .Where(a => a.IsActive && a.TriggerType == AutomationTriggerType.ContactBecameCustomer)
            .ToListAsync(ct);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (var auto in automations)
        {
            bool alreadyActive = await _db.AutomationEnrolments
                .AnyAsync(e => e.AutomationId == auto.AutomationId && e.ContactId == contactId && e.Status == AutomationEnrolmentStatus.Active, ct);

            if (alreadyActive) continue;

            var firstStep = auto.Steps.OrderBy(s => s.Sequence).FirstOrDefault();
            if (firstStep is null) continue;

            DateTimeOffset nextRun = firstStep.Kind == AutomationStepKind.Wait
                ? now.AddDays(firstStep.WaitDays)
                : now;

            var enrolment = new AutomationEnrolment
            {
                AutomationId = auto.AutomationId,
                ContactId = contactId,
                Email = cleanEmail,
                CurrentStep = firstStep.Sequence,
                NextRunAt = nextRun,
                Status = AutomationEnrolmentStatus.Active
            };

            _db.AutomationEnrolments.Add(enrolment);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task AdvanceDueEnrolmentsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var dueEnrolments = await _db.AutomationEnrolments
            .Where(e => e.Status == AutomationEnrolmentStatus.Active && e.NextRunAt <= now)
            .ToListAsync(ct);

        if (dueEnrolments.Count == 0) return;

        var (tenantCustomerId, tenantOrgId) = _tenant.Require();
        string baseUrl = _config["App:BaseUrl"] ?? _config["BaseUrl"] ?? "http://localhost:5000";

        foreach (var enrolment in dueEnrolments)
        {
            try
            {
                // 1. Check Exit conditions: Email unsubscribed/bounced
                if (await _consentRepo.IsSuppressedAsync(enrolment.Email, ct))
                {
                    enrolment.Status = AutomationEnrolmentStatus.Exited;
                    enrolment.ExitReason = "Unsubscribed";
                    enrolment.CompletedAt = now;
                    continue;
                }

                // 2. Check Exit conditions: Lead converted or lost
                if (enrolment.LeadId.HasValue)
                {
                    var lead = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.LeadId == enrolment.LeadId.Value, ct);
                    if (lead is null)
                    {
                        enrolment.Status = AutomationEnrolmentStatus.Exited;
                        enrolment.ExitReason = "LeadNotFound";
                        enrolment.CompletedAt = now;
                        continue;
                    }

                    if (lead.Status == LeadStatus.Converted)
                    {
                        enrolment.Status = AutomationEnrolmentStatus.Exited;
                        enrolment.ExitReason = "Converted";
                        enrolment.CompletedAt = now;
                        continue;
                    }

                    if (lead.Status == LeadStatus.Lost)
                    {
                        enrolment.Status = AutomationEnrolmentStatus.Exited;
                        enrolment.ExitReason = "Lost";
                        enrolment.CompletedAt = now;
                        continue;
                    }
                }

                // 3. Load automation steps
                var steps = await _db.AutomationSteps
                    .AsNoTracking()
                    .Where(s => s.AutomationId == enrolment.AutomationId)
                    .OrderBy(s => s.Sequence)
                    .ToListAsync(ct);

                var currentStep = steps.FirstOrDefault(s => s.Sequence == enrolment.CurrentStep);
                if (currentStep is null)
                {
                    enrolment.Status = AutomationEnrolmentStatus.Completed;
                    enrolment.CompletedAt = now;
                    continue;
                }

                if (currentStep.Kind == AutomationStepKind.Send)
                {
                    if (currentStep.EmailTemplateId.HasValue)
                    {
                        var template = await _db.EmailTemplates
                            .AsNoTracking()
                            .FirstOrDefaultAsync(t => t.EmailTemplateId == currentStep.EmailTemplateId.Value, ct);

                        if (template is not null && !string.IsNullOrWhiteSpace(template.HtmlBody))
                        {
                            string unsubToken = _unsubSigner.GenerateToken(tenantCustomerId, tenantOrgId, enrolment.Email);
                            string unsubUrl = $"{baseUrl.TrimEnd('/')}/api/public/unsubscribe/{unsubToken}";

                            string leadName = "Customer";
                            if (enrolment.LeadId.HasValue)
                            {
                                var lead = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.LeadId == enrolment.LeadId.Value, ct);
                                if (lead is not null && !string.IsNullOrWhiteSpace(lead.Name))
                                {
                                    leadName = lead.Name;
                                }
                            }

                            string htmlBody = template.HtmlBody
                                .Replace("{{first_name}}", leadName, StringComparison.OrdinalIgnoreCase)
                                .Replace("{{name}}", leadName, StringComparison.OrdinalIgnoreCase)
                                .Replace("{{email}}", enrolment.Email, StringComparison.OrdinalIgnoreCase)
                                .Replace("{{unsubscribe}}", unsubUrl, StringComparison.OrdinalIgnoreCase);

                            string? textBody = template.TextBody?
                                .Replace("{{first_name}}", leadName, StringComparison.OrdinalIgnoreCase)
                                .Replace("{{name}}", leadName, StringComparison.OrdinalIgnoreCase)
                                .Replace("{{email}}", enrolment.Email, StringComparison.OrdinalIgnoreCase)
                                .Replace("{{unsubscribe}}", unsubUrl, StringComparison.OrdinalIgnoreCase);

                            // Deterministic MessageId: aut-{enrolmentId}-{step} (TK-122)
                            string messageId = $"aut-{enrolment.AutomationEnrolmentId}-{currentStep.Sequence}";

                            var emailRequest = new EmailRequested
                            {
                                MessageId = messageId,
                                RequestedAt = now,
                                CustomerId = tenantCustomerId,
                                ToEmail = enrolment.Email,
                                ToName = leadName,
                                Subject = template.Subject,
                                HtmlBody = htmlBody,
                                TextBody = textBody,
                                Headers = new Dictionary<string, string>
                                {
                                    ["List-Unsubscribe"] = $"<{unsubUrl}>",
                                    ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click"
                                }
                            };

                            await _events.PublishAsync(emailRequest, ct);
                        }
                    }

                    // Move to next step in sequence
                    var nextStep = steps.FirstOrDefault(s => s.Sequence > currentStep.Sequence);
                    if (nextStep is null)
                    {
                        enrolment.Status = AutomationEnrolmentStatus.Completed;
                        enrolment.CompletedAt = now;
                    }
                    else if (nextStep.Kind == AutomationStepKind.Wait)
                    {
                        enrolment.NextRunAt = now.AddDays(nextStep.WaitDays);
                        var stepAfterWait = steps.FirstOrDefault(s => s.Sequence > nextStep.Sequence);
                        if (stepAfterWait is not null)
                        {
                            enrolment.CurrentStep = stepAfterWait.Sequence;
                        }
                        else
                        {
                            enrolment.Status = AutomationEnrolmentStatus.Completed;
                            enrolment.CompletedAt = now.AddDays(nextStep.WaitDays);
                        }
                    }
                    else
                    {
                        enrolment.CurrentStep = nextStep.Sequence;
                        enrolment.NextRunAt = now;
                    }
                }
                else if (currentStep.Kind == AutomationStepKind.Wait)
                {
                    // Wait completed! Move to next step after wait
                    var nextStep = steps.FirstOrDefault(s => s.Sequence > currentStep.Sequence);
                    if (nextStep is null)
                    {
                        enrolment.Status = AutomationEnrolmentStatus.Completed;
                        enrolment.CompletedAt = now;
                    }
                    else if (nextStep.Kind == AutomationStepKind.Wait)
                    {
                        enrolment.CurrentStep = nextStep.Sequence;
                        enrolment.NextRunAt = now.AddDays(nextStep.WaitDays);
                    }
                    else
                    {
                        enrolment.CurrentStep = nextStep.Sequence;
                        enrolment.NextRunAt = now;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error advancing automation enrolment {EnrolmentId}.", enrolment.AutomationEnrolmentId);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private static AutomationDto MapToDto(Automation a, Dictionary<long, string> templateNames, int activeCount)
    {
        return new AutomationDto
        {
            AutomationId = a.AutomationId,
            Name = a.Name,
            TriggerType = a.TriggerType,
            TriggerSource = a.TriggerSource,
            TriggerStatus = a.TriggerStatus,
            IsActive = a.IsActive,
            ActiveEnrolmentsCount = activeCount,
            Steps = a.Steps.OrderBy(s => s.Sequence).Select(s => new AutomationStepDto
            {
                AutomationStepId = s.AutomationStepId,
                Sequence = s.Sequence,
                Kind = s.Kind,
                WaitDays = s.WaitDays,
                EmailTemplateId = s.EmailTemplateId,
                EmailTemplateName = s.EmailTemplateId.HasValue ? templateNames.GetValueOrDefault(s.EmailTemplateId.Value) : null
            }).ToList(),
            CreatedAt = a.CreatedAt,
            ModifiedAt = a.ModifiedAt
        };
    }
}
