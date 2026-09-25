using Fee.Entity.Models;
using Fee.Entity.TableEntities;
using Fee.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Ledgers;
using Shared.Kernel.School;

namespace Fee.Api.Services;

/// <summary>
/// Fee heads, structures and concessions (S4, TK-64). Ids that live in other
/// services — the income account, the year and class, the student — are
/// checked through them before they are stored.
/// </summary>
public sealed class FeeSetupService
{
    public const int IncomeType = 4;
    public const int LiabilityType = 2;

    private readonly FeeDbContext _db;
    private readonly IAccountDirectory _accounts;
    private readonly IStudentClient _sis;
    private readonly ILogger<FeeSetupService> _log;

    public FeeSetupService(FeeDbContext db, IAccountDirectory accounts, IStudentClient sis, ILogger<FeeSetupService> log)
    {
        _db = db;
        _accounts = accounts;
        _sis = sis;
        _log = log;
    }

    // ---- Heads -----------------------------------------------------------------

    public Task<List<FeeHeadView>> HeadsAsync(CancellationToken ct) =>
        _db.FeeHeads.AsNoTracking().OrderBy(h => h.Code)
            .Select(h => new FeeHeadView
            {
                FeeHeadId = h.FeeHeadId, Code = h.Code, Name = h.Name, IncomeAccountId = h.IncomeAccountId,
                IsRefundable = h.IsRefundable, HsnSacCode = h.HsnSacCode, IsActive = h.IsActive,
            })
            .ToListAsync(ct);

