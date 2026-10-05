using Shared.Kernel.Apps;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Master.Repository.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Security;
using Shared.Kernel.Tenancy;

namespace Master.Api.Controllers;

/// <summary>
/// Statutory compliance calendar, upcoming filings, and operator-editable due-date rules (TK-119).
/// </summary>
[ApiController]
[Authorize]
[RequireBusinessType(AllowStandalone = true)]
[Route("api/compliance")]
public sealed class ComplianceController : ControllerBase
{
    private readonly AdminDbContext _db;
    private readonly ITenantContext _tenant;

    public ComplianceController(AdminDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    /// <summary>
    /// Returns the upcoming statutory filings for the branch, ordered by due date.
    /// Used by the Home dashboard and compliance tracker.
    /// </summary>
    [HttpGet("upcoming")]
    public async Task<IActionResult> GetUpcoming([FromQuery] int count = 3, CancellationToken ct = default)
    {
        Guid? orgId = _tenant.OrgId;
        if (orgId is null || orgId == Guid.Empty)
        {
            return BadRequest(new MessageResponse { Message = "An organization context is required." });
        }

        Organization? org = await _db.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrgId == orgId.Value, ct);
        if (org is null)
        {
            return NotFound(new MessageResponse { Message = "Organization not found." });
        }

        List<ComplianceDueDate> rules = await _db.ComplianceDueDates.AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.ComplianceDueDateId)
            .ToListAsync(ct);

        List<OrgComplianceSetting> settings = await _db.OrgComplianceSettings.AsNoTracking()
            .Where(s => s.OrgId == orgId.Value)
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var upcoming = new List<UpcomingComplianceItem>();

        foreach (var rule in rules)
        {
            var setting = settings.FirstOrDefault(s => s.ComplianceDueDateId == rule.ComplianceDueDateId);
            bool isApplicable = setting?.IsEnabled ?? IsRuleApplicable(rule, org);
            if (!isApplicable)
            {
                continue;
            }

            int currentYear = today.Year;
            int currentMonth = today.Month;

            var periods = GetRelevantPeriods(rule.Frequency, currentYear, currentMonth);
            foreach (var (year, month, periodName, periodKey) in periods)
            {
                DateOnly dueDate = ComplianceCalendarCalculator.ComputeDueDate(rule, year, month);
                bool isFiled = string.Equals(setting?.LastFiledPeriod, periodKey, StringComparison.OrdinalIgnoreCase);

                int daysRemaining = dueDate.DayNumber - today.DayNumber;

                if (!isFiled && daysRemaining >= -30)
                {
                    bool isExtended = !string.IsNullOrWhiteSpace(rule.SpecificPeriod)
                        && string.Equals(rule.SpecificPeriod, periodKey, StringComparison.OrdinalIgnoreCase)
                        && rule.ExtendedDueDate.HasValue;

                    string status = daysRemaining < 0 ? "Overdue"
                        : daysRemaining <= 3 ? "Due Soon"
                        : "Ready";

                    upcoming.Add(new UpcomingComplianceItem
                    {
                        ComplianceDueDateId = rule.ComplianceDueDateId,
                        FormName = rule.FormName,
                        Category = rule.Category,
                        PeriodName = periodName,
                        PeriodKey = periodKey,
                        DueDate = dueDate,
                        DaysRemaining = daysRemaining,
                        IsExtended = isExtended,
                        ExtensionNotice = isExtended ? rule.ExtensionNotice : null,
                        IsReady = true,
                        Status = status,
                        RuleDescription = rule.RuleDescription,
                        IsFiled = isFiled,
                    });
                }
            }
        }

        var result = upcoming
            .OrderBy(u => u.DueDate)
            .Take(Math.Max(1, count))
            .ToList();

