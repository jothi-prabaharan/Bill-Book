using Fee.Entity.Enums;
using Fee.Entity.Models;
using Fee.Entity.TableEntities;
using Fee.Repository;
using Fee.Repository.SeedData;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;
using Shared.Kernel.School;
using Shared.Kernel.Tenancy;

namespace Fee.Api.Services;

/// <summary>
/// Fee demands (S4, TK-64).
///
/// <list type="bullet">
/// <item><b>Generating a period is idempotent</b>: one demand per enrolment,
/// structure and period, enforced by a unique index, so a second run finds
/// what the first raised and raises nothing.</item>
/// <item><b>Generated demands are drafts.</b> Posting numbers one from FDM and
/// posts it through Accounting; Accounting replaces by document, so a post
/// retried after a failure lands once.</item>
/// <item><b>A posted demand is never edited.</b> It is voided, and only while
/// nothing has been paid against it.</item>
/// </list>
/// </summary>
public sealed class DemandService
{
    public const string TypeCode = "FDM";

    private readonly FeeDbContext _db;
    private readonly INumberGenerator _numbers;
    private readonly IStudentClient _sis;
    private readonly IFeeLedger _ledger;
    private readonly IBaseCurrencyProvider _currency;
    private readonly ITenantContext _tenant;
    private readonly ILogger<DemandService> _log;

    public DemandService(
        FeeDbContext db,
        INumberGenerator numbers,
        IStudentClient sis,
        IFeeLedger ledger,
        IBaseCurrencyProvider currency,
        ITenantContext tenant,
        ILogger<DemandService> log)
    {
        _db = db;
        _numbers = numbers;
        _sis = sis;
        _ledger = ledger;
        _currency = currency;
        _tenant = tenant;
        _log = log;
    }

    public async Task<FeeResult> GenerateAsync(GenerateDemandsRequest request, CancellationToken ct)
    {
        FeeStructure? structure = await _db.FeeStructures.AsNoTracking().Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.FeeStructureId == request.FeeStructureId, ct);
        if (structure is null)
        {
            return FeeResult.Fail(FeeOutcome.NotFound);
        }

        if (!structure.IsActive)
        {
            return FeeResult.Fail(FeeOutcome.StateRule, "That fee structure is inactive.");
        }

        (_, int month) = FeeRules.Period(request.PeriodKey);
        List<FeeStructureLine> due = [.. structure.Lines.Where(l => FeeRules.IsDue(l.Frequency, structure.FirstMonth, month))];
        var response = new GenerateDemandsResponse();
        if (due.Count == 0)
        {
            response.Notes.Add("Nothing in the structure falls due in that month.");
            return FeeResult.Ok(structure.FeeStructureId, response);
        }

        IReadOnlyList<EnrolmentInfo> enrolments;
        string? currency;
        try
        {
            enrolments = await _sis.EnrolmentsAsync(new EnrolmentQueryRequest
            {
                AcademicYearId = structure.AcademicYearId,
                SchoolClassId = structure.SchoolClassId,
            }, ct);
            currency = await _currency.GetBaseCurrencyAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogError(ex, "The class's students could not be read for fee structure {StructureId}.", structure.FeeStructureId);
            return FeeResult.Fail(FeeOutcome.Unavailable);
        }

        if (currency is null)
        {
            return FeeResult.Fail(FeeOutcome.Unavailable);
        }

        HashSet<long> raised = [.. await _db.FeeDemands
            .Where(d => d.FeeStructureId == structure.FeeStructureId && d.PeriodKey == request.PeriodKey)
            .Select(d => d.EnrolmentId)
            .ToListAsync(ct)];

        List<long> studentIds = [.. enrolments.Select(e => e.StudentId).Distinct()];
        List<FeeConcession> concessions = await _db.FeeConcessions.AsNoTracking()
            .Where(c => studentIds.Contains(c.StudentId) && c.IsApproved)
            .ToListAsync(ct);

