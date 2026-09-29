using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Api.Services;
using Reporting.Repository.Repositories;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Entity.TableEntities;
using Reporting.Repository;
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// Scheduled report email (TK-114). The card's "done when" is the first test:
/// a weekly schedule emails its report once per week. The run is driven with
/// explicit times rather than the worker's clock, and the export and the mail
/// are stubs, so what is asserted is the claim, the permission check and what
/// the schedule records; the workbook itself is the export route's, tested there.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ReportScheduleTests
{
    // A Monday, 08:00 in India.
    private static readonly DateTimeOffset MondayEight = new(2026, 10, 5, 2, 30, 0, TimeSpan.Zero);

    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Recipient = Guid.NewGuid();

    private readonly PostgresFixture _postgres;

    public ReportScheduleTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>The card's "done when".</summary>
    [SkippableFact]
    public async Task A_weekly_schedule_emails_its_report_once_per_week()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customer, Guid org) = (Guid.NewGuid(), Guid.NewGuid());
        long id = await ScheduleAsync(customer, org, MondayEight);
        RecordingMailer mailer = new();

        async Task<ScheduleRunResult> RunAt(DateTimeOffset now)
        {
            await using ReportingDbContext db = _postgres.CreateContext(customer, org);
            return await Run(db, mailer, Users(ownerPermissions: Allowed)).RunDueAsync(now, CancellationToken.None);
        }

        Assert.Equal(1, (await RunAt(MondayEight.AddMinutes(3))).Sent);
        Assert.Equal(0, (await RunAt(MondayEight.AddMinutes(8))).Claimed);
        Assert.Equal(0, (await RunAt(MondayEight.AddDays(3))).Claimed);
        Assert.Equal(1, (await RunAt(MondayEight.AddDays(7).AddMinutes(1))).Sent);

        Assert.Equal(2, mailer.Sent.Count);
        Assert.All(mailer.Sent, m => Assert.Equal("anita@shop.in", m.ToEmail));
        Assert.All(mailer.Sent, m => Assert.EndsWith(".xlsx", m.FileName));

        await using ReportingDbContext check = _postgres.CreateContext(customer, org);
        ReportSchedule schedule = await check.ReportSchedules.SingleAsync(s => s.ReportScheduleId == id);
        Assert.Equal(MondayEight.AddDays(14), schedule.NextRunAt);
        Assert.Equal(ReportScheduleRunStatus.Sent, schedule.LastStatus);
        Assert.Contains("Sent to 1", schedule.LastMessage);
    }

    [SkippableFact]
    public async Task A_due_schedule_runs_once_under_concurrency()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customer, Guid org) = (Guid.NewGuid(), Guid.NewGuid());
        long id = await ScheduleAsync(customer, org, MondayEight);
        DateTimeOffset next = MondayEight.AddDays(7);

        // Eight replicas read the same due row and race to claim it.
        bool[] claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using ReportingDbContext db = _postgres.CreateContext(customer, org);
            return await ReportScheduleRunRepository.TryClaimAsync(db, id, MondayEight, next, MondayEight.AddMinutes(1), CancellationToken.None);
        }));

        Assert.Equal(1, claims.Count(c => c));

        // And whole runs racing send one email between them.
        long second = await ScheduleAsync(customer, org, MondayEight);
        RecordingMailer mailer = new();
        await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            await using ReportingDbContext db = _postgres.CreateContext(customer, org);
            await Run(db, mailer, Users(ownerPermissions: Allowed)).RunDueAsync(MondayEight.AddMinutes(2), CancellationToken.None);
        }));

        Assert.Single(mailer.Sent);
        Assert.NotEqual(id, second);
    }

    [SkippableFact]
    public async Task A_schedule_whose_owner_lost_the_permission_sends_nothing_and_records_why()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customer, Guid org) = (Guid.NewGuid(), Guid.NewGuid());
        long id = await ScheduleAsync(customer, org, MondayEight);
        RecordingMailer mailer = new();
        RecordingExporter exporter = new();

        await using (ReportingDbContext db = _postgres.CreateContext(customer, org))
        {
            // The owner still exports reports but no longer has sales.view.
            ScheduleRunResult result = await Run(db, mailer, Users(ownerPermissions: ["reports.view", "reports.export"]), exporter)
                .RunDueAsync(MondayEight.AddMinutes(1), CancellationToken.None);

            Assert.Equal(1, result.Skipped);
        }

        Assert.Empty(mailer.Sent);
        Assert.Equal(0, exporter.Calls);

        await using ReportingDbContext check = _postgres.CreateContext(customer, org);
        ReportSchedule schedule = await check.ReportSchedules.SingleAsync(s => s.ReportScheduleId == id);
        Assert.Equal(ReportScheduleRunStatus.Skipped, schedule.LastStatus);
        Assert.Contains("no longer has permission to run this report", schedule.LastMessage);

        // It stays scheduled: when the permission comes back, so do the emails.
        Assert.True(schedule.IsActive);
        Assert.Equal(MondayEight.AddDays(7), schedule.NextRunAt);
    }

    [SkippableFact]
    public async Task An_owner_who_left_the_branch_or_cannot_export_sends_nothing()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customer, Guid org) = (Guid.NewGuid(), Guid.NewGuid());
        long id = await ScheduleAsync(customer, org, MondayEight);
        RecordingMailer mailer = new();

        await using (ReportingDbContext db = _postgres.CreateContext(customer, org))
        {
            await Run(db, mailer, Users(ownerPermissions: ["reports.view", "sales.view"])).RunDueAsync(MondayEight.AddMinutes(1), CancellationToken.None);
        }

        await using (ReportingDbContext db = _postgres.CreateContext(customer, org))
        {
            ReportSchedule schedule = await db.ReportSchedules.SingleAsync(s => s.ReportScheduleId == id);
            Assert.Contains("permission to export reports", schedule.LastMessage);
        }

        long gone = await ScheduleAsync(customer, org, MondayEight);
        await using (ReportingDbContext db = _postgres.CreateContext(customer, org))
        {
            await Run(db, mailer, new StubUsers([Person(Recipient, "Anita", "anita@shop.in", Allowed)]))
                .RunDueAsync(MondayEight.AddMinutes(1), CancellationToken.None);

            ReportSchedule schedule = await db.ReportSchedules.AsNoTracking().SingleAsync(s => s.ReportScheduleId == gone);
            Assert.Contains("no longer a user of this branch", schedule.LastMessage);
        }

        Assert.Empty(mailer.Sent);
    }

    [SkippableFact]
    public async Task With_Master_unreachable_nothing_is_claimed_so_the_next_tick_still_sends()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customer, Guid org) = (Guid.NewGuid(), Guid.NewGuid());
        await ScheduleAsync(customer, org, MondayEight);
        RecordingMailer mailer = new();

        await using (ReportingDbContext db = _postgres.CreateContext(customer, org))
        {
            ScheduleRunResult result = await Run(db, mailer, new StubUsers(null)).RunDueAsync(MondayEight.AddMinutes(1), CancellationToken.None);
            Assert.Equal(0, result.Claimed);
        }

        await using (ReportingDbContext db = _postgres.CreateContext(customer, org))
        {
            ScheduleRunResult result = await Run(db, mailer, Users(ownerPermissions: Allowed)).RunDueAsync(MondayEight.AddMinutes(6), CancellationToken.None);
            Assert.Equal(1, result.Sent);
        }
    }

    [Theory]
    [InlineData("2026-10-05T02:29:00Z", "2026-10-05T02:30:00Z")] // Monday 07:59 IST: today at 08:00
    [InlineData("2026-10-05T02:30:00Z", "2026-10-12T02:30:00Z")] // exactly 08:00: strictly after, so next week
    [InlineData("2026-10-07T10:00:00Z", "2026-10-12T02:30:00Z")] // Wednesday: the coming Monday
    public void A_weekly_slot_is_the_next_monday_at_eight_in_india(string after, string expected) =>
        Assert.Equal(
            DateTimeOffset.Parse(expected),
            ReportScheduleCalendarRepository.NextRun(ReportScheduleFrequency.Weekly, DayOfWeek.Monday, null, 8, "Asia/Kolkata", DateTimeOffset.Parse(after)));

    [Fact]
    public void A_daily_slot_is_tomorrow_once_todays_hour_has_passed()
    {
        DateTimeOffset next = ReportScheduleCalendarRepository.NextRun(
            ReportScheduleFrequency.Daily, null, null, 8, "Asia/Kolkata", new DateTimeOffset(2026, 10, 5, 3, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2026, 10, 6, 2, 30, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void A_monthly_slot_is_the_chosen_day_of_the_next_month_once_this_months_has_passed()
    {
        DateTimeOffset next = ReportScheduleCalendarRepository.NextRun(
            ReportScheduleFrequency.Monthly, null, 1, 9, "Asia/Kolkata", new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2026, 11, 1, 3, 30, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void India_is_half_past_five_ahead_even_without_a_zone_database()
    {
        Assert.Equal(TimeSpan.FromMinutes(330), ReportScheduleCalendarRepository.Zone("Asia/Kolkata").BaseUtcOffset);
        Assert.Equal(TimeSpan.FromMinutes(330), ReportScheduleCalendarRepository.Zone(null).BaseUtcOffset);
    }

    [Fact]
    public void The_attachment_is_named_for_the_schedule_and_cannot_carry_a_path()
    {
        string name = ReportScheduleRunRepository.FileName(new ReportSchedule { ReportKey = "dataset-sales-lines", Title = "Weekly ../sales: by site", LayoutJson = "{}" });

        Assert.StartsWith("Weekly-sales-by-site-", name);
        Assert.EndsWith(".xlsx", name);
        Assert.DoesNotContain("/", name);
    }

    private static readonly List<string> Allowed = ["reports.view", "reports.export", "sales.view"];

    private static BranchUserAccess Person(Guid id, string name, string email, List<string> permissions) =>
        new() { UserId = id, DisplayName = name, Email = email, Permissions = permissions };

    private static StubUsers Users(List<string> ownerPermissions) => new(
    [
        Person(Owner, "Owner", "owner@shop.in", ownerPermissions),
        Person(Recipient, "Anita", "anita@shop.in", ["reports.view"]),
    ]);

    private async Task<long> ScheduleAsync(Guid customer, Guid org, DateTimeOffset due)
    {
        await using ReportingDbContext db = _postgres.CreateContext(customer, org);

        ReportSchedule schedule = new()
        {
            ReportKey = "dataset-sales-lines",
            Title = "Weekly sales by site",
            LayoutJson = JsonSerializer.Serialize(new ReportQueryRequest { Columns = ["customer", "lineTotalBase"] }),
            Frequency = ReportScheduleFrequency.Weekly,
            DayOfWeek = DayOfWeek.Monday,
            SendAtHour = 8,
            TimeZoneId = "Asia/Kolkata",
            OwnerUserId = Owner,
            RecipientUserIds = [Recipient],
            NextRunAt = due,
        };

        db.ReportSchedules.Add(schedule);
        await db.SaveChangesAsync();
        return schedule.ReportScheduleId;
    }

    private static ReportScheduleRunRepository Run(
        ReportingDbContext db, RecordingMailer mailer, IBranchUsers users, RecordingExporter? exporter = null) =>
        new(db, new ReportCatalogRepository([new SalesLinesDataset()], db), users, exporter ?? new RecordingExporter(), mailer);

    private sealed class StubUsers(IReadOnlyList<BranchUserAccess>? users) : IBranchUsers
    {
        public Task<IReadOnlyList<BranchUserAccess>?> ListAsync(CancellationToken ct) => Task.FromResult(users);
    }

    private sealed class RecordingMailer : IReportMailer
    {
        private readonly object _gate = new();

        public List<ReportMail> Sent { get; } = [];

        public Task SendAsync(ReportMail mail, CancellationToken ct)
        {
            lock (_gate)
            {
                Sent.Add(mail);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingExporter : IScheduledReportExporter
    {
        private int _calls;

        public int Calls => _calls;

        public Task<ScheduledExport> ExportAsync(
            string reportKey, ReportQueryRequest layout, IReadOnlySet<string> permissions, CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(new ScheduledExport([0x50, 0x4B], 12, null));
        }
    }
}
