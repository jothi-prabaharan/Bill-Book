using Microsoft.EntityFrameworkCore;
using Recruitment.Entity.TableEntities;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;

namespace Recruitment.Repository;

public class RecruitmentDbContext : TenantDbContext
{
    public RecruitmentDbContext(DbContextOptions<RecruitmentDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    public DbSet<JobRequisition> JobRequisitions => Set<JobRequisition>();
    public DbSet<JobOpening> JobOpenings => Set<JobOpening>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<InterviewRound> InterviewRounds => Set<InterviewRound>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<NumberingSeries> NumberingSeries => Set<NumberingSeries>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("rec");

        modelBuilder.Entity<JobRequisition>(b =>
        {
            b.HasKey(e => e.JobRequisitionId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.RequisitionCode }).IsUnique();
            b.Property(e => e.MinCtc).HasColumnType("decimal(18,4)");
            b.Property(e => e.MaxCtc).HasColumnType("decimal(18,4)");
            b.Property(e => e.EmploymentType).HasConversion<string>().HasMaxLength(30);
            b.Property(e => e.ApprovalStatus).HasConversion<string>().HasMaxLength(30);
        });

        modelBuilder.Entity<JobOpening>(b =>
        {
            b.HasKey(e => e.JobOpeningId);
            b.Property(e => e.OpeningStatus).HasConversion<string>().HasMaxLength(30);
            b.HasOne(e => e.Requisition)
                .WithMany(r => r.OpeningsList)
                .HasForeignKey(e => e.JobRequisitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Candidate>(b =>
        {
            b.HasKey(e => e.CandidateId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.Email }).IsUnique();
            b.Property(e => e.CurrentCtc).HasColumnType("decimal(18,4)");
            b.Property(e => e.ExpectedCtc).HasColumnType("decimal(18,4)");
            b.Property(e => e.CandidateSource).HasConversion<string>().HasMaxLength(30);
        });

        modelBuilder.Entity<Application>(b =>
        {
            b.HasKey(e => e.ApplicationId);
            b.HasIndex(e => new { e.CustomerId, e.OrgId, e.JobOpeningId, e.CandidateId }).IsUnique();
            b.Property(e => e.Stage).HasConversion<string>().HasMaxLength(30);
            b.HasOne(e => e.JobOpening)
                .WithMany(o => o.Applications)
                .HasForeignKey(e => e.JobOpeningId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.Candidate)
                .WithMany(c => c.Applications)
                .HasForeignKey(e => e.CandidateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InterviewRound>(b =>
        {
            b.HasKey(e => e.InterviewRoundId);
            b.Property(e => e.RoundKind).HasConversion<string>().HasMaxLength(30);
            b.Property(e => e.Outcome).HasConversion<string>().HasMaxLength(30);
            b.HasOne(e => e.Application)
                .WithMany(a => a.InterviewRounds)
                .HasForeignKey(e => e.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Offer>(b =>
        {
            b.HasKey(e => e.OfferId);
            b.Property(e => e.OfferedCtc).HasColumnType("decimal(18,4)");
            b.Property(e => e.OfferStatus).HasConversion<string>().HasMaxLength(30);
            b.Property(e => e.ApprovalStatus).HasConversion<string>().HasMaxLength(30);
            b.HasOne(e => e.Application)
                .WithMany(a => a.Offers)
                .HasForeignKey(e => e.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.ConfigureNumberingSeries(ownsMigration: false);

        base.OnModelCreating(modelBuilder);
    }
}