    public async Task<FeeResult> SaveHeadAsync(long? id, SaveFeeHeadRequest request, CancellationToken ct)
    {
        string code = request.Code.Trim().ToUpperInvariant();
        if (await _db.FeeHeads.AnyAsync(h => h.FeeHeadId != (id ?? 0) && h.Code == code, ct))
        {
            return FeeResult.Fail(FeeOutcome.Duplicate, "Another fee head already uses that code.");
        }

        if (request.IncomeAccountId is long accountId)
        {
            IReadOnlyList<AccountSummary> found;
            try
            {
                found = await _accounts.AccountsAsync([], [accountId], ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _log.LogError(ex, "The income account could not be checked.");
                return FeeResult.Fail(FeeOutcome.Unavailable);
            }

            int wanted = request.IsRefundable ? LiabilityType : IncomeType;
            if (found.FirstOrDefault() is not { IsActive: true, IsLock: false } account || account.AccountTypeId != wanted)
            {
                return FeeResult.Fail(FeeOutcome.Invalid, request.IsRefundable
                    ? "A refundable head posts to an active liability account of this branch."
                    : "Choose an active income account of this branch.");
            }
        }

        FeeHead? head = id is long existing ? await _db.FeeHeads.FirstOrDefaultAsync(h => h.FeeHeadId == existing, ct) : new FeeHead();
        if (head is null)
        {
            return FeeResult.Fail(FeeOutcome.NotFound);
        }

        head.Code = code;
        head.Name = request.Name.Trim();
        head.IncomeAccountId = request.IncomeAccountId;
        head.IsRefundable = request.IsRefundable;
        head.HsnSacCode = string.IsNullOrWhiteSpace(request.HsnSacCode) ? null : request.HsnSacCode.Trim();
        head.IsActive = request.IsActive;
        if (id is null)
        {
            _db.FeeHeads.Add(head);
        }

        await _db.SaveChangesAsync(ct);
        return FeeResult.Ok(head.FeeHeadId);
    }

    /// <summary>The accounts a fee head may post to: income, and liabilities for a refundable head.</summary>
    public Task<IReadOnlyList<AccountSummary>> PostableAccountsAsync(CancellationToken ct) =>
        _accounts.AccountsAsync([IncomeType, LiabilityType], [], ct);

    public Task<IReadOnlyList<BankAccountSummary>> BankAccountsAsync(CancellationToken ct) => _accounts.BankAccountsAsync(ct);

    // ---- Structures -------------------------------------------------------------

    public async Task<List<FeeStructureView>> StructuresAsync(long? academicYearId, CancellationToken ct)
    {
        List<FeeStructure> rows = await _db.FeeStructures.AsNoTracking().Include(s => s.Lines)
            .Where(s => academicYearId == null || s.AcademicYearId == academicYearId)
            .OrderBy(s => s.SchoolClassId).ThenBy(s => s.Name)
            .ToListAsync(ct);

        return [.. rows.Select(s => new FeeStructureView
        {
            FeeStructureId = s.FeeStructureId, AcademicYearId = s.AcademicYearId, SchoolClassId = s.SchoolClassId,
            Name = s.Name, FirstMonth = s.FirstMonth, IsActive = s.IsActive,
            Lines = [.. s.Lines.Select(l => new FeeStructureLineModel { FeeHeadId = l.FeeHeadId, Amount = l.Amount, Frequency = l.Frequency, DueDay = l.DueDay })],
        })];
    }

    public async Task<FeeResult> SaveStructureAsync(long? id, SaveFeeStructureRequest request, CancellationToken ct)
    {
        if (request.Lines.Count == 0)
        {
            return FeeResult.Fail(FeeOutcome.Invalid, "Add at least one fee to the structure.");
        }

        if (request.Lines.Select(l => (l.FeeHeadId, l.Frequency)).Distinct().Count() != request.Lines.Count)
        {
            return FeeResult.Fail(FeeOutcome.Invalid, "A fee head is listed twice with the same frequency.");
        }

        HashSet<long> headIds = [.. request.Lines.Select(l => l.FeeHeadId)];
        if (await _db.FeeHeads.CountAsync(h => headIds.Contains(h.FeeHeadId) && h.IsActive, ct) != headIds.Count)
        {
            return FeeResult.Fail(FeeOutcome.Invalid, "Choose active fee heads of this branch.");
        }

        try
        {
            AcademicCheckResponse check = await _sis.CheckAsync(request.AcademicYearId, request.SchoolClassId, null, ct);
            if (!check.YearExists || !check.ClassExists)
            {
                return FeeResult.Fail(FeeOutcome.Invalid, "Choose a school year and a class from this branch.");
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogError(ex, "The school year and class could not be checked.");
            return FeeResult.Fail(FeeOutcome.Unavailable);
        }

        string name = request.Name.Trim();
        if (await _db.FeeStructures.AnyAsync(s => s.FeeStructureId != (id ?? 0) && s.AcademicYearId == request.AcademicYearId
            && s.SchoolClassId == request.SchoolClassId && s.Name == name, ct))
        {
            return FeeResult.Fail(FeeOutcome.Duplicate, "That class already has a structure of that name for the year.");
        }

        FeeStructure? structure = id is long existing
            ? await _db.FeeStructures.Include(s => s.Lines).FirstOrDefaultAsync(s => s.FeeStructureId == existing, ct)
            : new FeeStructure();
        if (structure is null)
        {
            return FeeResult.Fail(FeeOutcome.NotFound);
        }

        // Demands already raised keep their own lines; a structure with demands
        // can still change what later periods bill, but not its year or class.
        if (id is not null && (structure.AcademicYearId != request.AcademicYearId || structure.SchoolClassId != request.SchoolClassId)
            && await _db.FeeDemands.AnyAsync(d => d.FeeStructureId == structure.FeeStructureId, ct))
        {
            return FeeResult.Fail(FeeOutcome.StateRule, "A structure that has billed students cannot move to another year or class.");
        }

        structure.AcademicYearId = request.AcademicYearId;
        structure.SchoolClassId = request.SchoolClassId;
        structure.Name = name;
        structure.FirstMonth = request.FirstMonth;
        structure.IsActive = request.IsActive;

        if (id is null)
        {
            _db.FeeStructures.Add(structure);
        }
        else
        {
            _db.FeeStructureLines.RemoveRange(structure.Lines);
            await _db.SaveChangesAsync(ct);
            structure.Lines.Clear();
        }

        foreach (FeeStructureLineModel l in request.Lines)
        {
            structure.Lines.Add(new FeeStructureLine { FeeHeadId = l.FeeHeadId, Amount = l.Amount, Frequency = l.Frequency, DueDay = l.DueDay });
        }

        await _db.SaveChangesAsync(ct);
        return FeeResult.Ok(structure.FeeStructureId);
    }

    // ---- Concessions ------------------------------------------------------------

    public Task<List<ConcessionView>> ConcessionsAsync(long? studentId, CancellationToken ct) =>
        _db.FeeConcessions.AsNoTracking()
            .Where(c => studentId == null || c.StudentId == studentId)
            .OrderByDescending(c => c.ValidFrom)
            .Select(c => new ConcessionView
            {
                FeeConcessionId = c.FeeConcessionId, StudentId = c.StudentId, FeeHeadId = c.FeeHeadId, ConcessionKind = c.ConcessionKind,
                Value = c.Value, Reason = c.Reason, ValidFrom = c.ValidFrom, ValidTo = c.ValidTo, IsApproved = c.IsApproved,
            })
            .ToListAsync(ct);

    /// <summary>A concession applies to demands raised after it is approved; ones already raised keep their amounts.</summary>
    public async Task<FeeResult> SaveConcessionAsync(long? id, SaveConcessionRequest request, bool mayApprove, CancellationToken ct)
    {
        if (request.ValidTo < request.ValidFrom)
        {
            return FeeResult.Fail(FeeOutcome.Invalid, "A concession must end on or after the day it starts.");
        }

        if (request.ConcessionKind == Entity.Enums.ConcessionKind.Percent && request.Value > 100)
        {
            return FeeResult.Fail(FeeOutcome.Invalid, "A percentage concession cannot be more than 100.");
        }

        if (request.IsApproved && !mayApprove)
        {
            return FeeResult.Fail(FeeOutcome.StateRule, "Approving a concession needs permission to approve fees.");
        }

        if (!await _db.FeeHeads.AnyAsync(h => h.FeeHeadId == request.FeeHeadId, ct))
        {
            return FeeResult.Fail(FeeOutcome.Invalid, "Choose a fee head of this branch.");
        }

        FeeConcession? concession = id is long existing
            ? await _db.FeeConcessions.FirstOrDefaultAsync(c => c.FeeConcessionId == existing, ct)
            : new FeeConcession();
        if (concession is null)
        {
            return FeeResult.Fail(FeeOutcome.NotFound);
        }

        if (concession.IsApproved && !mayApprove)
        {
            return FeeResult.Fail(FeeOutcome.StateRule, "An approved concession is changed by someone who may approve fees.");
        }

        concession.StudentId = request.StudentId;
        concession.FeeHeadId = request.FeeHeadId;
        concession.ConcessionKind = request.ConcessionKind;
        concession.Value = request.Value;
        concession.Reason = request.Reason.Trim();
        concession.ValidFrom = request.ValidFrom;
        concession.ValidTo = request.ValidTo;
        concession.IsApproved = request.IsApproved;
        if (id is null)
        {
            _db.FeeConcessions.Add(concession);
        }

        await _db.SaveChangesAsync(ct);
        return FeeResult.Ok(concession.FeeConcessionId);
    }
}
