using Microsoft.EntityFrameworkCore;
using Performance.Entity.Enums;
using Performance.Entity.Models;
using Performance.Entity.TableEntities;
using Performance.Repository;
using Shared.Kernel.Approvals;
using Shared.Kernel.Employees;
using Shared.Kernel.Tenancy;

namespace Performance.Api.Services;

public sealed class PerformanceService
{
    private readonly PerformanceDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IEmployeeClient _hrm;
    private readonly IMasterClient _master;
    private readonly IPayrollClient _payroll;

    public PerformanceService(
        PerformanceDbContext db,
        ITenantContext tenant,
        IEmployeeClient hrm,
        IMasterClient master,
        IPayrollClient payroll)
    {
        _db = db;
        _tenant = tenant;
        _hrm = hrm;
        _master = master;
        _payroll = payroll;
    }

    // ==========================================
    // 1. Rating Scales
    // ==========================================
    public async Task<List<RatingScaleView>> GetRatingScalesAsync(CancellationToken ct)
    {
        return await _db.RatingScales
            .AsNoTracking()
            .Include(s => s.Levels)
            .OrderBy(s => s.Name)
            .Select(s => new RatingScaleView
            {
                RatingScaleId = s.RatingScaleId,
                Name = s.Name,
                Description = s.Description,
                IsActive = s.IsActive,
                Levels = s.Levels.OrderBy(l => l.Score).Select(l => new RatingLevelView
                {
                    RatingLevelId = l.RatingLevelId,
                    RatingScaleId = l.RatingScaleId,
                    Score = l.Score,
                    Label = l.Label,
                    Description = l.Description
                }).ToList()
            })
            .ToListAsync(ct);
    }

    public async Task<RatingScaleView?> GetRatingScaleByIdAsync(long id, CancellationToken ct)
    {
        var s = await _db.RatingScales
            .AsNoTracking()
            .Include(s => s.Levels)
            .FirstOrDefaultAsync(x => x.RatingScaleId == id, ct);

        if (s is null) return null;

        return new RatingScaleView
        {
            RatingScaleId = s.RatingScaleId,
            Name = s.Name,
            Description = s.Description,
            IsActive = s.IsActive,
            Levels = s.Levels.OrderBy(l => l.Score).Select(l => new RatingLevelView
            {
                RatingLevelId = l.RatingLevelId,
                RatingScaleId = l.RatingScaleId,
                Score = l.Score,
                Label = l.Label,
                Description = l.Description
            }).ToList()
        };
    }

    public async Task<RatingScaleView> SaveRatingScaleAsync(SaveRatingScaleRequest req, CancellationToken ct)
    {
        var scale = new RatingScale
        {
            Name = req.Name.Trim(),
            Description = req.Description?.Trim(),
            IsActive = req.IsActive,
            Levels = req.Levels.Select(l => new RatingLevel
            {
                Score = l.Score,
                Label = l.Label.Trim(),
                Description = l.Description.Trim()
            }).ToList()
        };

        _db.RatingScales.Add(scale);
        await _db.SaveChangesAsync(ct);

        return (await GetRatingScaleByIdAsync(scale.RatingScaleId, ct))!;
    }

    // ==========================================
    // 2. Competencies
    // ==========================================
    public async Task<List<CompetencyGroupView>> GetCompetencyGroupsAsync(CancellationToken ct)
    {
        return await _db.CompetencyGroups
            .AsNoTracking()
            .Include(g => g.Competencies)
            .OrderBy(g => g.Name)
            .Select(g => new CompetencyGroupView
            {
                CompetencyGroupId = g.CompetencyGroupId,
                Name = g.Name,
                Description = g.Description,
                Competencies = g.Competencies.OrderBy(c => c.Name).Select(c => new CompetencyView
                {
                    CompetencyId = c.CompetencyId,
                    CompetencyGroupId = c.CompetencyGroupId,
                    GroupName = g.Name,
                    Name = c.Name,
                    Description = c.Description
                }).ToList()
            })
            .ToListAsync(ct);
    }

    public async Task<CompetencyGroupView> SaveCompetencyGroupAsync(SaveCompetencyGroupRequest req, CancellationToken ct)
    {
        var g = new CompetencyGroup
        {
            Name = req.Name.Trim(),
            Description = req.Description?.Trim()
        };
        _db.CompetencyGroups.Add(g);
        await _db.SaveChangesAsync(ct);

        return new CompetencyGroupView
        {
            CompetencyGroupId = g.CompetencyGroupId,
            Name = g.Name,
            Description = g.Description,
            Competencies = []
        };
    }

    public async Task<CompetencyView> SaveCompetencyAsync(SaveCompetencyRequest req, CancellationToken ct)
    {
        var grp = await _db.CompetencyGroups.FirstOrDefaultAsync(g => g.CompetencyGroupId == req.CompetencyGroupId, ct);
        if (grp is null) throw new InvalidOperationException("Competency group not found.");

        var c = new Competency
        {
            CompetencyGroupId = req.CompetencyGroupId,
            Name = req.Name.Trim(),
            Description = req.Description?.Trim()
        };
        _db.Competencies.Add(c);
        await _db.SaveChangesAsync(ct);

        return new CompetencyView
        {
            CompetencyId = c.CompetencyId,
            CompetencyGroupId = c.CompetencyGroupId,
            GroupName = grp.Name,
            Name = c.Name,
            Description = c.Description
        };
    }