        foreach (EnrolmentInfo enrolment in enrolments.Where(e => e.IsActive))
        {
            if (raised.Contains(enrolment.EnrolmentId))
            {
                response.AlreadyRaised++;
                continue;
            }

            if (enrolment.PrimaryGuardianContactId is not long contactId)
            {
                response.Skipped++;
                response.Notes.Add($"{enrolment.StudentName} ({enrolment.AdmissionNo}) has no primary guardian to bill.");
                continue;
            }

            var demand = new FeeDemand
            {
                StudentId = enrolment.StudentId,
                EnrolmentId = enrolment.EnrolmentId,
                FeeStructureId = structure.FeeStructureId,
                PeriodKey = request.PeriodKey,
                ContactId = contactId,
                DemandDate = request.DemandDate,
                DueDate = FeeRules.DueDate(request.PeriodKey, due.Min(l => l.DueDay)),
                CurrencyCode = currency,
                ExchangeRate = 1m,
            };

            List<FeeConcession> theirs = [.. concessions.Where(c => c.StudentId == enrolment.StudentId)];
            foreach (FeeStructureLine line in due)
            {
                decimal concession = FeeRules.BestConcession(line.Amount, theirs, line.FeeHeadId, request.DemandDate);
                demand.Lines.Add(new FeeDemandLine { FeeHeadId = line.FeeHeadId, Amount = line.Amount, ConcessionAmount = concession });
            }

            demand.TotalAmount = demand.Lines.Sum(l => l.Amount);
            demand.ConcessionAmount = demand.Lines.Sum(l => l.ConcessionAmount);
            demand.NetAmount = demand.TotalAmount - demand.ConcessionAmount;
            _db.FeeDemands.Add(demand);
            response.Created++;
        }

