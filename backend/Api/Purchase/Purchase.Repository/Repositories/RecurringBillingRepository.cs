using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Purchase.Entity.Models;
using Purchase.Entity.TableEntities;
using Shared.Kernel.Documents;
using Shared.Kernel.Persistence;
using Shared.Kernel.Recurring;
using Shared.Kernel.Tenancy;

namespace Purchase.Repository.Repositories;

/// <summary>
/// Recurring bills (TK-139): schedules that copy a template bill into a new
/// one on each occurrence. The same shape as Sales' recurring invoices.
///
/// <b>There is no second posting path.</b> Each occurrence is an ordinary
/// <see cref="BillRepository.CreateAsync"/>, so numbering, tax at the rates
/// in force on the occurrence's date, TDS and custom fields are the bill's own; with <c>AutoPost</c> it is then an ordinary
/// <see cref="BillRepository.PostAsync"/>.
///
/// <b>Once only, twice over.</b> An occurrence's bill, its run row and the
/// schedule's step forward commit together. The run row is unique on schedule
/// and date — the fixed document key — and the schedule carries a row version,
/// so a rerun or a second worker that reaches the same occurrence fails its
/// save and rolls its bill back with it, number included.
///
/// <b>Posting is a second transaction.</b> A post that is refused — a closed
/// period, a vendor bill number still to be typed in — leaves the bill as a draft for someone to look at,
/// with the reason on the run, rather than undoing the bill and trying again
/// every hour.
/// </summary>
public sealed class RecurringBillingRepository
{
    private readonly PurchaseDbContext _db;
    private readonly BillRepository _bills;
    private readonly IBaseCurrencyProvider _baseCurrency;
    private readonly TimeProvider _clock;

    public RecurringBillingRepository(
        PurchaseDbContext db, BillRepository bills, IBaseCurrencyProvider baseCurrency, TimeProvider clock)
    {
        _db = db;
        _bills = bills;
        _baseCurrency = baseCurrency;
        _clock = clock;
    }