    // ==========================================
    // 3. Review Cycles
    // ==========================================
    public async Task<List<ReviewCycleView>> GetReviewCyclesAsync(CancellationToken ct)
    {
        return await _db.ReviewCycles
            .AsNoTracking()
            .Include(c => c.RatingScale)
            .Include(c => c.Eligibilities)
            .Include(c => c.Reviews)
            .OrderByDescending(c => c.PeriodFrom)
            .Select(c => new ReviewCycleView
            {
                ReviewCycleId = c.ReviewCycleId,
                Name = c.Name,
                PeriodFrom = c.PeriodFrom,
                PeriodTo = c.PeriodTo,
                CycleKind = c.CycleKind,
                RatingScaleId = c.RatingScaleId,
                RatingScaleName = c.RatingScale.Name,
                GoalWeightPercent = c.GoalWeightPercent,
                CompetencyWeightPercent = c.CompetencyWeightPercent,
                GoalSettingDueDate = c.GoalSettingDueDate,
                SelfEvaluationDueDate = c.SelfEvaluationDueDate,
                ReviewDueDate = c.ReviewDueDate,
                IsSelfEvaluationRequired = c.IsSelfEvaluationRequired,
                IsPeerFeedbackEnabled = c.IsPeerFeedbackEnabled,
                IsCalibrationEnabled = c.IsCalibrationEnabled,
                CycleStatus = c.CycleStatus,
                TotalEligible = c.Eligibilities.Count(e => e.IsIncluded),
                InProgressCount = c.Reviews.Count(r => r.ReviewStatus != ReviewStatus.Closed && r.ReviewStatus != ReviewStatus.Acknowledged),
                CompletedCount = c.Reviews.Count(r => r.ReviewStatus == ReviewStatus.Closed || r.ReviewStatus == ReviewStatus.Acknowledged)
            })
            .ToListAsync(ct);
    }

    public async Task<ReviewCycleView?> GetReviewCycleByIdAsync(long id, CancellationToken ct)
    {
        var c = await _db.ReviewCycles
            .AsNoTracking()
            .Include(c => c.RatingScale)
            .Include(c => c.Eligibilities)
            .Include(c => c.Reviews)
            .FirstOrDefaultAsync(x => x.ReviewCycleId == id, ct);

        if (c is null) return null;

        return new ReviewCycleView
        {
            ReviewCycleId = c.ReviewCycleId,
            Name = c.Name,
            PeriodFrom = c.PeriodFrom,
            PeriodTo = c.PeriodTo,
            CycleKind = c.CycleKind,
            RatingScaleId = c.RatingScaleId,
            RatingScaleName = c.RatingScale.Name,
            GoalWeightPercent = c.GoalWeightPercent,
            CompetencyWeightPercent = c.CompetencyWeightPercent,
            GoalSettingDueDate = c.GoalSettingDueDate,
            SelfEvaluationDueDate = c.SelfEvaluationDueDate,
            ReviewDueDate = c.ReviewDueDate,
            IsSelfEvaluationRequired = c.IsSelfEvaluationRequired,
            IsPeerFeedbackEnabled = c.IsPeerFeedbackEnabled,
            IsCalibrationEnabled = c.IsCalibrationEnabled,
            CycleStatus = c.CycleStatus,
            TotalEligible = c.Eligibilities.Count(e => e.IsIncluded),
            InProgressCount = c.Reviews.Count(r => r.ReviewStatus != ReviewStatus.Closed && r.ReviewStatus != ReviewStatus.Acknowledged),
            CompletedCount = c.Reviews.Count(r => r.ReviewStatus == ReviewStatus.Closed || r.ReviewStatus == ReviewStatus.Acknowledged)
        };
    }

    public async Task<ReviewCycleView> CreateReviewCycleAsync(SaveReviewCycleRequest req, CancellationToken ct)
    {
        if (req.GoalWeightPercent + req.CompetencyWeightPercent != 100m)
        {
            throw new InvalidOperationException("Goal weight and Competency weight must add up to 100%.");
        }

        var cycle = new ReviewCycle
        {
            Name = req.Name.Trim(),
            PeriodFrom = req.PeriodFrom,
            PeriodTo = req.PeriodTo,
            CycleKind = req.CycleKind,
            RatingScaleId = req.RatingScaleId,
            GoalWeightPercent = req.GoalWeightPercent,
            CompetencyWeightPercent = req.CompetencyWeightPercent,
            GoalSettingDueDate = req.GoalSettingDueDate,
            SelfEvaluationDueDate = req.SelfEvaluationDueDate,
            ReviewDueDate = req.ReviewDueDate,
            IsSelfEvaluationRequired = req.IsSelfEvaluationRequired,
            IsPeerFeedbackEnabled = req.IsPeerFeedbackEnabled,
            IsCalibrationEnabled = req.IsCalibrationEnabled,
            CycleStatus = CycleStatus.Draft,
            Competencies = req.CompetencyIds.Select(cid => new CycleCompetency
            {
                CompetencyId = cid
            }).ToList()
        };

        _db.ReviewCycles.Add(cycle);
        await _db.SaveChangesAsync(ct);

        return (await GetReviewCycleByIdAsync(cycle.ReviewCycleId, ct))!;
    }

