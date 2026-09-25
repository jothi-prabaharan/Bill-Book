using Claims.Entity.Enums;
using Claims.Entity.Models;
using Claims.Entity.TableEntities;
using Claims.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Employees;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;

namespace Claims.Api.Services;

public sealed class ClaimService
{
    private readonly ClaimsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IHrmClient _hrm;
    private readonly IAccountingClient _accounting;
    private readonly IPayrollClient _payroll;
    private readonly INumberGenerator _numberGenerator;

    public ClaimService(
        ClaimsDbContext db,
        ITenantContext tenant,
        IHrmClient hrm,
        IAccountingClient accounting,
        IPayrollClient payroll,
        INumberGenerator numberGenerator)
    {
        _db = db;
        _tenant = tenant;
        _hrm = hrm;
        _accounting = accounting;
        _payroll = payroll;
        _numberGenerator = numberGenerator;
    }

    // ==========================================
    // 1. Categories
    // ==========================================
    public async Task<List<ClaimCategoryDto>> GetCategoriesAsync(bool includeInactive, CancellationToken ct)
    {
        var q = _db.ClaimCategories.AsNoTracking();
        if (!includeInactive) q = q.Where(c => c.IsActive);

        return await q.OrderBy(c => c.Name)
            .Select(c => new ClaimCategoryDto(
                c.ClaimCategoryId,
                c.Code,
                c.Name,
                c.IsReceiptRequired,
                c.LedgerAccountId,
                c.IsTaxable,
                c.IsActive))
            .ToListAsync(ct);
    }

    public async Task<ClaimCategoryDto?> GetCategoryByIdAsync(long id, CancellationToken ct)
    {
        var c = await _db.ClaimCategories.AsNoTracking().FirstOrDefaultAsync(x => x.ClaimCategoryId == id, ct);
        return c is null ? null : new ClaimCategoryDto(
            c.ClaimCategoryId,
            c.Code,
            c.Name,
            c.IsReceiptRequired,
            c.LedgerAccountId,
            c.IsTaxable,
            c.IsActive);
    }

    public async Task<ClaimCategoryDto> CreateCategoryAsync(CreateClaimCategoryRequest req, CancellationToken ct)
    {
        if (await _db.ClaimCategories.AnyAsync(c => c.Code == req.Code.Trim().ToUpperInvariant(), ct))
        {
            throw new InvalidOperationException($"Claim category code '{req.Code}' already exists.");
        }

        var cat = new ClaimCategory
        {
            Code = req.Code.Trim().ToUpperInvariant(),
            Name = req.Name.Trim(),
            IsReceiptRequired = req.IsReceiptRequired,
            LedgerAccountId = req.LedgerAccountId,
            IsTaxable = req.IsTaxable,
            IsActive = req.IsActive
        };

        _db.ClaimCategories.Add(cat);
        await _db.SaveChangesAsync(ct);

        return new ClaimCategoryDto(cat.ClaimCategoryId, cat.Code, cat.Name, cat.IsReceiptRequired, cat.LedgerAccountId, cat.IsTaxable, cat.IsActive);
    }

    public async Task<ClaimCategoryDto?> UpdateCategoryAsync(long id, CreateClaimCategoryRequest req, CancellationToken ct)
    {
        var cat = await _db.ClaimCategories.FirstOrDefaultAsync(c => c.ClaimCategoryId == id, ct);
        if (cat is null) return null;

        cat.Name = req.Name.Trim();
        cat.IsReceiptRequired = req.IsReceiptRequired;
        cat.LedgerAccountId = req.LedgerAccountId;
        cat.IsTaxable = req.IsTaxable;
        cat.IsActive = req.IsActive;

        await _db.SaveChangesAsync(ct);
        return new ClaimCategoryDto(cat.ClaimCategoryId, cat.Code, cat.Name, cat.IsReceiptRequired, cat.LedgerAccountId, cat.IsTaxable, cat.IsActive);
    }

    // ==========================================
    // 2. Limits
    // ==========================================
    public async Task<List<ClaimLimitDto>> GetLimitsAsync(long? categoryId, CancellationToken ct)
    {
        var q = _db.ClaimLimits.AsNoTracking().Include(l => l.Category).AsQueryable();
        if (categoryId.HasValue) q = q.Where(l => l.ClaimCategoryId == categoryId.Value);

        return await q.OrderBy(l => l.Category.Name)
            .Select(l => new ClaimLimitDto(
                l.ClaimLimitId,
                l.ClaimCategoryId,
                l.Category.Name,
                l.GradeId,
                l.LimitPeriod,
                l.Amount))
            .ToListAsync(ct);
    }

