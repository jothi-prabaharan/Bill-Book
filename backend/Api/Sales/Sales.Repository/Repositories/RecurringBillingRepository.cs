using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Shared.Kernel.Documents;
using Shared.Kernel.Persistence;
using Shared.Kernel.Recurring;
using Shared.Kernel.Tenancy;

namespace Sales.Repository.Repositories;

/// <summary>
/// Recurring invoices (TK-139): schedules that copy a template invoice into a
/// new one on each occurrence.
///
/// <b>There is no second posting path.</b> Each occurrence is an ordinary
/// <see cref="IInvoiceRepository.CreateAsync"/>, so numbering, tax at the rates
/// in force on the occurrence's date, credit limits and custom fields are the
/// invoice's own; with <c>AutoPost</c> it is then an ordinary
/// <see cref="IInvoiceRepository.PostAsync"/>.
///
/// <b>Once only, twice over.</b> An occurrence's invoice, its run row and the
/// schedule's step forward commit together. The run row is unique on schedule
/// and date — the fixed document key — and the schedule carries a row version,
/// so a rerun or a second worker that reaches the same occurrence fails its
/// save and rolls its invoice back with it, number included.
///
/// <b>Posting is a second transaction.</b> A post that is refused — a credit
/// limit, a closed period — leaves the invoice as a draft for someone to look at,
/// with the reason on the run, rather than undoing the invoice and trying again
/// every hour.
/// </summary>
public sealed class RecurringBillingRepository
{
    private readonly SalesDbContext _db;
    private readonly IInvoiceRepository _invoices;
    private readonly IBaseCurrencyProvider _baseCurrency;
    private readonly TimeProvider _clock;

    public RecurringBillingRepository(
        SalesDbContext db, IInvoiceRepository invoices, IBaseCurrencyProvider baseCurrency, TimeProvider clock)
    {
        _db = db;
        _invoices = invoices;
        _baseCurrency = baseCurrency;
        _clock = clock;
    }

