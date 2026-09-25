using Microsoft.EntityFrameworkCore;
using Performance.Entity.TableEntities;
using Shared.Kernel.Tenancy;

namespace Performance.Repository;

public class PerformanceDbContext : TenantDbContext
{
    public PerformanceDbContext(DbContextOptions<PerformanceDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    public DbSet<RatingScale> RatingScales => Set<RatingScale>();
    public DbSet<RatingLevel> RatingLevels => Set<RatingLevel>();
    public DbSet<CompetencyGroup> CompetencyGroups => Set<CompetencyGroup>();
    public DbSet<Competency> Competencies => Set<Competency>();
    public DbSet<ReviewCycle> ReviewCycles => Set<ReviewCycle>();
    public DbSet<CycleCompetency> CycleCompetencies => Set<CycleCompetency>();
    public DbSet<ReviewEligibility> ReviewEligibilities => Set<ReviewEligibility>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<PerformanceReview> PerformanceReviews => Set<PerformanceReview>();
    public DbSet<PerformanceApprovalStep> PerformanceApprovalSteps => Set<PerformanceApprovalStep>();
    public DbSet<SelfEvaluation> SelfEvaluations => Set<SelfEvaluation>();
    public DbSet<GoalSelfAssessment> GoalSelfAssessments => Set<GoalSelfAssessment>();
    public DbSet<SelfEvidence> SelfEvidences => Set<SelfEvidence>();
    public DbSet<CompetencySelfAssessment> CompetencySelfAssessments => Set<CompetencySelfAssessment>();
    public DbSet<LevelReview> LevelReviews => Set<LevelReview>();
    public DbSet<LevelGoalRating> LevelGoalRatings => Set<LevelGoalRating>();
    public DbSet<LevelCompetencyRating> LevelCompetencyRatings => Set<LevelCompetencyRating>();
    public DbSet<PeerFeedback> PeerFeedbacks => Set<PeerFeedback>();
    public DbSet<CalibrationAdjustment> CalibrationAdjustments => Set<CalibrationAdjustment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("prf");

        modelBuilder.Entity<RatingScale>(b =>
        {
            b.HasKey(e => e.RatingScaleId);
            b.HasMany(e => e.Levels)
                .WithOne(l => l.RatingScale)
                .HasForeignKey(l => l.RatingScaleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RatingLevel>(b =>
        {
            b.HasKey(e => e.RatingLevelId);
        });

        modelBuilder.Entity<CompetencyGroup>(b =>
        {
            b.HasKey(e => e.CompetencyGroupId);
            b.HasMany(e => e.Competencies)
                .WithOne(c => c.Group)
                .HasForeignKey(c => c.CompetencyGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Competency>(b =>
        {
            b.HasKey(e => e.CompetencyId);
        });

        modelBuilder.Entity<ReviewCycle>(b =>
        {
            b.HasKey(e => e.ReviewCycleId);
            b.Property(e => e.CycleKind).HasConversion<string>().HasMaxLength(30);
            b.Property(e => e.CycleStatus).HasConversion<string>().HasMaxLength(30);
            b.Property(e => e.GoalWeightPercent).HasColumnType("decimal(18,4)");
            b.Property(e => e.CompetencyWeightPercent).HasColumnType("decimal(18,4)");

            b.HasOne(e => e.RatingScale)
                .WithMany()
                .HasForeignKey(e => e.RatingScaleId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasMany(e => e.Competencies)
                .WithOne(c => c.ReviewCycle)
                .HasForeignKey(c => c.ReviewCycleId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(e => e.Eligibilities)
                .WithOne(el => el.ReviewCycle)
                .HasForeignKey(el => el.ReviewCycleId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(e => e.Reviews)
                .WithOne(r => r.ReviewCycle)
                .HasForeignKey(r => r.ReviewCycleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CycleCompetency>(b =>
        {
            b.HasKey(e => e.CycleCompetencyId);
            b.HasOne(e => e.Competency)
                .WithMany()
                .HasForeignKey(e => e.CompetencyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReviewEligibility>(b =>
        {
            b.HasKey(e => e.ReviewEligibilityId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.ReviewCycleId, e.EmployeeId }).IsUnique();
        });

        modelBuilder.Entity<Goal>(b =>
        {
            b.HasKey(e => e.GoalId);
            b.Property(e => e.GoalStatus).HasConversion<string>().HasMaxLength(30);
            b.Property(e => e.Weightage).HasColumnType("decimal(18,4)");
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.ReviewCycleId, e.EmployeeId });
        });

        modelBuilder.Entity<PerformanceReview>(b =>
        {
            b.HasKey(e => e.PerformanceReviewId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.ReviewCycleId, e.EmployeeId }).IsUnique();
            b.Property(e => e.ReviewStatus).HasConversion<string>().HasMaxLength(30);
            b.Property(e => e.FinalGoalScore).HasColumnType("decimal(18,4)");
            b.Property(e => e.FinalCompetencyScore).HasColumnType("decimal(18,4)");
            b.Property(e => e.FinalScore).HasColumnType("decimal(18,4)");
            b.Property(e => e.RecommendedIncreasePercent).HasColumnType("decimal(18,4)");

            b.HasOne(e => e.SelfEvaluation)
                .WithOne(s => s.PerformanceReview)
                .HasForeignKey<SelfEvaluation>(s => s.PerformanceReviewId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(e => e.LevelReviews)
                .WithOne(l => l.PerformanceReview)
                .HasForeignKey(l => l.PerformanceReviewId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(e => e.ApprovalSteps)
                .WithOne(s => s.PerformanceReview)
                .HasForeignKey(s => s.PerformanceReviewId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(e => e.PeerFeedbacks)
                .WithOne(p => p.PerformanceReview)
                .HasForeignKey(p => p.PerformanceReviewId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(e => e.CalibrationAdjustments)
                .WithOne(c => c.PerformanceReview)
                .HasForeignKey(c => c.PerformanceReviewId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PerformanceApprovalStep>(b =>
        {
            b.HasKey(e => e.PerformanceApprovalStepId);
            b.Property(e => e.RequestKind).HasConversion<string>().HasMaxLength(30);
            b.Property(e => e.StepStatus).HasConversion<string>().HasMaxLength(30);
        });

        modelBuilder.Entity<SelfEvaluation>(b =>
        {
            b.HasKey(e => e.SelfEvaluationId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.PerformanceReviewId }).IsUnique();

            b.HasMany(e => e.GoalAssessments)
                .WithOne(g => g.SelfEvaluation)
                .HasForeignKey(g => g.SelfEvaluationId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(e => e.CompetencyAssessments)
                .WithOne(c => c.SelfEvaluation)
                .HasForeignKey(c => c.SelfEvaluationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GoalSelfAssessment>(b =>
        {
            b.HasKey(e => e.GoalSelfAssessmentId);
            b.Property(e => e.AchievementPercent).HasColumnType("decimal(18,4)");
            b.HasMany(e => e.Evidences)
                .WithOne(ev => ev.GoalSelfAssessment)
                .HasForeignKey(ev => ev.GoalSelfAssessmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SelfEvidence>(b =>
        {
            b.HasKey(e => e.SelfEvidenceId);
        });

        modelBuilder.Entity<CompetencySelfAssessment>(b =>
        {
            b.HasKey(e => e.CompetencySelfAssessmentId);
        });

        modelBuilder.Entity<LevelReview>(b =>
        {
            b.HasKey(e => e.LevelReviewId);
            b.Property(e => e.Decision).HasConversion<string>().HasMaxLength(30);
            b.Property(e => e.IncreasePercent).HasColumnType("decimal(18,4)");

            b.HasMany(e => e.GoalRatings)
                .WithOne(g => g.LevelReview)
                .HasForeignKey(g => g.LevelReviewId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(e => e.CompetencyRatings)
                .WithOne(c => c.LevelReview)
                .HasForeignKey(c => c.LevelReviewId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LevelGoalRating>(b =>
        {
            b.HasKey(e => e.LevelGoalRatingId);
            b.Property(e => e.Score).HasColumnType("decimal(18,4)");
        });

        modelBuilder.Entity<LevelCompetencyRating>(b =>
        {
            b.HasKey(e => e.LevelCompetencyRatingId);
            b.Property(e => e.Score).HasColumnType("decimal(18,4)");
        });

        modelBuilder.Entity<PeerFeedback>(b =>
        {
            b.HasKey(e => e.PeerFeedbackId);
        });

        modelBuilder.Entity<CalibrationAdjustment>(b =>
        {
            b.HasKey(e => e.CalibrationAdjustmentId);
        });

        base.OnModelCreating(modelBuilder);
    }
}
