using Shared.Kernel.Apps;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Master.Repository.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

public class ComplianceReminderDto
{
    public Guid CustomerId { get; set; }
    public Guid OrgId { get; set; }
    public string OrgName { get; set; } = null!;
    public string FormName { get; set; } = null!;
    public string Category { get; set; } = null!;
    public string PeriodName { get; set; } = null!;
    public string PeriodKey { get; set; } = null!;
    public DateOnly DueDate { get; set; }
    public int DaysRemaining { get; set; }
    public string RecipientEmail { get; set; } = null!;
    public string RecipientName { get; set; } = null!;
}

/// <summary>
/// Internal endpoints for compliance reminders (TK-119), called by Notification.Worker.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/compliance")]
public sealed class InternalComplianceController : ControllerBase
{
    private readonly AdminDbContext _db;

    public InternalComplianceController(AdminDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Evaluates all active branches and returns due compliance reminders for the given date.
    /// A reminder is produced when a filing is N days away (per OrgComplianceSetting.ReminderDaysBefore, default 3)
    /// and has not been filed yet, addressed to all active users with accounting.view in that branch.
    /// </summary>
    [HttpGet("reminders")]
    public async Task<IActionResult> GetReminders([FromQuery] DateOnly? asOf = null, CancellationToken ct = default)
    {
        DateOnly today = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // Find permission ID for accounting.view
        int? accountingViewPermId = await _db.Permissions
            .Where(p => p.Code == "accounting.view")
            .Select(p => (int?)p.PermissionId)
            .FirstOrDefaultAsync(ct);

        List<int> roleIdsWithAccounting = [];
        if (accountingViewPermId.HasValue)
        {
            roleIdsWithAccounting = await _db.RolePermissions
                .Where(rp => rp.PermissionId == accountingViewPermId.Value)
                .Select(rp => rp.RoleId)
                .ToListAsync(ct);
        }

        List<Organization> orgs = await _db.Organizations.AsNoTracking()
            .Where(o => o.Status == Master.Entity.Enums.TenantStatus.Active)
            .ToListAsync(ct);

        List<ComplianceDueDate> rules = await _db.ComplianceDueDates.AsNoTracking()
            .Where(r => r.IsActive)
            .ToListAsync(ct);

        List<OrgComplianceSetting> settings = await _db.OrgComplianceSettings.AsNoTracking()
            .ToListAsync(ct);

        var reminders = new List<ComplianceReminderDto>();

        foreach (var org in orgs)
        {
            var branchSettings = settings.Where(s => s.OrgId == org.OrgId).ToList();

            // Find recipients with accounting.view
            var recipients = await (from uor in _db.UserOrganizationRoles
                                    where uor.OrgId == org.OrgId && uor.IsActive && roleIdsWithAccounting.Contains(uor.RoleId)
                                    join u in _db.Users on uor.UserId equals u.UserId
                                    where u.IsActive && !string.IsNullOrWhiteSpace(u.Email)
                                    select new { u.Email, u.DisplayName })
                                    .Distinct()
                                    .ToListAsync(ct);

            if (recipients.Count == 0)
            {
                continue;
            }

            foreach (var rule in rules)
            {
                var setting = branchSettings.FirstOrDefault(s => s.ComplianceDueDateId == rule.ComplianceDueDateId);
                bool isApplicable = setting?.IsEnabled ?? IsRuleApplicable(rule, org);
                if (!isApplicable)
                {
                    continue;
                }

                int reminderDays = setting?.ReminderDaysBefore ?? 3;

                // Evaluate periods around today
                int curYear = today.Year;
                int curMonth = today.Month;
                var periods = GetRelevantPeriods(rule.Frequency, curYear, curMonth);

                foreach (var (pYear, pMonth, periodName, periodKey) in periods)
                {
                    DateOnly dueDate = ComplianceCalendarCalculator.ComputeDueDate(rule, pYear, pMonth);
                    bool isFiled = string.Equals(setting?.LastFiledPeriod, periodKey, StringComparison.OrdinalIgnoreCase);

                    int daysRemaining = dueDate.DayNumber - today.DayNumber;

                    // Produce reminder if it matches reminder interval (e.g. 7, 3, or 1 day away, or exact match)
                    if (!isFiled && (daysRemaining == reminderDays || daysRemaining == 1 || daysRemaining == 7))
                    {
                        foreach (var recipient in recipients)
                        {
                            reminders.Add(new ComplianceReminderDto
                            {
                                CustomerId = org.CustomerId,
                                OrgId = org.OrgId,
                                OrgName = org.Name,
                                FormName = rule.FormName,
                                Category = rule.Category,
                                PeriodName = periodName,
                                PeriodKey = periodKey,
                                DueDate = dueDate,
                                DaysRemaining = daysRemaining,
                                RecipientEmail = recipient.Email!,
                                RecipientName = recipient.DisplayName,
                            });
                        }
                    }
                }
            }
        }

        return Ok(reminders);
    }

    private static bool IsRuleApplicable(ComplianceDueDate rule, Organization org)
    {
        return rule.ApplicabilityCondition switch
        {
            "GSTIN" => !string.IsNullOrWhiteSpace(org.Gstin),
            "TAN" => !string.IsNullOrWhiteSpace(org.Tan),
            "CIN" => !string.IsNullOrWhiteSpace(org.Cin),
            "All" => true,
            _ => true,
        };
    }

    private static List<(int Year, int Month, string PeriodName, string PeriodKey)> GetRelevantPeriods(
        ComplianceFrequency frequency, int currentYear, int currentMonth)
    {
        var periods = new List<(int, int, string, string)>();

        switch (frequency)
        {
            case ComplianceFrequency.Monthly:
                int prevMonth = currentMonth == 1 ? 12 : currentMonth - 1;
                int prevYear = currentMonth == 1 ? currentYear - 1 : currentYear;
                periods.Add((prevYear, prevMonth, new DateTime(prevYear, prevMonth, 1).ToString("MMMM yyyy"), $"{prevYear:D4}-{prevMonth:D2}"));
                periods.Add((currentYear, currentMonth, new DateTime(currentYear, currentMonth, 1).ToString("MMMM yyyy"), $"{currentYear:D4}-{currentMonth:D2}"));
                break;

            case ComplianceFrequency.Quarterly:
                int[] quarterEndMonths = [3, 6, 9, 12];
                foreach (int m in quarterEndMonths)
                {
                    string qName = m switch { 3 => "Q4", 6 => "Q1", 9 => "Q2", _ => "Q3" };
                    int qYear = m == 3 ? currentYear : (currentMonth <= 3 ? currentYear - 1 : currentYear);
                    periods.Add((qYear, m, $"{qName} {qYear}", $"{qYear:D4}-Q{m / 3}"));
                }
                break;

            case ComplianceFrequency.HalfYearly:
                int hyYear = currentMonth <= 3 ? currentYear - 1 : currentYear;
                periods.Add((hyYear, 9, $"Aprâ€“Sep {hyYear}", $"{hyYear:D4}-H1"));
                periods.Add((hyYear + 1, 3, $"Octâ€“Mar {hyYear + 1}", $"{hyYear + 1:D4}-H2"));
                break;

            case ComplianceFrequency.Annual:
                periods.Add((currentYear, 3, $"FY {currentYear - 1}-{currentYear}", $"{currentYear:D4}-FY"));
                break;
        }

        return periods;
    }
}