        await _db.SaveChangesAsync(ct);
        return FeeResult.Ok(structure.FeeStructureId, response);
    }

    public async Task<List<FeeDemandView>> ListAsync(long? contactId, long? feeStructureId, string? periodKey, bool openOnly, CancellationToken ct)
    {
        List<FeeDemand> rows = await _db.FeeDemands.AsNoTracking().Include(d => d.Lines)
            .Where(d => (contactId == null || d.ContactId == contactId)
                && (feeStructureId == null || d.FeeStructureId == feeStructureId)
                && (periodKey == null || d.PeriodKey == periodKey)
                && (!openOnly || (d.DocumentStatus == FeeDocumentStatus.Posted && d.PaidAmount < d.NetAmount)))
            .OrderBy(d => d.DueDate).ThenBy(d => d.FeeDemandId)
            .Take(2000)
            .ToListAsync(ct);

        return [.. rows.Select(View)];
    }

    /// <summary>
    /// Posts drafts: each numbered from FDM and posted through Accounting. A
    /// refusal stops the run, and the request's transaction takes back every
    /// number it took; the postings already made are replaced on the retry.
    /// </summary>
    public async Task<FeeResult> PostAsync(PostDemandsRequest request, CancellationToken ct)
    {
        IQueryable<FeeDemand> query = _db.FeeDemands.Include(d => d.Lines).Where(d => d.DocumentStatus == FeeDocumentStatus.Draft);
        if (request.FeeDemandIds.Count > 0)
        {
            query = query.Where(d => request.FeeDemandIds.Contains(d.FeeDemandId));
        }
        else if (request.FeeStructureId is long structureId && request.PeriodKey is { Length: 7 } period)
        {
            query = query.Where(d => d.FeeStructureId == structureId && d.PeriodKey == period);
        }
        else
        {
            return FeeResult.Fail(FeeOutcome.Invalid, "Choose the demands, or a structure and period, to post.");
        }

        List<FeeDemand> drafts = await query.OrderBy(d => d.FeeDemandId).ToListAsync(ct);
        Dictionary<long, FeeHead> heads = await _db.FeeHeads.AsNoTracking().ToDictionaryAsync(h => h.FeeHeadId, ct);

        foreach (FeeDemand demand in drafts)
        {
            demand.DemandNo ??= (await _numbers.NextAsync(FeeSeed.DemandSeriesCode, demand.DemandDate, ct)).Code;
            demand.DocumentStatus = FeeDocumentStatus.Posted;
            await _db.SaveChangesAsync(ct);

            FeeResult posted = await SendAsync(demand, FeeRules.DemandLegs(demand, heads), [], ct);
            if (posted.Outcome != FeeOutcome.Ok)
            {
                return posted;
            }
        }

        return FeeResult.Ok(drafts.Count, new { posted = drafts.Count });
    }

    public async Task<FeeResult> VoidAsync(long id, string reason, CancellationToken ct)
    {
        FeeDemand? demand = await _db.FeeDemands.Include(d => d.Lines).FirstOrDefaultAsync(d => d.FeeDemandId == id, ct);
        if (demand is null)
        {
            return FeeResult.Fail(FeeOutcome.NotFound);
        }

        if (demand.DocumentStatus == FeeDocumentStatus.Void)
        {
            return FeeResult.Fail(FeeOutcome.StateRule, "That demand is already void.");
        }

        if (demand.PaidAmount > 0 || await _db.FeeReceiptAllocations.AnyAsync(a => a.FeeDemandId == id, ct))
        {
            return FeeResult.Fail(FeeOutcome.StateRule, "A demand with money received against it cannot be voided. Void the receipt first.");
        }

        bool wasPosted = demand.DocumentStatus == FeeDocumentStatus.Posted;
        demand.DocumentStatus = FeeDocumentStatus.Void;
        demand.VoidReason = reason.Trim();
        await _db.SaveChangesAsync(ct);

        // A draft never reached the ledger; a posted demand's rows are withdrawn.
        if (!wasPosted)
        {
            return FeeResult.Ok(id);
        }

        FeeResult withdrawn = await SendAsync(demand, [], [LedgerType.Item, LedgerType.Control], ct);
        return withdrawn.Outcome == FeeOutcome.Ok ? FeeResult.Ok(id) : withdrawn;
    }

    private async Task<FeeResult> SendAsync(FeeDemand demand, List<LedgerLeg> legs, List<int> withdraw, CancellationToken ct)
    {
        (Guid customerId, Guid orgId) = _tenant.Require();
        LedgerOutcome outcome;
        try
        {
            outcome = await _ledger.PostAsync(new LedgerPosting
            {
                CustomerId = customerId,
                OrgId = orgId,
                TransactionTypeCode = TypeCode,
                TransactionId = demand.FeeDemandId,
                LedgerDate = demand.DemandDate,
                CurrencyCode = demand.CurrencyCode,
                ExchangeRate = demand.ExchangeRate,
                ContactId = demand.ContactId,
                DocumentNo = demand.DemandNo,
                WithdrawLedgerTypeIds = withdraw,
                Legs = legs,
            }, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogError(ex, "Fee demand {DemandId} could not reach the ledger.", demand.FeeDemandId);
            return FeeResult.Fail(FeeOutcome.Unavailable);
        }

        if (!outcome.Posted)
        {
            _log.LogError("The ledger refused fee demand {DemandId}: {Detail}", demand.FeeDemandId, outcome.Detail);
            return FeeResult.Fail(FeeOutcome.LedgerRefused);
        }

        return FeeResult.Ok(demand.FeeDemandId);
    }

    internal static FeeDemandView View(FeeDemand d) => new()
    {
        FeeDemandId = d.FeeDemandId,
        DemandNo = d.DemandNo,
        StudentId = d.StudentId,
        EnrolmentId = d.EnrolmentId,
        FeeStructureId = d.FeeStructureId,
        PeriodKey = d.PeriodKey,
        ContactId = d.ContactId,
        DemandDate = d.DemandDate,
        DueDate = d.DueDate,
        DocumentStatus = d.DocumentStatus,
        CurrencyCode = d.CurrencyCode,
        TotalAmount = d.TotalAmount,
        ConcessionAmount = d.ConcessionAmount,
        NetAmount = d.NetAmount,
        PaidAmount = d.PaidAmount,
        OpenAmount = d.DocumentStatus == FeeDocumentStatus.Posted ? d.NetAmount - d.PaidAmount : 0m,
        Lines = [.. d.Lines.Select(l => new FeeDemandLineView { FeeHeadId = l.FeeHeadId, Amount = l.Amount, ConcessionAmount = l.ConcessionAmount })],
    };
}