    public async Task<ClaimLimitDto> SaveLimitAsync(SaveClaimLimitRequest req, CancellationToken ct)
    {
        var category = await _db.ClaimCategories.FirstOrDefaultAsync(c => c.ClaimCategoryId == req.ClaimCategoryId, ct)
            ?? throw new InvalidOperationException("Claim category not found.");

        var existing = await _db.ClaimLimits.FirstOrDefaultAsync(
            l => l.ClaimCategoryId == req.ClaimCategoryId && l.GradeId == req.GradeId && l.LimitPeriod == req.LimitPeriod, ct);

        if (existing is not null)
        {
            existing.Amount = req.Amount;
            await _db.SaveChangesAsync(ct);
            return new ClaimLimitDto(existing.ClaimLimitId, existing.ClaimCategoryId, category.Name, existing.GradeId, existing.LimitPeriod, existing.Amount);
        }

        var limit = new ClaimLimit
        {
            ClaimCategoryId = req.ClaimCategoryId,
            GradeId = req.GradeId,
            LimitPeriod = req.LimitPeriod,
            Amount = req.Amount
        };

        _db.ClaimLimits.Add(limit);
        await _db.SaveChangesAsync(ct);

        return new ClaimLimitDto(limit.ClaimLimitId, limit.ClaimCategoryId, category.Name, limit.GradeId, limit.LimitPeriod, limit.Amount);
    }