        return Ok(result);
    }

    /// <summary>
    /// Returns the calendar of compliance filings for the given year.
    /// </summary>
    [HttpGet("calendar")]
    public async Task<IActionResult> GetCalendar([FromQuery] int? year = null, CancellationToken ct = default)
    {
        Guid? orgId = _tenant.OrgId;
        if (orgId is null || orgId == Guid.Empty)
        {
            return BadRequest(new MessageResponse { Message = "An organization context is required." });
        }

        Organization? org = await _db.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrgId == orgId.Value, ct);
        if (org is null)
        {
            return NotFound(new MessageResponse { Message = "Organization not found." });
        }

        int targetYear = year ?? DateTime.UtcNow.Year;

        List<ComplianceDueDate> rules = await _db.ComplianceDueDates.AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.ComplianceDueDateId)
            .ToListAsync(ct);

        List<OrgComplianceSetting> settings = await _db.OrgComplianceSettings.AsNoTracking()
            .Where(s => s.OrgId == orgId.Value)
            .ToListAsync(ct);

        var calendar = new List<ComplianceCalendarItem>();

        foreach (var rule in rules)
        {
            var setting = settings.FirstOrDefault(s => s.ComplianceDueDateId == rule.ComplianceDueDateId);
            bool isApplicable = setting?.IsEnabled ?? IsRuleApplicable(rule, org);

            var periods = GetAllPeriodsForYear(rule.Frequency, targetYear);
            foreach (var (pYear, pMonth, periodName, periodKey) in periods)
            {
                DateOnly dueDate = ComplianceCalendarCalculator.ComputeDueDate(rule, pYear, pMonth);
                bool isFiled = string.Equals(setting?.LastFiledPeriod, periodKey, StringComparison.OrdinalIgnoreCase);

                bool isExtended = !string.IsNullOrWhiteSpace(rule.SpecificPeriod)
                    && string.Equals(rule.SpecificPeriod, periodKey, StringComparison.OrdinalIgnoreCase)
                    && rule.ExtendedDueDate.HasValue;

                calendar.Add(new ComplianceCalendarItem
                {
                    ComplianceDueDateId = rule.ComplianceDueDateId,
                    FormName = rule.FormName,
                    Category = rule.Category,
                    PeriodName = periodName,
                    PeriodKey = periodKey,
                    DueDate = dueDate,
                    IsExtended = isExtended,
                    ExtensionNotice = isExtended ? rule.ExtensionNotice : null,
                    RuleDescription = rule.RuleDescription,
                    IsApplicable = isApplicable,
                    IsFiled = isFiled,
                    FiledAt = isFiled ? setting?.LastFiledAt : null,
                });
            }
        }

        return Ok(calendar.OrderBy(c => c.DueDate));
    }

    /// <summary>
    /// Marks a statutory filing period as filed for the branch.
    /// </summary>
    [HttpPost("{id:int}/mark-filed")]
    public async Task<IActionResult> MarkFiled(
        int id, [FromBody] MarkComplianceFiledRequest request, CancellationToken ct = default)
    {
        Guid? orgId = _tenant.OrgId;
        if (orgId is null || orgId == Guid.Empty)
        {
            return BadRequest(new MessageResponse { Message = "An organization context is required." });
        }

        ComplianceDueDate? rule = await _db.ComplianceDueDates.FindAsync([id], ct);
        if (rule is null)
        {
            return NotFound(new MessageResponse { Message = "Compliance rule not found." });
        }

        OrgComplianceSetting? setting = await _db.OrgComplianceSettings
            .FirstOrDefaultAsync(s => s.OrgId == orgId.Value && s.ComplianceDueDateId == id, ct);

        if (setting is null)
        {
            setting = new OrgComplianceSetting
            {
                OrgId = orgId.Value,
                ComplianceDueDateId = id,
                IsEnabled = true,
                ReminderDaysBefore = 3,
                LastFiledPeriod = request.Period,
                LastFiledAt = DateTimeOffset.UtcNow,
            };
            _db.OrgComplianceSettings.Add(setting);
        }
        else
        {
            setting.LastFiledPeriod = request.Period;
            setting.LastFiledAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new MessageResponse { Message = $"{rule.FormName} for {request.Period} marked as filed." });
    }

    /// <summary>
    /// Operator endpoint to extend a statutory due date globally (e.g. government extension).
    /// </summary>
    [RequirePermission("platform.edit")]
    [HttpPost("rules/{id:int}/extend")]
    public async Task<IActionResult> ExtendDueDate(
        int id, [FromBody] ExtendDueDateRequest request, CancellationToken ct = default)
    {

        ComplianceDueDate? rule = await _db.ComplianceDueDates.FindAsync([id], ct);
        if (rule is null)
        {
            return NotFound(new MessageResponse { Message = "Compliance rule not found." });
        }

        rule.SpecificPeriod = request.SpecificPeriod;
        rule.ExtendedDueDate = request.ExtendedDueDate;
        rule.ExtensionNotice = request.ExtensionNotice;

        await _db.SaveChangesAsync(ct);
        return Ok(new MessageResponse { Message = $"{rule.FormName} due date for {request.SpecificPeriod} extended to {request.ExtendedDueDate:yyyy-MM-dd}." });
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
                periods.Add((hyYear, 9, $"Apr–Sep {hyYear}", $"{hyYear:D4}-H1"));
                periods.Add((hyYear + 1, 3, $"Oct–Mar {hyYear + 1}", $"{hyYear + 1:D4}-H2"));
                break;

            case ComplianceFrequency.Annual:
                periods.Add((currentYear, 3, $"FY {currentYear - 1}-{currentYear}", $"{currentYear:D4}-FY"));
                break;
        }

        return periods;
    }

    private static List<(int Year, int Month, string PeriodName, string PeriodKey)> GetAllPeriodsForYear(
        ComplianceFrequency frequency, int year)
    {
        var periods = new List<(int, int, string, string)>();

        switch (frequency)
        {
            case ComplianceFrequency.Monthly:
                for (int m = 1; m <= 12; m++)
                {
                    periods.Add((year, m, new DateTime(year, m, 1).ToString("MMMM yyyy"), $"{year:D4}-{m:D2}"));
                }
                break;

            case ComplianceFrequency.Quarterly:
                periods.Add((year, 6, $"Q1 Apr–Jun {year}", $"{year:D4}-Q1"));
                periods.Add((year, 9, $"Q2 Jul–Sep {year}", $"{year:D4}-Q2"));
                periods.Add((year, 12, $"Q3 Oct–Dec {year}", $"{year:D4}-Q3"));
                periods.Add((year + 1, 3, $"Q4 Jan–Mar {year + 1}", $"{year + 1:D4}-Q4"));
                break;

            case ComplianceFrequency.HalfYearly:
                periods.Add((year, 9, $"H1 Apr–Sep {year}", $"{year:D4}-H1"));
                periods.Add((year + 1, 3, $"H2 Oct–Mar {year + 1}", $"{year + 1:D4}-H2"));
                break;

            case ComplianceFrequency.Annual:
                periods.Add((year, 3, $"FY {year - 1}-{year}", $"{year:D4}-FY"));
                break;
        }

        return periods;
    }
}
