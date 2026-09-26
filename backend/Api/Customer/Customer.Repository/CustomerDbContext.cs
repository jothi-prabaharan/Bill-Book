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

    public DbSet<Lead> Leads => Set<Lead>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();

    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("cus");

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

        base.OnModelCreating(modelBuilder);
    }
}