    private DateOnly Today => DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);

    // ---- Schedules ---------------------------------------------------------

    public async Task<List<RecurringBillScheduleView>> ListAsync(CancellationToken ct)
    {
        List<RecurringBillScheduleView> rows = await (
            from s in _db.RecurringBillSchedules.AsNoTracking()
            join i in _db.Bills.AsNoTracking() on s.TemplateBillId equals i.BillId
            orderby s.Status, s.NextRunDate, s.Name
            select new RecurringBillScheduleView
            {
                RecurringBillScheduleId = s.RecurringBillScheduleId,
                Name = s.Name,
                TemplateBillId = s.TemplateBillId,
                TemplateDocumentNo = i.DocumentNo,
                ContactId = i.ContactId,
                TemplateTotal = i.TotalAmount,
                Frequency = s.Frequency.ToString(),
                Every = s.Every,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                MaxOccurrences = s.MaxOccurrences,
                NextRunDate = s.NextRunDate,
                OccurrencesCreated = s.OccurrencesCreated,
                Status = s.Status.ToString(),
                AutoPost = s.AutoPost,
                VendorBillNoPrefix = s.VendorBillNoPrefix,
                PlaceOfSupplyStateCode = s.PlaceOfSupplyStateCode,
                LastRunAt = s.LastRunAt,
                LastError = s.LastError,
            }).ToListAsync(ct);

        return rows;
    }

    public async Task<RecurringBillScheduleView?> GetAsync(long scheduleId, CancellationToken ct)
    {
        RecurringBillScheduleView? view = (await ListAsync(ct))
            .FirstOrDefault(v => v.RecurringBillScheduleId == scheduleId);

        if (view is null)
        {
            return null;
        }

        view.Runs = await (
            from r in _db.RecurringBillRuns.AsNoTracking()
            join i in _db.Bills.AsNoTracking() on r.BillId equals i.BillId
            where r.RecurringBillScheduleId == scheduleId
            orderby r.OccurrenceDate descending
            select new RecurringBillRunView
            {
                OccurrenceDate = r.OccurrenceDate,
                BillId = r.BillId,
                DocumentNo = i.DocumentNo,
                Posted = r.Posted,
                PostError = r.PostError,
            }).Take(100).ToListAsync(ct);

        return view;
    }

    public async Task<RecurringScheduleResult> CreateAsync(SaveRecurringBillScheduleRequest request, CancellationToken ct)
    {
        if (await RefusalAsync(request, ct) is string refused)
        {
            return new RecurringScheduleResult(RecurringScheduleOutcome.Invalid, Detail: refused);
        }

        RecurringBillSchedule schedule = new()
        {
            Name = request.Name.Trim(),
            TemplateBillId = request.TemplateBillId,
            Frequency = request.Frequency,
            Every = request.Every,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            MaxOccurrences = request.MaxOccurrences,
            AutoPost = request.AutoPost,
            VendorBillNoPrefix = Blank(request.VendorBillNoPrefix) ?? "REC-",
            PlaceOfSupplyStateCode = Blank(request.PlaceOfSupplyStateCode),
            NextOccurrenceIndex = 0,
            Status = RecurringScheduleStatus.Active,
        };
        schedule.NextRunDate = NextDate(schedule);
        if (schedule.NextRunDate is null)
        {
            schedule.Status = RecurringScheduleStatus.Ended;
        }

        _db.RecurringBillSchedules.Add(schedule);
        await _db.SaveChangesAsync(ct);

        return new RecurringScheduleResult(RecurringScheduleOutcome.Ok, schedule.RecurringBillScheduleId);
    }

    /// <summary>
    /// The name, end, limit, posting and place of supply can change at any time.
    /// The template, start and frequency cannot once an occurrence has been
    /// raised: they decide which dates the earlier bills were raised for.
    /// </summary>
    public async Task<RecurringScheduleResult> UpdateAsync(
        long scheduleId, SaveRecurringBillScheduleRequest request, CancellationToken ct)
    {
        RecurringBillSchedule? schedule = await _db.RecurringBillSchedules
            .FirstOrDefaultAsync(s => s.RecurringBillScheduleId == scheduleId, ct);
        if (schedule is null)
        {
            return new RecurringScheduleResult(RecurringScheduleOutcome.NotFound);
        }

        if (await RefusalAsync(request, ct) is string refused)
        {
            return new RecurringScheduleResult(RecurringScheduleOutcome.Invalid, Detail: refused);
        }

        bool reshaped = schedule.TemplateBillId != request.TemplateBillId
            || schedule.StartDate != request.StartDate
            || schedule.Frequency != request.Frequency
            || schedule.Every != request.Every;

        if (reshaped && schedule.OccurrencesCreated > 0)
        {
            return new RecurringScheduleResult(
                RecurringScheduleOutcome.Conflict,
                Detail: "The template, start date and frequency cannot change once a bill has been raised. End this schedule and start a new one.");
        }

        schedule.Name = request.Name.Trim();
        schedule.TemplateBillId = request.TemplateBillId;
        schedule.StartDate = request.StartDate;
        schedule.Frequency = request.Frequency;
        schedule.Every = request.Every;
        schedule.EndDate = request.EndDate;
        schedule.MaxOccurrences = request.MaxOccurrences;
        schedule.AutoPost = request.AutoPost;
        schedule.VendorBillNoPrefix = Blank(request.VendorBillNoPrefix) ?? "REC-";
        schedule.PlaceOfSupplyStateCode = Blank(request.PlaceOfSupplyStateCode);

        if (schedule.Status != RecurringScheduleStatus.Paused)
        {
            schedule.NextRunDate = NextDate(schedule);
            schedule.Status = schedule.NextRunDate is null ? RecurringScheduleStatus.Ended : RecurringScheduleStatus.Active;
        }

        await _db.SaveChangesAsync(ct);
        return new RecurringScheduleResult(RecurringScheduleOutcome.Ok, scheduleId);
    }

    public Task<RecurringScheduleResult> PauseAsync(long scheduleId, CancellationToken ct) =>
        ChangeStatusAsync(scheduleId, RecurringScheduleStatus.Paused, ct);

    public Task<RecurringScheduleResult> ResumeAsync(long scheduleId, CancellationToken ct) =>
        ChangeStatusAsync(scheduleId, RecurringScheduleStatus.Active, ct);

    public Task<RecurringScheduleResult> EndAsync(long scheduleId, CancellationToken ct) =>
        ChangeStatusAsync(scheduleId, RecurringScheduleStatus.Ended, ct);

    private async Task<RecurringScheduleResult> ChangeStatusAsync(
        long scheduleId, RecurringScheduleStatus to, CancellationToken ct)
    {
        RecurringBillSchedule? schedule = await _db.RecurringBillSchedules
            .FirstOrDefaultAsync(s => s.RecurringBillScheduleId == scheduleId, ct);
        if (schedule is null)
        {
            return new RecurringScheduleResult(RecurringScheduleOutcome.NotFound);
        }

        if (schedule.Status == RecurringScheduleStatus.Ended)
        {
            return new RecurringScheduleResult(
                RecurringScheduleOutcome.Conflict, Detail: "This schedule has ended. Start a new one instead.");
        }

        switch (to)
        {
            case RecurringScheduleStatus.Paused:
                schedule.Status = RecurringScheduleStatus.Paused;
                break;

            case RecurringScheduleStatus.Active:
                // What fell due while paused is skipped, not raised all at once.
                schedule.NextOccurrenceIndex = RecurrenceCalendar.FirstIndexOnOrAfter(
                    schedule.StartDate, schedule.Frequency, schedule.Every, schedule.NextOccurrenceIndex, Today);
                schedule.NextRunDate = NextDate(schedule);
                schedule.Status = schedule.NextRunDate is null ? RecurringScheduleStatus.Ended : RecurringScheduleStatus.Active;
                break;

            default:
                schedule.Status = RecurringScheduleStatus.Ended;
                schedule.NextRunDate = null;
                break;
        }

        await _db.SaveChangesAsync(ct);
        return new RecurringScheduleResult(RecurringScheduleOutcome.Ok, scheduleId);
    }

    private async Task<string?> RefusalAsync(SaveRecurringBillScheduleRequest request, CancellationToken ct)
    {
        if (request.EndDate is DateOnly end && end < request.StartDate)
        {
            return "The end date cannot be before the start date.";
        }

        var template = await _db.Bills.AsNoTracking()
            .Where(b => b.BillId == request.TemplateBillId)
            .Select(b => new { b.Status, b.CurrencyCode })
            .FirstOrDefaultAsync(ct);

        if (template is null)
        {
            return "The bill to repeat was not found.";
        }

        if (template.Status == DocumentStatus.Void)
        {
            return "A voided bill cannot be repeated.";
        }

        // An exchange rate is a snapshot on the document's date and is never
        // looked up live, so a copy cannot carry the template's.
        string? baseCurrency = await _baseCurrency.GetBaseCurrencyAsync(ct);
        if (baseCurrency is not null && !string.Equals(template.CurrencyCode, baseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return "Only a bill in the branch's own currency can be repeated.";
        }

        return null;
    }

    // ---- Running -----------------------------------------------------------

    /// <summary>
    /// Raises every occurrence due on or before <paramref name="asOf"/>, oldest
    /// first, at most <see cref="RecurrenceCalendar.MaxCatchUp"/> per schedule
    /// per call. Each occurrence is its own transaction.
    /// </summary>
    public async Task<RunRecurringResponse> RunDueAsync(DateOnly? asOf, CancellationToken ct)
    {
        DateOnly day = asOf ?? Today;
        RunRecurringResponse response = new();

        List<long> due = await _db.RecurringBillSchedules.AsNoTracking()
            .Where(s => s.Status == RecurringScheduleStatus.Active && s.NextRunDate != null && s.NextRunDate <= day)
            .OrderBy(s => s.NextRunDate).ThenBy(s => s.RecurringBillScheduleId)
            .Select(s => s.RecurringBillScheduleId)
            .ToListAsync(ct);

        foreach (long scheduleId in due)
        {
            for (int n = 0; n < RecurrenceCalendar.MaxCatchUp; n++)
            {
                OccurrenceOutcome outcome = await RunOccurrenceAsync(scheduleId, day, response, ct);
                if (outcome != OccurrenceOutcome.Raised)
                {
                    break;
                }
            }
        }

        return response;
    }

    private enum OccurrenceOutcome
    {
        Raised,
        NothingDue,
        Failed,
    }

    private async Task<OccurrenceOutcome> RunOccurrenceAsync(
        long scheduleId, DateOnly day, RunRecurringResponse response, CancellationToken ct)
    {
        _db.ChangeTracker.Clear();

        long billId;
        bool autoPost;
        DateOnly occurrence;

        await using (ITransactionScope tx = await _db.Database.BeginScopeAsync(ct))
        {
            RecurringBillSchedule? schedule = await _db.RecurringBillSchedules
                .FirstOrDefaultAsync(s => s.RecurringBillScheduleId == scheduleId
                    && s.Status == RecurringScheduleStatus.Active
                    && s.NextRunDate != null
                    && s.NextRunDate <= day, ct);

            if (schedule is null)
            {
                return OccurrenceOutcome.NothingDue;
            }

            occurrence = schedule.NextRunDate!.Value;
            autoPost = schedule.AutoPost;

            SaveBillRequest? request = await CopyTemplateAsync(schedule, occurrence, ct);
            BillResult created = request is null
                ? new BillResult(BillOutcome.NotFound, Detail: "The bill this schedule repeats no longer exists.")
                : await _bills.CreateAsync(request, ct);

            if (created.Outcome != BillOutcome.Ok)
            {
                await tx.RollbackAsync(ct);
                await RecordErrorAsync(scheduleId, created.Detail ?? $"The bill was refused ({created.Outcome}).", ct);
                response.Failed++;
                return OccurrenceOutcome.Failed;
            }

            billId = created.BillId;

            _db.RecurringBillRuns.Add(new RecurringBillRun
            {
                RecurringBillScheduleId = scheduleId,
                OccurrenceDate = occurrence,
                OccurrenceIndex = schedule.NextOccurrenceIndex,
                BillId = billId,
            });

            schedule.OccurrencesCreated++;
            schedule.NextOccurrenceIndex++;
            schedule.NextRunDate = NextDate(schedule);
            if (schedule.NextRunDate is null)
            {
                schedule.Status = RecurringScheduleStatus.Ended;
            }

            schedule.LastRunAt = _clock.GetUtcNow();
            schedule.LastError = null;

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException || IsUniqueViolation(ex))
            {
                // Another run reached this occurrence first; its bill stands
                // and this one rolls back with its number.
                await tx.RollbackAsync(ct);
                return OccurrenceOutcome.NothingDue;
            }

            await tx.CommitAsync(ct);
        }

        response.Created++;

        if (autoPost)
        {
            await PostOccurrenceAsync(billId, response, ct);
        }

        return OccurrenceOutcome.Raised;
    }

    private async Task PostOccurrenceAsync(long billId, RunRecurringResponse response, CancellationToken ct)
    {
        _db.ChangeTracker.Clear();

        string? error;
        await using (ITransactionScope tx = await _db.Database.BeginScopeAsync(ct))
        {
            BillResult posted = await _bills.PostAsync(billId, ct);
            if (posted.Outcome == BillOutcome.Ok)
            {
                await tx.CommitAsync(ct);
                error = null;
            }
            else
            {
                await tx.RollbackAsync(ct);
                error = posted.Detail ?? $"Posting was refused ({posted.Outcome}).";
            }
        }

        _db.ChangeTracker.Clear();
        RecurringBillRun run = await _db.RecurringBillRuns.FirstAsync(r => r.BillId == billId, ct);
        run.Posted = error is null;
        run.PostError = Clip(error);
        await _db.SaveChangesAsync(ct);

        if (error is null)
        {
            response.Posted++;
        }
    }

    private async Task RecordErrorAsync(long scheduleId, string error, CancellationToken ct)
    {
        _db.ChangeTracker.Clear();
        RecurringBillSchedule? schedule = await _db.RecurringBillSchedules
            .FirstOrDefaultAsync(s => s.RecurringBillScheduleId == scheduleId, ct);
        if (schedule is null)
        {
            return;
        }

        schedule.LastRunAt = _clock.GetUtcNow();
        schedule.LastError = Clip(error);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The template's vendor and lines as a new bill dated on the occurrence.
    /// The due date keeps the template's term length; the vendor bill number is
    /// the schedule's prefix and the date, until the real one is typed in.
    /// Receipts, orders and batches are left behind: they were the template's.
    /// </summary>
    private async Task<SaveBillRequest?> CopyTemplateAsync(
        RecurringBillSchedule schedule, DateOnly occurrence, CancellationToken ct)
    {
        Bill? t = await _db.Bills.AsNoTracking()
            .Include(b => b.Lines)
            .FirstOrDefaultAsync(b => b.BillId == schedule.TemplateBillId, ct);

        if (t is null)
        {
            return null;
        }

        int termDays = Math.Max(0, t.DueDate.DayNumber - t.DocumentDate.DayNumber);

        return new SaveBillRequest
        {
            DocumentDate = occurrence,
            DueDate = occurrence.AddDays(termDays),
            ContactId = t.ContactId,
            VendorBillNo = VendorBillNo(schedule.VendorBillNoPrefix, occurrence),
            VendorBillDate = occurrence,
            PaymentTermId = t.PaymentTermId,
            ContactGstin = t.ContactGstin,
            PlaceOfSupplyStateCode = schedule.PlaceOfSupplyStateCode,
            BillingAddress = t.BillingAddress,
            ShippingAddress = t.ShippingAddress,
            IsReverseCharge = t.IsReverseCharge,
            PrintTemplateId = t.PrintTemplateId,
            Notes = t.Notes,
            TermsAndConditions = t.TermsAndConditions,
            CustomFields = CustomFieldsOf(t.CustomFields),
            Lines = [.. t.Lines.OrderBy(l => l.LineNumber).Select(l => new SaveBillLineRequest
            {
                ItemId = l.ItemId,
                Description = l.Description,
                HsnSacCode = l.HsnSacCode,
                WarehouseId = l.WarehouseId,
                Quantity = l.Quantity,
                UomId = l.UomId,
                ConversionFactor = l.ConversionFactor,
                UnitPrice = l.UnitPrice,
                IsPriceInclusive = l.IsPriceInclusive,
                DiscountPercent = l.DiscountPercent,
                DiscountAmount = l.DiscountAmount,
                TaxTreatment = l.TaxTreatment,
                TaxGroupId = l.TaxGroupId,
                LineType = l.LineType,
                AccountId = l.AccountId,
                FixedAssetCategoryId = l.FixedAssetCategoryId,
                ProjectId = l.ProjectId,
                IsBillable = l.IsBillable,
                MarkupPercent = l.MarkupPercent,
                LineNotes = l.LineNotes,
                ItcEligibility = l.ItcEligibility,
            })],
        };
    }

    /// <summary>The prefix and the occurrence date, clipped to the column's 50 characters.</summary>
    public static string VendorBillNo(string prefix, DateOnly occurrence)
    {
        string number = $"{prefix.Trim()}{occurrence:yyyyMMdd}";
        return number.Length <= 50 ? number : number[^50..];
    }

    private static Dictionary<string, JsonElement>? CustomFieldsOf(JsonDocument? document)
    {
        if (document is null || document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
    }

    private static DateOnly? NextDate(RecurringBillSchedule s) =>
        RecurrenceCalendar.Next(s.StartDate, s.Frequency, s.Every, s.NextOccurrenceIndex, s.EndDate, s.MaxOccurrences);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Clip(string? text) => text is null || text.Length <= 500 ? text : text[..500];
}
