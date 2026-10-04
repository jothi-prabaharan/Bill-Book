using Customer.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;

namespace Customer.Repository;

public class CustomerDbContext : TenantDbContext
{
    public CustomerDbContext(DbContextOptions<CustomerDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<ServicePlan> ServicePlans => Set<ServicePlan>();

    public DbSet<Lead> Leads => Set<Lead>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();

    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();

    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();

    public DbSet<Audience> Audiences => Set<Audience>();

    public DbSet<EmailConsent> EmailConsents => Set<EmailConsent>();

    public DbSet<Campaign> Campaigns => Set<Campaign>();

    public DbSet<CampaignRecipient> CampaignRecipients => Set<CampaignRecipient>();

    public DbSet<CampaignLink> CampaignLinks => Set<CampaignLink>();

    public DbSet<Automation> Automations => Set<Automation>();

    public DbSet<AutomationStep> AutomationSteps => Set<AutomationStep>();

    public DbSet<AutomationEnrolment> AutomationEnrolments => Set<AutomationEnrolment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("cus");

        modelBuilder.Entity<ServicePlan>(b =>
        {
            b.HasKey(e => e.ServicePlanId);

            // The reminder run and the "Services due" list both read by status and date.
            b.HasIndex(e => new { e.OrgId, e.Status, e.NextDueDate });
            b.HasIndex(e => new { e.OrgId, e.ContactId });
        });

        modelBuilder.Entity<Lead>(b =>
        {
            b.HasKey(e => e.LeadId);
            
            b.Property(e => e.Source).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

            b.HasIndex(e => new { e.OrgId, e.Status });
        });

        modelBuilder.Entity<Ticket>(b =>
        {
            b.HasKey(e => e.TicketId);

            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Priority).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Channel).HasConversion<string>().HasMaxLength(20);

            b.HasIndex(e => new { e.OrgId, e.ContactId });
            b.HasIndex(e => new { e.OrgId, e.Status });
        });

        modelBuilder.Entity<TicketMessage>(b =>
        {
            b.HasKey(e => e.TicketMessageId);

            b.Property(e => e.AuthorType).HasConversion<string>().HasMaxLength(20);

            b.HasIndex(e => new { e.OrgId, e.TicketId });

            // Only staff write internal notes; a contact's message is never one.
            b.ToTable(t => t.HasCheckConstraint(
                "chk_ticketmessages_internal_by_staff",
                "NOT \"IsInternal\" OR \"AuthorType\" = 'User'"));

            b.HasOne<Ticket>()
                .WithMany(t => t.Messages)
                .HasForeignKey(e => e.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SlaPolicy>(b =>
        {
            b.HasKey(e => e.SlaPolicyId);

            b.Property(e => e.Priority).HasConversion<string>().HasMaxLength(20);

            // One policy per priority per branch.
            b.HasIndex(e => new { e.OrgId, e.Priority }).IsUnique();
        });

        modelBuilder.Entity<EmailTemplate>(b =>
        {
            b.HasKey(e => e.EmailTemplateId);
            b.HasIndex(e => new { e.OrgId, e.IsActive });
        });

        modelBuilder.Entity<Audience>(b =>
        {
            b.HasKey(e => e.AudienceId);
            b.Property(e => e.Rules).HasColumnType("jsonb");
            b.HasIndex(e => new { e.OrgId, e.IsActive });
        });

        modelBuilder.Entity<EmailConsent>(b =>
        {
            b.HasKey(e => e.EmailConsentId);
            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Source).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(e => new { e.OrgId, e.Email }).IsUnique();
        });

        modelBuilder.Entity<Campaign>(b =>
        {
            b.HasKey(e => e.CampaignId);
            b.Property(e => e.Channel).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(e => new { e.OrgId, e.Status });
            b.HasIndex(e => new { e.OrgId, e.ScheduledAt });
        });

        modelBuilder.Entity<CampaignRecipient>(b =>
        {
            b.HasKey(e => e.CampaignRecipientId);
            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(e => new { e.OrgId, e.CampaignId, e.Email }).IsUnique();
            b.HasIndex(e => new { e.OrgId, e.CampaignId, e.Status });
        });

        modelBuilder.Entity<CampaignLink>(b =>
        {
            b.HasKey(e => e.CampaignLinkId);
            b.HasIndex(e => new { e.OrgId, e.TrackingToken }).IsUnique();
            b.HasIndex(e => new { e.OrgId, e.CampaignId });
        });

        modelBuilder.Entity<Automation>(b =>
        {
            b.HasKey(e => e.AutomationId);
            b.Property(e => e.TriggerType).HasConversion<string>().HasMaxLength(30);
            b.HasIndex(e => new { e.OrgId, e.IsActive, e.TriggerType });
            b.HasMany(e => e.Steps).WithOne().HasForeignKey(s => s.AutomationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AutomationStep>(b =>
        {
            b.HasKey(e => e.AutomationStepId);
            b.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(e => new { e.OrgId, e.AutomationId, e.Sequence });
        });

        modelBuilder.Entity<AutomationEnrolment>(b =>
        {
            b.HasKey(e => e.AutomationEnrolmentId);
            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(e => new { e.OrgId, e.AutomationId, e.Email, e.Status });
            b.HasIndex(e => new { e.OrgId, e.Status, e.NextRunAt });
        });

        base.OnModelCreating(modelBuilder);
    }
}


