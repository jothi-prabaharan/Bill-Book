using Claims.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;

namespace Claims.Repository;

public class ClaimsDbContext : TenantDbContext
{
    public ClaimsDbContext(DbContextOptions<ClaimsDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    public DbSet<ClaimCategory> ClaimCategories => Set<ClaimCategory>();
    public DbSet<ClaimLimit> ClaimLimits => Set<ClaimLimit>();
    public DbSet<ExpenseClaim> ExpenseClaims => Set<ExpenseClaim>();
    public DbSet<ExpenseClaimLine> ExpenseClaimLines => Set<ExpenseClaimLine>();
    public DbSet<ApprovalWorkflow> ApprovalWorkflows => Set<ApprovalWorkflow>();
    public DbSet<ApprovalWorkflowLevel> ApprovalWorkflowLevels => Set<ApprovalWorkflowLevel>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();
    public DbSet<NumberingSeries> NumberingSeries => Set<NumberingSeries>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("clm");

        modelBuilder.Entity<ClaimCategory>(b =>
        {
            b.HasKey(e => e.ClaimCategoryId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Code }).IsUnique();
        });

        modelBuilder.Entity<ClaimLimit>(b =>
        {
            b.HasKey(e => e.ClaimLimitId);
            b.Property(e => e.Amount).HasColumnType("decimal(18,4)");
            b.Property(e => e.LimitPeriod).HasConversion<string>().HasMaxLength(20);
            b.HasOne(e => e.Category)
                .WithMany(c => c.Limits)
                .HasForeignKey(e => e.ClaimCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExpenseClaim>(b =>
        {
            b.HasKey(e => e.ExpenseClaimId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.ClaimNo }).IsUnique();
            b.Property(e => e.TotalAmount).HasColumnType("decimal(18,4)");
            b.Property(e => e.ApprovedAmount).HasColumnType("decimal(18,4)");
            b.Property(e => e.ClaimStatus).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.PayoutMode).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.ApprovalStatus).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<ExpenseClaimLine>(b =>
        {
            b.HasKey(e => e.ExpenseClaimLineId);
            b.Property(e => e.Amount).HasColumnType("decimal(18,4)");
            b.HasOne(e => e.Claim)
                .WithMany(c => c.Lines)
                .HasForeignKey(e => e.ExpenseClaimId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.ClaimCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApprovalWorkflow>(b =>
        {
            b.HasKey(e => e.ApprovalWorkflowId);
            b.Property(e => e.RequestKind).HasConversion<string>().HasMaxLength(30);
        });

        modelBuilder.Entity<ApprovalWorkflowLevel>(b =>
        {
            b.HasKey(e => e.ApprovalWorkflowLevelId);
            b.Property(e => e.ApproverKind).HasConversion<string>().HasMaxLength(30);
            b.HasOne(e => e.Workflow)
                .WithMany(w => w.Levels)
                .HasForeignKey(e => e.ApprovalWorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApprovalStep>(b =>
        {
            b.HasKey(e => e.ApprovalStepId);
            b.Property(e => e.StepStatus).HasConversion<string>().HasMaxLength(20);
            b.HasOne(e => e.Claim)
                .WithMany(c => c.Steps)
                .HasForeignKey(e => e.ExpenseClaimId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.ConfigureNumberingSeries(ownsMigration: false);

        base.OnModelCreating(modelBuilder);
    }
}
