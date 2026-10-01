using Microsoft.EntityFrameworkCore;
using RecurringBilling.Entity.Enums;
using RecurringBilling.Repository;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Apps;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace BillBook.Worker.RecurringBilling;

public class RecurringBillingWorker : BranchWorkerBase<RecurringBillingWorker>
{
    private readonly IHttpClientFactory _httpClientFactory;

    public RecurringBillingWorker(
        IServiceScopeFactory scopes,
        ILogger<RecurringBillingWorker> logger,
        IConfiguration config,
        IHttpClientFactory httpClientFactory)
        : base(scopes, logger, config)
    {
        _httpClientFactory = httpClientFactory;
    }

    protected override string JobName => "RecurringBilling";
    protected override TimeSpan DefaultInterval => TimeSpan.FromHours(1);

    protected override async Task ProcessBranchAsync(ActiveOrganization branch, IServiceScope scope, CancellationToken stoppingToken)
    {
        var db = scope.ServiceProvider.GetRequiredService<RecurringBillingDbContext>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Find schedules that are active, not in-progress, and due.
        var dueSchedules = await db.RecurringSchedules
            .Where(s => s.Status == ScheduleStatus.Active &&
                        s.RunStatus == ScheduleRunStatus.Pending &&
                        s.NextDueDate <= today &&
                        (s.EndDate == null || s.EndDate >= today))
            .Select(s => s.RecurringScheduleId)
            .ToListAsync(stoppingToken);

        foreach (var id in dueSchedules)
        {
            // Guarded claim: use ExecuteUpdateAsync so only one worker replica claims it
            int claimed = await db.RecurringSchedules
                .Where(s => s.RecurringScheduleId == id && s.RunStatus == ScheduleRunStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.RunStatus, ScheduleRunStatus.InProgress)
                    .SetProperty(p => p.RunAttempts, p => p.RunAttempts + 1),
                    stoppingToken);

            if (claimed == 0) continue; // Claimed by another replica

            try
            {
                var schedule = await db.RecurringSchedules.FindAsync(new object[] { id }, stoppingToken);
                if (schedule == null) continue;

                // Fixed Document Key for Idempotency: REC-{ScheduleId}-{NextDueDate:yyyyMMdd}
                string idempotencyKey = $"REC-{schedule.RecurringScheduleId}-{schedule.NextDueDate:yyyyMMdd}";

                var client = _httpClientFactory.CreateClient();
                var baseUrl = _config["Sales:BaseUrl"] ?? "http://localhost:4503/";
                if (schedule.DocumentType == ScheduleDocumentType.PurchaseBill)
                {
                    baseUrl = _config["Purchase:BaseUrl"] ?? "http://localhost:4502/";
                }

                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Add("X-Tenant-Customer", branch.CustomerId.ToString());
                client.DefaultRequestHeaders.Add("X-Tenant-Org", branch.OrgId.ToString());
                client.DefaultRequestHeaders.Add("X-Idempotency-Key", idempotencyKey);

                // We call a clone endpoint or create endpoint depending on the service
                var request = new { TemplateId = schedule.TemplateDocumentId, IdempotencyKey = idempotencyKey };
                var response = await client.PostAsJsonAsync($"api/{schedule.DocumentType.ToString().ToLowerInvariant()}s/clone", request, stoppingToken);

                response.EnsureSuccessStatusCode();

                // Advance schedule
                schedule.NextDueDate = schedule.Interval switch
                {
                    ScheduleInterval.Daily => schedule.NextDueDate.AddDays(1),
                    ScheduleInterval.Weekly => schedule.NextDueDate.AddDays(7),
                    ScheduleInterval.Monthly => schedule.NextDueDate.AddMonths(1),
                    ScheduleInterval.Yearly => schedule.NextDueDate.AddYears(1),
                    _ => schedule.NextDueDate.AddDays(1)
                };
                schedule.RunStatus = ScheduleRunStatus.Pending;
                schedule.RunError = null;

                if (schedule.EndDate.HasValue && schedule.NextDueDate > schedule.EndDate.Value)
                {
                    schedule.Status = ScheduleStatus.Ended;
                }

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run recurring schedule {Id}", id);
                await db.RecurringSchedules
                    .Where(s => s.RecurringScheduleId == id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.RunStatus, ScheduleRunStatus.Pending)
                        .SetProperty(p => p.RunError, ex.Message),
                        stoppingToken);
            }
        }
    }
}