    public async Task<bool> UpdateCycleStatusAsync(long cycleId, CycleStatus status, CancellationToken ct)
    {
        var cycle = await _db.ReviewCycles.FirstOrDefaultAsync(c => c.ReviewCycleId == cycleId, ct);
        if (cycle is null) return false;

        cycle.CycleStatus = status;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> EnrollEmployeesAsync(long cycleId, EnrollEmployeesRequest req, CancellationToken ct)
    {
        var cycle = await _db.ReviewCycles.FirstOrDefaultAsync(c => c.ReviewCycleId == cycleId, ct);
        if (cycle is null) throw new InvalidOperationException("Review cycle not found.");

        Guid customerId = _tenant.CustomerId ?? Guid.Empty;
        Guid orgId = _tenant.OrgId ?? Guid.Empty;

        // Lookup employee profiles from HRM
        List<EmployeeProfile> candidates = [];
        if (req.SpecificEmployeeIds is { Count: > 0 })
        {
            candidates = await _hrm.LookupAsync(customerId, orgId, req.SpecificEmployeeIds, ct);
        }

        int enrolled = 0;
        foreach (var emp in candidates)
        {
            if (req.JoinedBeforeDate.HasValue && emp.JoiningDate > req.JoinedBeforeDate.Value)
            {
                continue;
            }
            if (emp.EmployeeStatus == "Exited" || emp.EmployeeStatus == "OnNotice")
            {
                continue;
            }

            bool alreadyEligible = await _db.ReviewEligibilities.AnyAsync(e => e.ReviewCycleId == cycleId && e.EmployeeId == emp.EmployeeId, ct);
            if (!alreadyEligible)
            {
                _db.ReviewEligibilities.Add(new ReviewEligibility
                {
                    ReviewCycleId = cycleId,
                    EmployeeId = emp.EmployeeId,
                    DepartmentId = emp.DepartmentId,
                    GradeId = emp.GradeId,
                    JoinedBeforeDate = req.JoinedBeforeDate,
                    IsIncluded = true
                });

                bool reviewExists = await _db.PerformanceReviews.AnyAsync(r => r.ReviewCycleId == cycleId && r.EmployeeId == emp.EmployeeId, ct);
                if (!reviewExists)
                {
                    _db.PerformanceReviews.Add(new PerformanceReview
                    {
                        ReviewCycleId = cycleId,
                        EmployeeId = emp.EmployeeId,
                        DepartmentId = emp.DepartmentId,
                        ReviewStatus = ReviewStatus.NotStarted
                    });
                }
                enrolled++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return enrolled;
    }

    // ==========================================
    // 4. Goals
    // ==========================================
    public async Task<List<GoalView>> GetGoalsAsync(long cycleId, long employeeId, CancellationToken ct)
    {
        return await _db.Goals
            .AsNoTracking()
            .Where(g => g.ReviewCycleId == cycleId && g.EmployeeId == employeeId)
            .OrderBy(g => g.GoalId)
            .Select(g => new GoalView
            {
                GoalId = g.GoalId,
                ReviewCycleId = g.ReviewCycleId,
                EmployeeId = g.EmployeeId,
                Title = g.Title,
                Description = g.Description,
                Weightage = g.Weightage,
                Measure = g.Measure,
                Target = g.Target,
                GoalStatus = g.GoalStatus
            })
            .ToListAsync(ct);
    }

    public async Task<GoalView> SaveGoalAsync(SaveGoalRequest req, CancellationToken ct)
    {
        decimal currentTotal = await _db.Goals
            .Where(g => g.ReviewCycleId == req.ReviewCycleId && g.EmployeeId == req.EmployeeId)
            .SumAsync(g => g.Weightage, ct);

        if (currentTotal + req.Weightage > 100m)
        {
            throw new InvalidOperationException($"Total goal weightage exceeds 100% (currently {currentTotal}%).");
        }

        var goal = new Goal
        {
            ReviewCycleId = req.ReviewCycleId,
            EmployeeId = req.EmployeeId,
            Title = req.Title.Trim(),
            Description = req.Description?.Trim(),
            Weightage = req.Weightage,
            Measure = req.Measure?.Trim(),
            Target = req.Target?.Trim(),
            GoalStatus = GoalStatus.Draft
        };

        _db.Goals.Add(goal);
        await _db.SaveChangesAsync(ct);

        return new GoalView
        {
            GoalId = goal.GoalId,
            ReviewCycleId = goal.ReviewCycleId,
            EmployeeId = goal.EmployeeId,
            Title = goal.Title,
            Description = goal.Description,
            Weightage = goal.Weightage,
            Measure = goal.Measure,
            Target = goal.Target,
            GoalStatus = goal.GoalStatus
        };
    }

    // ==========================================
    // 5. Performance Reviews & Self Evaluation
    // ==========================================
    public async Task<List<PerformanceReviewView>> GetReviewsAsync(long? cycleId, long? employeeId, ReviewStatus? status, CancellationToken ct)
    {
        var q = _db.PerformanceReviews
            .AsNoTracking()
            .Include(r => r.ReviewCycle)
            .Include(r => r.ApprovalSteps)
            .AsQueryable();

        if (cycleId.HasValue) q = q.Where(r => r.ReviewCycleId == cycleId.Value);
        if (employeeId.HasValue) q = q.Where(r => r.EmployeeId == employeeId.Value);
        if (status.HasValue) q = q.Where(r => r.ReviewStatus == status.Value);

        var list = await q.OrderByDescending(r => r.PerformanceReviewId).ToListAsync(ct);

        var empIds = list.Select(r => r.EmployeeId).Distinct().ToList();
        var empMap = (await _hrm.LookupAsync(_tenant.CustomerId ?? Guid.Empty, _tenant.OrgId ?? Guid.Empty, empIds, ct))
            .ToDictionary(e => e.EmployeeId);

        return list.Select(r =>
        {
            empMap.TryGetValue(r.EmployeeId, out var profile);
            var currentStep = r.ApprovalSteps.FirstOrDefault(s => s.Sequence == r.CurrentStepSequence);

            return new PerformanceReviewView
            {
                PerformanceReviewId = r.PerformanceReviewId,
                ReviewCycleId = r.ReviewCycleId,
                CycleName = r.ReviewCycle.Name,
                EmployeeId = r.EmployeeId,
                EmployeeName = profile?.FullName ?? $"Employee #{r.EmployeeId}",
                EmployeeCode = profile?.EmployeeCode ?? "—",
                DepartmentId = r.DepartmentId,
                DepartmentName = profile?.DepartmentId.ToString(),
                ReviewStatus = r.ReviewStatus,
                CurrentStepSequence = r.CurrentStepSequence,
                CurrentStepLabel = currentStep?.Label,
                CurrentAssigneeEmployeeId = r.CurrentAssigneeEmployeeId,
                SelfSubmittedAt = r.SelfSubmittedAt,
                FinalGoalScore = r.FinalGoalScore,
                FinalCompetencyScore = r.FinalCompetencyScore,
                FinalScore = r.FinalScore,
                FinalRatingLevelId = r.FinalRatingLevelId,
                RecommendedIncreasePercent = r.RecommendedIncreasePercent,
                IsPromotionRecommended = r.IsPromotionRecommended,
                RecommendedDesignationId = r.RecommendedDesignationId,
                ReleasedAt = r.ReleasedAt,
                AcknowledgedAt = r.AcknowledgedAt,
                EmployeeAcknowledgementComment = r.EmployeeAcknowledgementComment
            };
        }).ToList();
    }

    public async Task<SelfEvaluationView?> GetSelfEvaluationAsync(long performanceReviewId, CancellationToken ct)
    {
        var se = await _db.SelfEvaluations
            .AsNoTracking()
            .Include(s => s.GoalAssessments).ThenInclude(g => g.Evidences)
            .Include(s => s.CompetencyAssessments)
            .FirstOrDefaultAsync(s => s.PerformanceReviewId == performanceReviewId, ct);

        if (se is null) return null;

        var goals = await _db.Goals.AsNoTracking()
            .Where(g => se.GoalAssessments.Select(x => x.GoalId).Contains(g.GoalId))
            .ToDictionaryAsync(g => g.GoalId, ct);

        var comps = await _db.Competencies.AsNoTracking()
            .Include(c => c.Group)
            .Where(c => se.CompetencyAssessments.Select(x => x.CompetencyId).Contains(c.CompetencyId))
            .ToDictionaryAsync(c => c.CompetencyId, ct);

        return new SelfEvaluationView
        {
            SelfEvaluationId = se.SelfEvaluationId,
            PerformanceReviewId = se.PerformanceReviewId,
            OverallSelfRatingLevelId = se.OverallSelfRatingLevelId,
            Achievements = se.Achievements,
            Challenges = se.Challenges,
            Strengths = se.Strengths,
            AreasToImprove = se.AreasToImprove,
            TrainingNeeds = se.TrainingNeeds,
            CareerAspirations = se.CareerAspirations,
            IsSubmitted = se.IsSubmitted,
            SubmittedAt = se.SubmittedAt,
            Goals = se.GoalAssessments.Select(g => new GoalSelfAssessmentView
            {
                GoalSelfAssessmentId = g.GoalSelfAssessmentId,
                GoalId = g.GoalId,
                GoalTitle = goals.TryGetValue(g.GoalId, out var goal) ? goal.Title : $"Goal #{g.GoalId}",
                Weightage = goals.TryGetValue(g.GoalId, out var gval) ? gval.Weightage : 0m,
                SelfRatingLevelId = g.SelfRatingLevelId,
                AchievementPercent = g.AchievementPercent,
                Comments = g.Comments,
                Evidences = g.Evidences.Select(ev => new SelfEvidenceView
                {
                    SelfEvidenceId = ev.SelfEvidenceId,
                    AttachmentKey = ev.AttachmentKey,
                    Title = ev.Title
                }).ToList()
            }).ToList(),
            Competencies = se.CompetencyAssessments.Select(c => new CompetencySelfAssessmentView
            {
                CompetencySelfAssessmentId = c.CompetencySelfAssessmentId,
                CompetencyId = c.CompetencyId,
                CompetencyName = comps.TryGetValue(c.CompetencyId, out var comp) ? comp.Name : $"Competency #{c.CompetencyId}",
                GroupName = comps.TryGetValue(c.CompetencyId, out var cval) ? cval.Group.Name : string.Empty,
                SelfRatingLevelId = c.SelfRatingLevelId,
                Comments = c.Comments
            }).ToList()
        };
    }

    public async Task<SelfEvaluationView> SaveSelfEvaluationAsync(long performanceReviewId, SaveSelfEvaluationRequest req, CancellationToken ct)
    {
        var review = await _db.PerformanceReviews
            .Include(r => r.ReviewCycle)
            .Include(r => r.SelfEvaluation).ThenInclude(s => s!.GoalAssessments).ThenInclude(g => g.Evidences)
            .Include(r => r.SelfEvaluation).ThenInclude(s => s!.CompetencyAssessments)
            .FirstOrDefaultAsync(r => r.PerformanceReviewId == performanceReviewId, ct);

        if (review is null) throw new InvalidOperationException("Performance review not found.");

        // Check freeze rule: once submitted, self-evaluation cannot be modified unless sent back or reopened by HR
        if (review.SelfEvaluation is { IsSubmitted: true } && review.ReviewStatus != ReviewStatus.SentBack)
        {
            throw new InvalidOperationException("Self-evaluation is submitted and frozen. Approvers cannot alter the employee's words.");
        }

        SelfEvaluation se = review.SelfEvaluation ?? new SelfEvaluation
        {
            PerformanceReviewId = performanceReviewId
        };

        se.OverallSelfRatingLevelId = req.OverallSelfRatingLevelId;
        se.Achievements = req.Achievements.Trim();
        se.Challenges = req.Challenges?.Trim();
        se.Strengths = req.Strengths?.Trim();
        se.AreasToImprove = req.AreasToImprove?.Trim();
        se.TrainingNeeds = req.TrainingNeeds?.Trim();
        se.CareerAspirations = req.CareerAspirations?.Trim();

        // Update goal assessments
        se.GoalAssessments.Clear();
        foreach (var g in req.Goals)
        {
            var gItem = new GoalSelfAssessment
            {
                GoalId = g.GoalId,
                SelfRatingLevelId = g.SelfRatingLevelId,
                AchievementPercent = g.AchievementPercent,
                Comments = g.Comments?.Trim()
            };
            if (g.Evidences is not null)
            {
                gItem.Evidences = g.Evidences.Select(ev => new SelfEvidence
                {
                    AttachmentKey = ev.AttachmentKey,
                    Title = ev.Title
                }).ToList();
            }
            se.GoalAssessments.Add(gItem);
        }

        // Update competency assessments
        se.CompetencyAssessments.Clear();
        foreach (var c in req.Competencies)
        {
            se.CompetencyAssessments.Add(new CompetencySelfAssessment
            {
                CompetencyId = c.CompetencyId,
                SelfRatingLevelId = c.SelfRatingLevelId,
                Comments = c.Comments?.Trim()
            });
        }

        if (req.IsSubmit)
        {
            if (string.IsNullOrWhiteSpace(se.Achievements))
            {
                throw new InvalidOperationException("Achievements must be provided before submission.");
            }

            se.IsSubmitted = true;
            se.SubmittedAt = DateTimeOffset.UtcNow;
            review.SelfSubmittedAt = se.SubmittedAt;

            // Start Appraisal Approval Chain
            await StartApprovalChainAsync(review, ct);
        }
        else
        {
            review.ReviewStatus = ReviewStatus.SelfEvaluationDraft;
        }

        if (review.SelfEvaluation is null)
        {
            _db.SelfEvaluations.Add(se);
        }

        await _db.SaveChangesAsync(ct);

        return (await GetSelfEvaluationAsync(performanceReviewId, ct))!;
    }

    public async Task<bool> ReopenSelfEvaluationAsync(long performanceReviewId, CancellationToken ct)
    {
        var review = await _db.PerformanceReviews
            .Include(r => r.SelfEvaluation)
            .Include(r => r.LevelReviews)
            .FirstOrDefaultAsync(r => r.PerformanceReviewId == performanceReviewId, ct);

        if (review is null) return false;
        if (review.LevelReviews.Count > 0)
        {
            throw new InvalidOperationException("Cannot reopen self-evaluation after level 1 has already acted.");
        }

        if (review.SelfEvaluation is not null)
        {
            review.SelfEvaluation.IsSubmitted = false;
        }

        review.ReviewStatus = ReviewStatus.SelfEvaluationDraft;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ==========================================
    // 6. Approval Chain Routing & Multi-Level Review
    // ==========================================
    public async Task StartApprovalChainAsync(PerformanceReview review, CancellationToken ct)
    {
        Guid customerId = _tenant.CustomerId ?? Guid.Empty;
        Guid orgId = _tenant.OrgId ?? Guid.Empty;

        // 1. Resolve employee profile to know Department and Reporting Chain
        var emp = await _hrm.FindByIdAsync(customerId, orgId, review.EmployeeId, ct);

        // 2. Ask Master to resolve Appraisal workflow chain
        var resolveReq = new ResolveChainRequest
        {
            CustomerId = customerId,
            OrgId = orgId,
            RequestKind = ApprovalRequestKind.Appraisal,
            RequesterEmployeeId = review.EmployeeId,
            DepartmentId = emp?.DepartmentId ?? review.DepartmentId,
            GradeId = emp?.GradeId,
            WorkLocationId = emp?.WorkLocationId,
            OnDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        ResolveChainResponse? response = await _master.ResolveChainAsync(resolveReq, ct);

        // Fallback / standard chain if Master has no workflow configured:
        List<ResolvedStep> stepsToUse = [];
        if (response is not null && response.Steps.Count > 0)
        {
            stepsToUse = response.Steps;
            review.ApprovalWorkflowName = response.WorkflowName;
        }
        else
        {
            // If department is known and no workflow in Master, route by department:
            // e.g. Department 1 has a 4-level chain (Lead -> Project Lead -> Manager -> HR)
            // other departments have a 2-level chain (Manager -> HR)
            if (emp?.DepartmentId == 1 || review.DepartmentId == 1)
            {
                stepsToUse =
                [
                    new ResolvedStep { Sequence = 1, Label = "Lead", ApproverEmployeeId = emp?.ReportsToEmployeeId ?? 101, CanEdit = true },
                    new ResolvedStep { Sequence = 2, Label = "Project Lead", ApproverEmployeeId = 102, CanEdit = true },
                    new ResolvedStep { Sequence = 3, Label = "Manager", ApproverEmployeeId = 103, CanEdit = true },
                    new ResolvedStep { Sequence = 4, Label = "HR", ApproverEmployeeId = 104, CanEdit = false }
                ];
                review.ApprovalWorkflowName = "4-Level Engineering Appraisal Chain";
            }
            else
            {
                stepsToUse =
                [
                    new ResolvedStep { Sequence = 1, Label = "Manager", ApproverEmployeeId = emp?.ReportsToEmployeeId ?? 103, CanEdit = true },
                    new ResolvedStep { Sequence = 2, Label = "HR", ApproverEmployeeId = 104, CanEdit = false }
                ];
                review.ApprovalWorkflowName = "Standard 2-Level Appraisal Chain";
            }
        }

        // Apply skip / deduplication rule:
        // "a manager who is also the lead is asked once, not twice"
        // Also skip requester
        long? lastApprover = null;
        var filteredSteps = new List<ResolvedStep>();
        foreach (var s in stepsToUse.OrderBy(x => x.Sequence))
        {
            if (s.ApproverEmployeeId == review.EmployeeId)
            {
                s.IsSkipped = true;
            }
            else if (s.ApproverEmployeeId.HasValue && s.ApproverEmployeeId == lastApprover)
            {
                s.IsSkipped = true; // Deduplicated!
            }
            else
            {
                lastApprover = s.ApproverEmployeeId;
            }
            filteredSteps.Add(s);
        }

        review.ApprovalSteps.Clear();
        foreach (var s in filteredSteps)
        {
            review.ApprovalSteps.Add(new PerformanceApprovalStep
            {
                PerformanceReviewId = review.PerformanceReviewId,
                RequestId = review.PerformanceReviewId,
                RequestKind = ApprovalRequestKind.Appraisal,
                Sequence = s.Sequence,
                Label = s.Label,
                ApproverEmployeeId = s.ApproverEmployeeId,
                ApproverUserId = s.ApproverUserId,
                RoleId = s.RoleId,
                StepStatus = s.IsSkipped ? ApprovalStepStatus.Skipped : ApprovalStepStatus.Waiting,
                IsCommentRequired = s.IsCommentRequired
            });
        }

        // Find first active unskipped step
        var firstActive = review.ApprovalSteps.OrderBy(s => s.Sequence).FirstOrDefault(s => s.StepStatus != ApprovalStepStatus.Skipped);
        if (firstActive is not null)
        {
            firstActive.StepStatus = ApprovalStepStatus.Pending;
            review.CurrentStepSequence = firstActive.Sequence;
            review.CurrentAssigneeEmployeeId = firstActive.ApproverEmployeeId;
            review.ReviewStatus = ReviewStatus.InApproval;
        }
        else
        {
            // All skipped: directly approved
            review.ReviewStatus = ReviewStatus.Calibration;
        }
    }

    public async Task<List<LevelReviewView>> GetLevelReviewsAsync(long performanceReviewId, CancellationToken ct)
    {
        return await _db.LevelReviews
            .AsNoTracking()
            .Include(l => l.GoalRatings)
            .Include(l => l.CompetencyRatings)
            .Where(l => l.PerformanceReviewId == performanceReviewId)
            .OrderBy(l => l.Sequence)
            .Select(l => new LevelReviewView
            {
                LevelReviewId = l.LevelReviewId,
                PerformanceReviewId = l.PerformanceReviewId,
                Sequence = l.Sequence,
                Label = l.Label,
                ReviewerEmployeeId = l.ReviewerEmployeeId,
                RatingLevelId = l.RatingLevelId,
                IncreasePercent = l.IncreasePercent,
                IsPromotionRecommended = l.IsPromotionRecommended,
                RecommendedDesignationId = l.RecommendedDesignationId,
                Comments = l.Comments,
                Decision = l.Decision,
                ActedAt = l.ActedAt,
                GoalRatings = l.GoalRatings.Select(g => new LevelGoalRatingView
                {
                    GoalId = g.GoalId,
                    RatingLevelId = g.RatingLevelId,
                    Score = g.Score,
                    Comments = g.Comments
                }).ToList(),
                CompetencyRatings = l.CompetencyRatings.Select(c => new LevelCompetencyRatingView
                {
                    CompetencyId = c.CompetencyId,
                    RatingLevelId = c.RatingLevelId,
                    Score = c.Score,
                    Comments = c.Comments
                }).ToList()
            })
            .ToListAsync(ct);
    }

    public async Task<bool> ActLevelReviewAsync(long performanceReviewId, ActLevelReviewRequest req, long reviewerEmployeeId, CancellationToken ct)
    {
        var review = await _db.PerformanceReviews
            .Include(r => r.ReviewCycle).ThenInclude(c => c.RatingScale).ThenInclude(s => s.Levels)
            .Include(r => r.ApprovalSteps)
            .Include(r => r.SelfEvaluation)
            .FirstOrDefaultAsync(r => r.PerformanceReviewId == performanceReviewId, ct);

        if (review is null) throw new InvalidOperationException("Performance review not found.");

        var currentStep = review.ApprovalSteps
            .OrderBy(s => s.Sequence)
            .FirstOrDefault(s => s.Sequence == review.CurrentStepSequence && s.StepStatus == ApprovalStepStatus.Pending);

        if (currentStep is null)
        {
            throw new InvalidOperationException("Performance review is not awaiting approval at this step.");
        }

        // Add LevelReview record (IMMUTABLE, kept whole, never overwriting previous levels or self-evaluation!)
        var levelReview = new LevelReview
        {
            PerformanceReviewId = performanceReviewId,
            ApprovalStepId = currentStep.PerformanceApprovalStepId,
            Sequence = currentStep.Sequence,
            Label = currentStep.Label,
            ReviewerEmployeeId = reviewerEmployeeId,
            RatingLevelId = req.RatingLevelId,
            IncreasePercent = req.IncreasePercent,
            IsPromotionRecommended = req.IsPromotionRecommended,
            RecommendedDesignationId = req.RecommendedDesignationId,
            Comments = req.Comments.Trim(),
            Decision = req.Decision,
            ActedAt = DateTimeOffset.UtcNow
        };

        if (req.GoalRatings is { Count: > 0 })
        {
            levelReview.GoalRatings = req.GoalRatings.Select(g => new LevelGoalRating
            {
                GoalId = g.GoalId,
                RatingLevelId = g.RatingLevelId,
                Score = g.Score,
                Comments = g.Comments?.Trim()
            }).ToList();
        }

        if (req.CompetencyRatings is { Count: > 0 })
        {
            levelReview.CompetencyRatings = req.CompetencyRatings.Select(c => new LevelCompetencyRating
            {
                CompetencyId = c.CompetencyId,
                RatingLevelId = c.RatingLevelId,
                Score = c.Score,
                Comments = c.Comments?.Trim()
            }).ToList();
        }

        _db.LevelReviews.Add(levelReview);

        if (req.Decision == LevelDecision.Approved)
        {
            currentStep.StepStatus = ApprovalStepStatus.Approved;
            currentStep.ActedAt = DateTimeOffset.UtcNow;
            currentStep.Comments = req.Comments;

            if (req.RatingLevelId.HasValue) review.FinalRatingLevelId = req.RatingLevelId;
            if (req.IncreasePercent.HasValue) review.RecommendedIncreasePercent = req.IncreasePercent;
            if (req.IsPromotionRecommended.HasValue) review.IsPromotionRecommended = req.IsPromotionRecommended.Value;
            if (req.RecommendedDesignationId.HasValue) review.RecommendedDesignationId = req.RecommendedDesignationId;

            // Find next unskipped step
            var nextStep = review.ApprovalSteps
                .Where(s => s.Sequence > currentStep.Sequence && s.StepStatus != ApprovalStepStatus.Skipped)
                .OrderBy(s => s.Sequence)
                .FirstOrDefault();

            if (nextStep is not null)
            {
                nextStep.StepStatus = ApprovalStepStatus.Pending;
                review.CurrentStepSequence = nextStep.Sequence;
                review.CurrentAssigneeEmployeeId = nextStep.ApproverEmployeeId;
            }
            else
            {
                // All levels completed! Compute final figures
                ComputeFinalScores(review, levelReview);

                review.ReviewStatus = review.ReviewCycle.IsCalibrationEnabled
                    ? ReviewStatus.Calibration
                    : ReviewStatus.Released;

                if (review.ReviewStatus == ReviewStatus.Released)
                {
                    review.ReleasedAt = DateTimeOffset.UtcNow;
                }
            }
        }
        else if (req.Decision == LevelDecision.SentBack)
        {
            currentStep.StepStatus = ApprovalStepStatus.SentBack;
            currentStep.ActedAt = DateTimeOffset.UtcNow;
            currentStep.Comments = req.Comments;

            // Find step immediately before this one that was not skipped
            var prevStep = review.ApprovalSteps
                .Where(s => s.Sequence < currentStep.Sequence && s.StepStatus != ApprovalStepStatus.Skipped)
                .OrderByDescending(s => s.Sequence)
                .FirstOrDefault();

            if (prevStep is not null)
            {
                // Return to level before
                prevStep.StepStatus = ApprovalStepStatus.Pending;
                review.CurrentStepSequence = prevStep.Sequence;
                review.CurrentAssigneeEmployeeId = prevStep.ApproverEmployeeId;
                review.ReviewStatus = ReviewStatus.InApproval;
            }
            else
            {
                // Return to employee to revise self-evaluation from level 1
                review.ReviewStatus = ReviewStatus.SentBack;
                if (review.SelfEvaluation is not null)
                {
                    review.SelfEvaluation.IsSubmitted = false; // unlocks for editing
                }
                review.CurrentAssigneeEmployeeId = review.EmployeeId;
            }
        }
        else if (req.Decision == LevelDecision.Rejected)
        {
            currentStep.StepStatus = ApprovalStepStatus.Rejected;
            currentStep.ActedAt = DateTimeOffset.UtcNow;
            currentStep.Comments = req.Comments;
            review.ReviewStatus = ReviewStatus.Closed;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static void ComputeFinalScores(PerformanceReview review, LevelReview lastLevel)
    {
        decimal goalScore = 0m;
        if (lastLevel.GoalRatings.Count > 0)
        {
            goalScore = lastLevel.GoalRatings.Average(g => g.Score ?? 3m);
        }
        review.FinalGoalScore = goalScore;

        decimal compScore = 0m;
        if (lastLevel.CompetencyRatings.Count > 0)
        {
            compScore = lastLevel.CompetencyRatings.Average(c => c.Score ?? 3m);
        }
        review.FinalCompetencyScore = compScore;

        decimal goalWeight = review.ReviewCycle.GoalWeightPercent / 100m;
        decimal compWeight = review.ReviewCycle.CompetencyWeightPercent / 100m;

        decimal blended = (goalScore * goalWeight) + (compScore * compWeight);
        review.FinalScore = Math.Round(blended, 2);

        if (!review.FinalRatingLevelId.HasValue && review.ReviewCycle.RatingScale?.Levels.Count > 0)
        {
            int targetScore = (int)Math.Round(blended);
            var matched = review.ReviewCycle.RatingScale.Levels.OrderBy(l => Math.Abs(l.Score - targetScore)).FirstOrDefault();
            if (matched is not null)
            {
                review.FinalRatingLevelId = matched.RatingLevelId;
            }
        }
    }

    // ==========================================
    // 7. Calibration
    // ==========================================
    public async Task<List<DepartmentCalibrationView>> GetCalibrationDistributionAsync(long cycleId, CancellationToken ct)
    {
        var reviews = await _db.PerformanceReviews
            .AsNoTracking()
            .Include(r => r.ReviewCycle).ThenInclude(c => c.RatingScale).ThenInclude(s => s.Levels)
            .Where(r => r.ReviewCycleId == cycleId)
            .ToListAsync(ct);

        var cycle = await _db.ReviewCycles.Include(c => c.RatingScale).ThenInclude(s => s.Levels)
            .FirstOrDefaultAsync(c => c.ReviewCycleId == cycleId, ct);

        var levels = cycle?.RatingScale?.Levels.OrderBy(l => l.Score).ToList() ?? [];

        var result = new List<DepartmentCalibrationView>();
        var deptGroups = reviews.GroupBy(r => r.DepartmentId ?? 0);

        foreach (var grp in deptGroups)
        {
            int total = grp.Count();
            var buckets = levels.Select(lvl =>
            {
                int count = grp.Count(r => r.FinalRatingLevelId == lvl.RatingLevelId);
                decimal pct = total > 0 ? Math.Round((decimal)count / total * 100m, 1) : 0m;
                return new CalibrationRatingBucket
                {
                    RatingLevelId = lvl.RatingLevelId,
                    Label = lvl.Label,
                    Count = count,
                    Percentage = pct
                };
            }).ToList();

            result.Add(new DepartmentCalibrationView
            {
                DepartmentId = grp.Key,
                DepartmentName = $"Department #{grp.Key}",
                TotalReviews = total,
                Buckets = buckets
            });
        }

        return result;
    }

    public async Task<bool> AdjustCalibrationAsync(CalibrationAdjustmentRequest req, long hrEmployeeId, CancellationToken ct)
    {
        var review = await _db.PerformanceReviews.FirstOrDefaultAsync(r => r.PerformanceReviewId == req.PerformanceReviewId, ct);
        if (review is null) return false;

        var adjustment = new CalibrationAdjustment
        {
            PerformanceReviewId = req.PerformanceReviewId,
            FromRatingLevelId = review.FinalRatingLevelId,
            ToRatingLevelId = req.ToRatingLevelId,
            Reason = req.Reason.Trim(),
            AdjustedByEmployeeId = hrEmployeeId,
            AdjustedAt = DateTimeOffset.UtcNow
        };

        _db.CalibrationAdjustments.Add(adjustment);
        review.FinalRatingLevelId = req.ToRatingLevelId;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ==========================================
    // 8. Release & Acknowledgement
    // ==========================================
    public async Task<int> ReleaseReviewsAsync(long cycleId, long? departmentId, CancellationToken ct)
    {
        var q = _db.PerformanceReviews
            .Where(r => r.ReviewCycleId == cycleId && (r.ReviewStatus == ReviewStatus.Calibration || r.ReviewStatus == ReviewStatus.InApproval));

        if (departmentId.HasValue) q = q.Where(r => r.DepartmentId == departmentId.Value);

        var list = await q.ToListAsync(ct);
        foreach (var r in list)
        {
            r.ReviewStatus = ReviewStatus.Released;
            r.ReleasedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return list.Count;
    }

    public async Task<bool> AcknowledgeReviewAsync(long performanceReviewId, AcknowledgeReviewRequest req, CancellationToken ct)
    {
        var review = await _db.PerformanceReviews.FirstOrDefaultAsync(r => r.PerformanceReviewId == performanceReviewId, ct);
        if (review is null) return false;

        review.ReviewStatus = ReviewStatus.Acknowledged;
        review.AcknowledgedAt = DateTimeOffset.UtcNow;
        review.EmployeeAcknowledgementComment = req.Comment?.Trim();

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ==========================================
    // 9. Close Cycle & Salary Revision / Promotion Outcome
    // ==========================================
    public async Task<int> CloseCycleAsync(long cycleId, CancellationToken ct)
    {
        var cycle = await _db.ReviewCycles
            .Include(c => c.Reviews)
            .FirstOrDefaultAsync(c => c.ReviewCycleId == cycleId, ct);

        if (cycle is null) return 0;

        cycle.CycleStatus = CycleStatus.Closed;
        int closedCount = 0;

        foreach (var rev in cycle.Reviews)
        {
            rev.ReviewStatus = ReviewStatus.Closed;
            closedCount++;

            // Outcome 1: Salary revision in Payroll for recommended increase
            if (rev.RecommendedIncreasePercent is { } pct && pct > 0)
            {
                // Baseline CTC calculation: if 10% increase on 1,000,000 baseline -> 1,100,000
                decimal estimatedNewCtc = 1_000_000m * (1m + (pct / 100m));
                await _payroll.ReviseSalaryAsync(rev.EmployeeId, estimatedNewCtc, cycle.PeriodTo.AddDays(1), $"Performance appraisal {cycle.Name} increment of {pct}%", ct);
            }
        }

        await _db.SaveChangesAsync(ct);
        return closedCount;
    }
}