    public async Task<bool> DeleteLimitAsync(long id, CancellationToken ct)
    {
        var limit = await _db.ClaimLimits.FirstOrDefaultAsync(l => l.ClaimLimitId == id, ct);
        if (limit is null) return false;
        _db.ClaimLimits.Remove(limit);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ==========================================
    // 3. Claims
    // ==========================================
    public async Task<List<ExpenseClaimDto>> ListClaimsAsync(
        long? employeeId,
        ClaimStatus? status,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken ct)
    {
        var q = _db.ExpenseClaims
            .AsNoTracking()
            .Include(c => c.Lines)
                .ThenInclude(l => l.Category)
            .AsQueryable();

        if (employeeId.HasValue) q = q.Where(c => c.EmployeeId == employeeId.Value);
        if (status.HasValue) q = q.Where(c => c.ClaimStatus == status.Value);
        if (fromDate.HasValue) q = q.Where(c => c.ClaimDate >= fromDate.Value);
        if (toDate.HasValue) q = q.Where(c => c.ClaimDate <= toDate.Value);

        var claims = await q.OrderByDescending(c => c.ClaimDate).ThenByDescending(c => c.ExpenseClaimId).ToListAsync(ct);

        // Fetch employee details in bulk
        var empIds = claims.Select(c => c.EmployeeId).Distinct().ToList();
        var empMap = new Dictionary<long, EmployeeProfile>();
        if (empIds.Count > 0 && _tenant.CustomerId is Guid customerId && _tenant.OrgId is Guid orgId)
        {
            var profiles = await _hrm.LookupAsync(customerId, orgId, empIds, ct);
            foreach (var p in profiles) empMap[p.EmployeeId] = p;
        }

        return claims.Select(c => ToDto(c, empMap.GetValueOrDefault(c.EmployeeId))).ToList();
    }

    public async Task<ExpenseClaimDto?> GetClaimByIdAsync(long id, CancellationToken ct)
    {
        var claim = await _db.ExpenseClaims
            .AsNoTracking()
            .Include(c => c.Lines)
                .ThenInclude(l => l.Category)
            .FirstOrDefaultAsync(c => c.ExpenseClaimId == id, ct);

        if (claim is null) return null;

        EmployeeProfile? emp = null;
        if (_tenant.CustomerId is Guid customerId && _tenant.OrgId is Guid orgId)
        {
            emp = await _hrm.FindByIdAsync(customerId, orgId, claim.EmployeeId, ct);
        }

        return ToDto(claim, emp);
    }

    public async Task<ExpenseClaimDto> CreateClaimAsync(CreateExpenseClaimRequest req, CancellationToken ct)
    {
        if (req.Lines.Count == 0)
        {
            throw new InvalidOperationException("At least one expense line is required.");
        }

        string claimNo;
        try
        {
            var alloc = await _numberGenerator.NextAsync("CLM", req.ClaimDate, ct);
            claimNo = alloc.Code;
        }
        catch
        {
            claimNo = $"CLM-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        decimal total = req.Lines.Sum(l => l.Amount);

        var claim = new ExpenseClaim
        {
            ClaimNo = claimNo,
            EmployeeId = req.EmployeeId,
            ClaimDate = req.ClaimDate,
            TotalAmount = total,
            ApprovedAmount = 0,
            ClaimStatus = ClaimStatus.Draft,
            PayoutMode = req.PayoutMode,
            ApprovalStatus = ApprovalStatus.Draft,
            Lines = req.Lines.Select(l => new ExpenseClaimLine
            {
                ClaimCategoryId = l.ClaimCategoryId,
                ExpenseDate = l.ExpenseDate,
                Description = l.Description.Trim(),
                Amount = l.Amount,
                ReceiptAttachmentKey = l.ReceiptAttachmentKey
            }).ToList()
        };

        _db.ExpenseClaims.Add(claim);
        await _db.SaveChangesAsync(ct);

        return (await GetClaimByIdAsync(claim.ExpenseClaimId, ct))!;
    }

    public async Task<ExpenseClaimDto?> UpdateClaimAsync(long id, UpdateExpenseClaimRequest req, CancellationToken ct)
    {
        var claim = await _db.ExpenseClaims
            .Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.ExpenseClaimId == id, ct);

        if (claim is null) return null;
        if (claim.ClaimStatus != ClaimStatus.Draft)
        {
            throw new InvalidOperationException("Only draft claims can be edited.");
        }

        if (req.Lines.Count == 0)
        {
            throw new InvalidOperationException("At least one expense line is required.");
        }

        claim.ClaimDate = req.ClaimDate;
        claim.PayoutMode = req.PayoutMode;

        // Replace lines
        _db.ExpenseClaimLines.RemoveRange(claim.Lines);
        claim.Lines = req.Lines.Select(l => new ExpenseClaimLine
        {
            ExpenseClaimId = claim.ExpenseClaimId,
            ClaimCategoryId = l.ClaimCategoryId,
            ExpenseDate = l.ExpenseDate,
            Description = l.Description.Trim(),
            Amount = l.Amount,
            ReceiptAttachmentKey = l.ReceiptAttachmentKey
        }).ToList();

        claim.TotalAmount = claim.Lines.Sum(l => l.Amount);

        await _db.SaveChangesAsync(ct);
        return await GetClaimByIdAsync(claim.ExpenseClaimId, ct);
    }

    public async Task<bool> DeleteClaimAsync(long id, CancellationToken ct)
    {
        var claim = await _db.ExpenseClaims.FirstOrDefaultAsync(c => c.ExpenseClaimId == id, ct);
        if (claim is null) return false;
        if (claim.ClaimStatus != ClaimStatus.Draft)
        {
            throw new InvalidOperationException("Only draft claims can be deleted.");
        }

        _db.ExpenseClaims.Remove(claim);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ==========================================
    // 4. Submit & Approvals
    // ==========================================
    public async Task<ExpenseClaimDto> SubmitClaimAsync(long id, CancellationToken ct)
    {
        var claim = await _db.ExpenseClaims
            .Include(c => c.Lines)
                .ThenInclude(l => l.Category)
            .FirstOrDefaultAsync(c => c.ExpenseClaimId == id, ct)
            ?? throw new InvalidOperationException("Expense claim not found.");

        if (claim.ClaimStatus != ClaimStatus.Draft)
        {
            throw new InvalidOperationException("Only draft claims can be submitted.");
        }

        if (claim.Lines.Count == 0)
        {
            throw new InvalidOperationException("Cannot submit an empty claim.");
        }

        // 1. Validate receipts
        foreach (var line in claim.Lines)
        {
            if (line.Category.IsReceiptRequired && string.IsNullOrWhiteSpace(line.ReceiptAttachmentKey))
            {
                throw new InvalidOperationException($"Receipt is required for category '{line.Category.Name}' on date {line.ExpenseDate:yyyy-MM-dd}.");
            }
        }

        // 2. Fetch employee profile for grade & manager
        EmployeeProfile? emp = null;
        if (_tenant.CustomerId is Guid customerId && _tenant.OrgId is Guid orgId)
        {
            emp = await _hrm.FindByIdAsync(customerId, orgId, claim.EmployeeId, ct);
        }

        long? gradeId = emp?.GradeId;

        // 3. Validate limits strictly (refuse with error, do not warn)
        var categoryGrouped = claim.Lines.GroupBy(l => l.ClaimCategoryId).ToList();
        var allLimits = await _db.ClaimLimits.AsNoTracking().ToListAsync(ct);

        foreach (var group in categoryGrouped)
        {
            long catId = group.Key;
            decimal groupSum = group.Sum(l => l.Amount);
            var cat = group.First().Category;

            // Match limit: specific grade takes precedence over universal (null)
            var catLimits = allLimits.Where(l => l.ClaimCategoryId == catId && (l.GradeId == null || l.GradeId == gradeId)).ToList();

            // PerClaim
            var perClaim = catLimits.Where(l => l.LimitPeriod == LimitPeriod.PerClaim)
                .OrderByDescending(l => l.GradeId.HasValue).FirstOrDefault();
            if (perClaim is not null && groupSum > perClaim.Amount)
            {
                throw new InvalidOperationException($"Total claim amount of {groupSum:C} for '{cat.Name}' exceeds the Per-Claim limit of {perClaim.Amount:C}.");
            }

            // Monthly
            var monthly = catLimits.Where(l => l.LimitPeriod == LimitPeriod.Monthly)
                .OrderByDescending(l => l.GradeId.HasValue).FirstOrDefault();
            if (monthly is not null)
            {
                var monthStart = new DateOnly(claim.ClaimDate.Year, claim.ClaimDate.Month, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                decimal pastMonth = await _db.ExpenseClaimLines
                    .AsNoTracking()
                    .Where(l => l.ClaimCategoryId == catId &&
                                l.Claim.EmployeeId == claim.EmployeeId &&
                                l.Claim.ExpenseClaimId != claim.ExpenseClaimId &&
                                (l.Claim.ClaimStatus == ClaimStatus.Submitted || l.Claim.ClaimStatus == ClaimStatus.Approved || l.Claim.ClaimStatus == ClaimStatus.Paid) &&
                                l.ExpenseDate >= monthStart && l.ExpenseDate <= monthEnd)
                    .SumAsync(l => l.Amount, ct);

                if (pastMonth + groupSum > monthly.Amount)
                {
                    throw new InvalidOperationException($"Total monthly expenses of {(pastMonth + groupSum):C} for '{cat.Name}' exceeds the Monthly limit of {monthly.Amount:C}.");
                }
            }

            // Yearly
            var yearly = catLimits.Where(l => l.LimitPeriod == LimitPeriod.Yearly)
                .OrderByDescending(l => l.GradeId.HasValue).FirstOrDefault();
            if (yearly is not null)
            {
                var yearStart = new DateOnly(claim.ClaimDate.Year, 1, 1);
                var yearEnd = new DateOnly(claim.ClaimDate.Year, 12, 31);

                decimal pastYear = await _db.ExpenseClaimLines
                    .AsNoTracking()
                    .Where(l => l.ClaimCategoryId == catId &&
                                l.Claim.EmployeeId == claim.EmployeeId &&
                                l.Claim.ExpenseClaimId != claim.ExpenseClaimId &&
                                (l.Claim.ClaimStatus == ClaimStatus.Submitted || l.Claim.ClaimStatus == ClaimStatus.Approved || l.Claim.ClaimStatus == ClaimStatus.Paid) &&
                                l.ExpenseDate >= yearStart && l.ExpenseDate <= yearEnd)
                    .SumAsync(l => l.Amount, ct);

                if (pastYear + groupSum > yearly.Amount)
                {
                    throw new InvalidOperationException($"Total yearly expenses of {(pastYear + groupSum):C} for '{cat.Name}' exceeds the Yearly limit of {yearly.Amount:C}.");
                }
            }
        }

        // 4. Generate Approval Steps from active workflow
        var workflow = await _db.ApprovalWorkflows
            .Include(w => w.Levels.OrderBy(l => l.Sequence))
            .FirstOrDefaultAsync(w => w.IsActive && w.RequestKind == ClaimRequestKind.Claim, ct);

        claim.Steps.Clear();

        if (workflow is not null && workflow.Levels.Count > 0)
        {
            foreach (var lvl in workflow.Levels.OrderBy(l => l.Sequence))
            {
                long? approverId = null;
                if (lvl.ApproverKind == ApproverKind.ReportingChain)
                {
                    approverId = emp?.ReportsToEmployeeId;
                }
                else if (lvl.ApproverKind == ApproverKind.SpecificEmployee)
                {
                    approverId = lvl.SpecificEmployeeId;
                }

                claim.Steps.Add(new ApprovalStep
                {
                    Sequence = lvl.Sequence,
                    Label = lvl.Label,
                    StepStatus = ApprovalStepStatus.Pending,
                    ApproverEmployeeId = approverId
                });
            }

            var firstStep = claim.Steps.OrderBy(s => s.Sequence).First();
            claim.CurrentStepLabel = firstStep.Label;
            claim.CurrentApproverEmployeeId = firstStep.ApproverEmployeeId;
        }

        claim.ClaimStatus = ClaimStatus.Submitted;
        claim.ApprovalStatus = ApprovalStatus.InApproval;

        await _db.SaveChangesAsync(ct);
        return (await GetClaimByIdAsync(claim.ExpenseClaimId, ct))!;
    }

    public async Task<ExpenseClaimDto> ActApprovalAsync(long claimId, ActClaimApprovalRequest req, Guid actedByUserId, CancellationToken ct)
    {
        var claim = await _db.ExpenseClaims
            .Include(c => c.Steps)
            .FirstOrDefaultAsync(c => c.ExpenseClaimId == claimId, ct)
            ?? throw new InvalidOperationException("Claim not found.");

        if (claim.ClaimStatus != ClaimStatus.Submitted)
        {
            throw new InvalidOperationException("Claim is not in a submitted state awaiting approval.");
        }

        var pendingStep = claim.Steps.OrderBy(s => s.Sequence).FirstOrDefault(s => s.StepStatus == ApprovalStepStatus.Pending);
        if (pendingStep is null)
        {
            throw new InvalidOperationException("No pending approval steps found on this claim.");
        }

        string action = req.Action.Trim().ToLowerInvariant();

        if (action == "approve")
        {
            pendingStep.StepStatus = ApprovalStepStatus.Approved;
            pendingStep.ActedByUserId = actedByUserId;
            pendingStep.ActedAt = DateTimeOffset.UtcNow;
            pendingStep.Comments = req.Comments;

            var nextStep = claim.Steps.OrderBy(s => s.Sequence).FirstOrDefault(s => s.Sequence > pendingStep.Sequence && s.StepStatus == ApprovalStepStatus.Pending);
            if (nextStep is not null)
            {
                claim.CurrentStepLabel = nextStep.Label;
                claim.CurrentApproverEmployeeId = nextStep.ApproverEmployeeId;
            }
            else
            {
                // All steps completed!
                claim.ClaimStatus = ClaimStatus.Approved;
                claim.ApprovalStatus = ApprovalStatus.Approved;
                claim.ApprovedAmount = claim.TotalAmount;
                claim.CurrentStepLabel = "Approved";
                claim.CurrentApproverEmployeeId = null;
            }
        }
        else if (action == "reject")
        {
            pendingStep.StepStatus = ApprovalStepStatus.Rejected;
            pendingStep.ActedByUserId = actedByUserId;
            pendingStep.ActedAt = DateTimeOffset.UtcNow;
            pendingStep.Comments = req.Comments;

            claim.ClaimStatus = ClaimStatus.Rejected;
            claim.ApprovalStatus = ApprovalStatus.Rejected;
            claim.CurrentStepLabel = "Rejected";
            claim.CurrentApproverEmployeeId = null;
        }
        else if (action == "sendback")
        {
            pendingStep.StepStatus = ApprovalStepStatus.SentBack;
            pendingStep.ActedByUserId = actedByUserId;
            pendingStep.ActedAt = DateTimeOffset.UtcNow;
            pendingStep.Comments = req.Comments;

            // Return to draft so employee can amend and re-submit
            claim.ClaimStatus = ClaimStatus.Draft;
            claim.ApprovalStatus = ApprovalStatus.Draft;
            claim.CurrentStepLabel = "Returned for Revision";
            claim.CurrentApproverEmployeeId = null;
        }
        else
        {
            throw new InvalidOperationException($"Unsupported action '{req.Action}'. Use Approve, Reject, or SendBack.");
        }

        await _db.SaveChangesAsync(ct);
        return (await GetClaimByIdAsync(claim.ExpenseClaimId, ct))!;
    }

    // ==========================================
    // 5. Payout
    // ==========================================
    public async Task<ExpenseClaimDto> PayoutClaimAsync(long id, PayoutClaimRequest req, CancellationToken ct)
    {
        var claim = await _db.ExpenseClaims
            .Include(c => c.Lines)
                .ThenInclude(l => l.Category)
            .FirstOrDefaultAsync(c => c.ExpenseClaimId == id, ct)
            ?? throw new InvalidOperationException("Claim not found.");

        if (claim.ClaimStatus != ClaimStatus.Approved)
        {
            throw new InvalidOperationException("Only approved claims can be paid.");
        }

        PayoutMode mode = req.PayoutMode ?? claim.PayoutMode;
        claim.PayoutMode = mode;

        if (mode == PayoutMode.Direct)
        {
            // Spend money posting to general ledger via Accounting
            if (_tenant.CustomerId is Guid customerId && _tenant.OrgId is Guid orgId)
            {
                var ledgerReq = new PostLedgerRequest
                {
                    CustomerId = customerId,
                    OrgId = orgId,
                    TransactionTypeCode = "CLM",
                    TransactionId = claim.ExpenseClaimId,
                    LedgerDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    DocumentNo = claim.ClaimNo,
                    Legs = new List<LedgerLegRequest>()
                };

                // Expense Debit legs
                foreach (var line in claim.Lines)
                {
                    ledgerReq.Legs.Add(new LedgerLegRequest
                    {
                        LedgerTypeId = 1,
                        TransactionDetailId = line.ExpenseClaimLineId,
                        AccountId = line.Category.LedgerAccountId,
                        AccountSystemName = line.Category.LedgerAccountId == null ? "TravelExpense" : null,
                        Debit = line.Amount,
                        Credit = 0,
                        Narration = $"{claim.ClaimNo}: {line.Description}"
                    });
                }

                // Bank Credit leg
                ledgerReq.Legs.Add(new LedgerLegRequest
                {
                    LedgerTypeId = 1,
                    TransactionDetailId = 0,
                    AccountId = req.BankAccountId,
                    AccountSystemName = req.BankAccountId == null ? "Bank" : null,
                    Debit = 0,
                    Credit = claim.ApprovedAmount > 0 ? claim.ApprovedAmount : claim.TotalAmount,
                    Narration = $"Payout for claim {claim.ClaimNo}"
                });

                var outcome = await _accounting.PostLedgerAsync(ledgerReq, ct);
                if (!outcome.Posted)
                {
                    throw new InvalidOperationException($"Direct payout journal posting failed: {outcome.Detail}");
                }
            }

            claim.ClaimStatus = ClaimStatus.Paid;
        }
        else if (mode == PayoutMode.Payroll)
        {
            if (req.PayrollRunId.HasValue)
            {
                await _payroll.AttachToPayrollRunAsync(req.PayrollRunId.Value, claim.ExpenseClaimId, claim.ApprovedAmount, ct);
                claim.PayrollRunId = req.PayrollRunId.Value;
            }

            claim.ClaimStatus = ClaimStatus.Paid;
        }

        await _db.SaveChangesAsync(ct);
        return (await GetClaimByIdAsync(claim.ExpenseClaimId, ct))!;
    }

    private static ExpenseClaimDto ToDto(ExpenseClaim c, EmployeeProfile? emp)
    {
        return new ExpenseClaimDto(
            c.ExpenseClaimId,
            c.ClaimNo,
            c.EmployeeId,
            emp?.EmployeeCode,
            emp?.FullName,
            c.ClaimDate,
            c.TotalAmount,
            c.ApprovedAmount,
            c.ClaimStatus,
            c.PayoutMode,
            c.PayrollRunId,
            c.SpendMoneyId,
            c.ApprovalStatus,
            c.CurrentStepLabel,
            c.CurrentApproverEmployeeId,
            c.Lines.Select(l => new ExpenseClaimLineDto(
                l.ExpenseClaimLineId,
                l.ExpenseClaimId,
                l.ClaimCategoryId,
                l.Category?.Code ?? "",
                l.Category?.Name ?? "",
                l.ExpenseDate,
                l.Description,
                l.Amount,
                l.ReceiptAttachmentKey)).ToList()
        );
    }
}