    private DateOnly Today => DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);

    // ---- Schedules ---------------------------------------------------------

    public async Task<List<RecurringInvoiceScheduleView>> ListAsync(CancellationToken ct)
    {
        List<RecurringInvoiceScheduleView> rows = await (
            from s in _db.RecurringInvoiceSchedules.AsNoTracking()
            join i in _db.Invoices.AsNoTracking() on s.TemplateInvoiceId equals i.InvoiceId
            orderby s.Status, s.NextRunDate, s.Name
            select new RecurringInvoiceScheduleView
            {
                RecurringInvoiceScheduleId = s.RecurringInvoiceScheduleId,
                Name = s.Name,
                TemplateInvoiceId = s.TemplateInvoiceId,
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
                PlaceOfSupplyStateCode = s.PlaceOfSupplyStateCode,
                LastRunAt = s.LastRunAt,
                LastError = s.LastError,
            }).ToListAsync(ct);

        return rows;
    }

    public async Task<RecurringInvoiceScheduleView?> GetAsync(long scheduleId, CancellationToken ct)
    {
        RecurringInvoiceScheduleView? view = (await ListAsync(ct))
            .FirstOrDefault(v => v.RecurringInvoiceScheduleId == scheduleId);

        if (view is null)
        {
            return null;
        }

        view.Runs = await (
            from r in _db.RecurringInvoiceRuns.AsNoTracking()
            join i in _db.Invoices.AsNoTracking() on r.InvoiceId equals i.InvoiceId
            where r.RecurringInvoiceScheduleId == scheduleId
            orderby r.OccurrenceDate descending
            select new RecurringInvoiceRunView
            {
                OccurrenceDate = r.OccurrenceDate,
                InvoiceId = r.InvoiceId,
                DocumentNo = i.DocumentNo,
                Posted = r.Posted,
                PostError = r.PostError,
            }).Take(100).ToListAsync(ct);

        return view;
    }

    public async Task<RecurringScheduleResult> CreateAsync(SaveRecurringInvoiceScheduleRequest request, CancellationToken ct)
    {
        if (await RefusalAsync(request, ct) is string refused)
        {
            return new RecurringScheduleResult(RecurringScheduleOutcome.Invalid, Detail: refused);
        }

        RecurringInvoiceSchedule schedule = new()
        {
            Name = request.Name.Trim(),
            TemplateInvoiceId = request.TemplateInvoiceId,
            Frequency = request.Frequency,
            Every = request.Every,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            MaxOccurrences = request.MaxOccurrences,
            AutoPost = request.AutoPost,
            PlaceOfSupplyStateCode = Blank(request.PlaceOfSupplyStateCode),
            NextOccurrenceIndex = 0,
            Status = RecurringScheduleStatus.Active,
        };
        schedule.NextRunDate = NextDate(schedule);
        if (schedule.NextRunDate is null)
        {
            schedule.Status = RecurringScheduleStatus.Ended;
        }

        _db.RecurringInvoiceSchedules.Add(schedule);
        await _db.SaveChangesAsync(ct);

        return new RecurringScheduleResult(RecurringScheduleOutcome.Ok, schedule.RecurringInvoiceScheduleId);
    }

    /// <summary>
    /// The name, end, limit, posting and place of supply can change at any time.
    /// The template, start and frequency cannot once an occurrence has been
    /// raised: they decide which dates the earlier invoices were raised for.
    /// </summary>
    public async Task<RecurringScheduleResult> UpdateAsync(
        long scheduleId, SaveRecurringInvoiceScheduleRequest request, CancellationToken ct)
    {
        RecurringInvoiceSchedule? schedule = await _db.RecurringInvoiceSchedules
            .FirstOrDefaultAsync(s => s.RecurringInvoiceScheduleId == scheduleId, ct);
        if (schedule is null)
        {
            return new RecurringScheduleResult(RecurringScheduleOutcome.NotFound);
        }

        if (await RefusalAsync(request, ct) is string refused)
        {
            return new RecurringScheduleResult(RecurringScheduleOutcome.Invalid, Detail: refused);
        }

        bool reshaped = schedule.TemplateInvoiceId != request.TemplateInvoiceId
            || schedule.StartDate != request.StartDate
            || schedule.Frequency != request.Frequency
            || schedule.Every != request.Every;

        if (reshaped && schedule.OccurrencesCreated > 0)
        {
            return new RecurringScheduleResult(
                RecurringScheduleOutcome.Conflict,
                Detail: "The template, start date and frequency cannot change once an invoice has been raised. End this schedule and start a new one.");
        }

        schedule.Name = request.Name.Trim();
        schedule.TemplateInvoiceId = request.TemplateInvoiceId;
        schedule.StartDate = request.StartDate;
        schedule.Frequency = request.Frequency;
        schedule.Every = request.Every;
        schedule.EndDate = request.EndDate;
        schedule.MaxOccurrences = request.MaxOccurrences;
        schedule.AutoPost = request.AutoPost;
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
        RecurringInvoiceSchedule? schedule = await _db.RecurringInvoiceSchedules
            .FirstOrDefaultAsync(s => s.RecurringInvoiceScheduleId == scheduleId, ct);
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

    private async Task<string?> RefusalAsync(SaveRecurringInvoiceScheduleRequest request, CancellationToken ct)
    {
        if (request.EndDate is DateOnly end && end < request.StartDate)
        {
            return "The end date cannot be before the start date.";
        }

        var template = await _db.Invoices.AsNoTracking()
            .Where(i => i.InvoiceId == request.TemplateInvoiceId)
            .Select(i => new { i.TransactionTypeCode, i.Status, i.CurrencyCode })
            .FirstOrDefaultAsync(ct);

        if (template is null)
        {
            return "The invoice to repeat was not found.";
        }

        if (template.TransactionTypeCode != "INV")
        {
            return "Only a tax invoice can be repeated, not a till sale.";
        }

        if (template.Status == DocumentStatus.Void)
        {
            return "A voided invoice cannot be repeated.";
        }

        // An exchange rate is a snapshot on the document's date and is never
        // looked up live, so a copy cannot carry the template's; a foreign
        // currency invoice is raised by hand with that day's rate.
        string? baseCurrency = await _baseCurrency.GetBaseCurrencyAsync(ct);
        if (baseCurrency is not null && !string.Equals(template.CurrencyCode, baseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return "Only an invoice in the branch's own currency can be repeated.";
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

        List<long> due = await _db.RecurringInvoiceSchedules.AsNoTracking()
            .Where(s => s.Status == RecurringScheduleStatus.Active && s.NextRunDate != null && s.NextRunDate <= day)
            .OrderBy(s => s.NextRunDate).ThenBy(s => s.RecurringInvoiceScheduleId)
            .Select(s => s.RecurringInvoiceScheduleId)
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

        long invoiceId;
        bool autoPost;
        DateOnly occurrence;

        await using (ITransactionScope tx = await _db.Database.BeginScopeAsync(ct))
        {
            RecurringInvoiceSchedule? schedule = await _db.RecurringInvoiceSchedules
                .FirstOrDefaultAsync(s => s.RecurringInvoiceScheduleId == scheduleId
                    && s.Status == RecurringScheduleStatus.Active
                    && s.NextRunDate != null
                    && s.NextRunDate <= day, ct);

            if (schedule is null)
            {
                return OccurrenceOutcome.NothingDue;
            }

            occurrence = schedule.NextRunDate!.Value;
            autoPost = schedule.AutoPost;

            SaveInvoiceRequest? request = await CopyTemplateAsync(schedule, occurrence, ct);
            InvoiceResult created = request is null
                ? new InvoiceResult(InvoiceOutcome.NotFound, Detail: "The invoice this schedule repeats no longer exists.")
                : await _invoices.CreateAsync(request, ct);

            if (created.Outcome != InvoiceOutcome.Ok)
            {
                await tx.RollbackAsync(ct);
                await RecordErrorAsync(scheduleId, created.Detail ?? $"The invoice was refused ({created.Outcome}).", ct);
                response.Failed++;
                return OccurrenceOutcome.Failed;
            }

            invoiceId = created.InvoiceId;

            _db.RecurringInvoiceRuns.Add(new RecurringInvoiceRun
            {
                RecurringInvoiceScheduleId = scheduleId,
                OccurrenceDate = occurrence,
                OccurrenceIndex = schedule.NextOccurrenceIndex,
                InvoiceId = invoiceId,
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
                // Another run reached this occurrence first; its invoice stands
                // and this one rolls back with its number.
                await tx.RollbackAsync(ct);
                return OccurrenceOutcome.NothingDue;
            }

            await tx.CommitAsync(ct);
        }

        response.Created++;

        if (autoPost)
        {
            await PostOccurrenceAsync(invoiceId, response, ct);
        }

        return OccurrenceOutcome.Raised;
    }

    private async Task PostOccurrenceAsync(long invoiceId, RunRecurringResponse response, CancellationToken ct)
    {
        _db.ChangeTracker.Clear();

        string? error;
        await using (ITransactionScope tx = await _db.Database.BeginScopeAsync(ct))
        {
            InvoiceResult posted = await _invoices.PostAsync(invoiceId, ct);
            if (posted.Outcome == InvoiceOutcome.Ok)
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
        RecurringInvoiceRun run = await _db.RecurringInvoiceRuns.FirstAsync(r => r.InvoiceId == invoiceId, ct);
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
        RecurringInvoiceSchedule? schedule = await _db.RecurringInvoiceSchedules
            .FirstOrDefaultAsync(s => s.RecurringInvoiceScheduleId == scheduleId, ct);
        if (schedule is null)
        {
            return;
        }

        schedule.LastRunAt = _clock.GetUtcNow();
        schedule.LastError = Clip(error);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The template's customer and lines as a new invoice dated on the
    /// occurrence. The due date keeps the template's term length. Batches and
    /// order links are left behind: the batch may be sold out and the order was
    /// the template's.
    /// </summary>
    private async Task<SaveInvoiceRequest?> CopyTemplateAsync(
        RecurringInvoiceSchedule schedule, DateOnly occurrence, CancellationToken ct)
    {
        Invoice? t = await _db.Invoices.AsNoTracking()
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.InvoiceId == schedule.TemplateInvoiceId, ct);

        if (t is null)
        {
            return null;
        }

        int termDays = t.DueDate is DateOnly due ? Math.Max(0, due.DayNumber - t.DocumentDate.DayNumber) : 0;

        return new SaveInvoiceRequest
        {
            DocumentDate = occurrence,
            DueDate = occurrence.AddDays(termDays),
            ContactId = t.ContactId,
            PaymentTermId = t.PaymentTermId,
            ContactGstin = t.ContactGstin,
            PlaceOfSupplyStateCode = schedule.PlaceOfSupplyStateCode,
            BillingAddress = t.BillingAddress,
            ShippingAddress = t.ShippingAddress,
            PrintTemplateId = t.PrintTemplateId,
            Notes = t.Notes,
            TermsAndConditions = t.TermsAndConditions,
            CustomFields = CustomFieldsOf(t.CustomFields),
            Lines = [.. t.Lines.OrderBy(l => l.LineNumber).Select(l => new SaveInvoiceLineRequest
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
                LineNotes = l.LineNotes,
            })],
        };
    }

    private static Dictionary<string, JsonElement>? CustomFieldsOf(JsonDocument? document)
    {
        if (document is null || document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
    }

    private static DateOnly? NextDate(RecurringInvoiceSchedule s) =>
        RecurrenceCalendar.Next(s.StartDate, s.Frequency, s.Every, s.NextOccurrenceIndex, s.EndDate, s.MaxOccurrences);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Clip(string? text) => text is null || text.Length <= 500 ? text : text[..500];
}
