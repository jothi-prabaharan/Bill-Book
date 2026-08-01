using Contacts.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;

namespace Contacts.Repository;

/// <summary>
/// The con schema, in a per-customer database. The base class supplies the
/// OrgId query filter, the insert-time OrgId stamp and xmin concurrency, so
/// nothing here needs to remember them.
/// </summary>
public class ContactsDbContext : TenantDbContext
{
    public ContactsDbContext(DbContextOptions<ContactsDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<Contact> Contacts => Set<Contact>();

    public DbSet<ContactAddress> ContactAddresses => Set<ContactAddress>();

    public DbSet<ContactPerson> ContactPersons => Set<ContactPerson>();

    public DbSet<ContactPersonRole> ContactPersonRoles => Set<ContactPersonRole>();

    public DbSet<ContactBankDetail> ContactBankDetails => Set<ContactBankDetail>();

    public DbSet<ContactLicence> ContactLicences => Set<ContactLicence>();

    public DbSet<ContactAttachment> ContactAttachments => Set<ContactAttachment>();

    /// <summary>
    /// Mapped, not migrated. Accounting owns this table; Contacts maps the same
    /// Shared.Kernel entity so a contact code can be allocated inside the same
    /// transaction as the contact insert. An HTTP hop could not join it, and a
    /// failed insert would leave the number consumed.
    /// </summary>
    public DbSet<NumberingSeries> NumberingSeries => Set<NumberingSeries>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("con");

        modelBuilder.ConfigureNumberingSeries(ownsMigration: false);

        modelBuilder.Entity<Contact>(b =>
        {
            b.HasKey(e => e.ContactId);
            b.HasIndex(e => new { e.OrgId, e.ContactCode }).IsUnique();
            b.HasIndex(e => new { e.OrgId, e.DisplayName });

            // One GSTIN, one row — whether they buy, sell, or both.
            // Finding contacts whose sub-ledger call failed, without scanning a
            // table where nearly every row succeeded.
            b.HasIndex(e => new { e.OrgId, e.ContactId })
                .HasFilter("\"SubLedgerProvisionedAt\" IS NULL")
                .HasDatabaseName("IX_Contacts_SubLedgerPending");

            b.HasIndex(e => new { e.OrgId, e.Gstin })
                .IsUnique()
                .HasFilter("\"Gstin\" IS NOT NULL")
                .HasDatabaseName("IX_Contacts_Gstin");

            // The four role pickers.
            b.HasIndex(e => e.OrgId).HasFilter("\"IsCustomer\" = true").HasDatabaseName("IX_Contacts_Customer");
            b.HasIndex(e => e.OrgId).HasFilter("\"IsVendor\" = true").HasDatabaseName("IX_Contacts_Vendor");
            b.HasIndex(e => e.OrgId).HasFilter("\"IsJobWorker\" = true").HasDatabaseName("IX_Contacts_JobWorker");
            b.HasIndex(e => e.OrgId).HasFilter("\"IsPrescriber\" = true").HasDatabaseName("IX_Contacts_Prescriber");

            b.Property(e => e.ContactCategory).HasConversion<string>().HasMaxLength(15);
            b.Property(e => e.GstRegistrationType).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.CreditLimit).HasColumnType("decimal(18,2)");
            b.Property(e => e.MaxDiscountPercent).HasColumnType("decimal(5,2)");

            b.ToTable(table =>
            {
                // A contact with no role is unreachable from every screen.
                table.HasCheckConstraint(
                    "chk_contact_role",
                    "\"IsCustomer\" = true OR \"IsVendor\" = true "
                        + "OR \"IsJobWorker\" = true OR \"IsPrescriber\" = true");

                table.HasCheckConstraint(
                    "chk_contact_tds",
                    "\"IsTdsApplicable\" = false OR \"TdsSection\" IS NOT NULL");

                table.HasCheckConstraint(
                    "chk_contact_msme",
                    "\"IsMsme\" = false OR \"UdyamNumber\" IS NOT NULL");

                table.HasCheckConstraint(
                    "chk_contact_limits",
                    "(\"CreditLimit\" IS NULL OR \"CreditLimit\" >= 0) "
                        + "AND (\"MaxOutstandingDays\" IS NULL OR \"MaxOutstandingDays\" >= 0) "
                        + "AND (\"MaxDiscountPercent\" IS NULL "
                        + "OR (\"MaxDiscountPercent\" >= 0 AND \"MaxDiscountPercent\" <= 100))");

                // Registration type decides whether a GSTIN may exist at all.
                // Getting this wrong misfiles the contact in GSTR-1.
                table.HasCheckConstraint(
                    "chk_contact_gstin_registration",
                    "(\"GstRegistrationType\" IN ('Regular', 'Composition', 'Sez') "
                        + "AND \"Gstin\" IS NOT NULL) "
                        + "OR (\"GstRegistrationType\" IN ('Unregistered', 'Overseas', 'Consumer') "
                        + "AND \"Gstin\" IS NULL)");
            });
        });

        modelBuilder.Entity<ContactAddress>(b =>
        {
            b.HasKey(e => e.ContactAddressId);
            b.HasIndex(e => new { e.OrgId, e.ContactId, e.AddressType });

            // One default billing and one default shipping per contact.
            b.HasIndex(e => new { e.OrgId, e.ContactId, e.AddressType })
                .IsUnique()
                .HasFilter("\"IsDefault\" = true")
                .HasDatabaseName("IX_ContactAddresses_Default");

            b.Property(e => e.AddressType).HasConversion<string>().HasMaxLength(10);

            b.HasOne<Contact>()
                .WithMany()
                .HasForeignKey(e => e.ContactId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContactPerson>(b =>
        {
            b.HasKey(e => e.ContactPersonId);
            b.HasIndex(e => new { e.OrgId, e.ContactId });

            // At most one default per contact. At least one is a C# rule — it
            // spans rows, so no constraint can express it.
            b.HasIndex(e => new { e.OrgId, e.ContactId })
                .IsUnique()
                .HasFilter("\"IsDefault\" = true")
                .HasDatabaseName("IX_ContactPersons_Default");

            // Counter staff search by phone number constantly in both verticals.
            b.HasIndex(e => new { e.OrgId, e.MobileNumber })
                .HasFilter("\"MobileNumber\" IS NOT NULL")
                .HasDatabaseName("IX_ContactPersons_Mobile");

            b.HasIndex(e => new { e.OrgId, e.Email })
                .HasFilter("\"Email\" IS NOT NULL")
                .HasDatabaseName("IX_ContactPersons_Email");

            b.HasOne<Contact>()
                .WithMany()
                .HasForeignKey(e => e.ContactId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict, not cascade: deleting a role must not silently delete
            // the people who hold it.
            b.HasOne<ContactPersonRole>()
                .WithMany()
                .HasForeignKey(e => e.ContactPersonRoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ContactPersonRole>(b =>
        {
            b.HasKey(e => e.ContactPersonRoleId);
            b.HasIndex(e => new { e.OrgId, e.RoleName }).IsUnique();

            b.HasIndex(e => new { e.OrgId, e.RoleSystemName })
                .IsUnique()
                .HasFilter("\"RoleSystemName\" IS NOT NULL")
                .HasDatabaseName("IX_ContactPersonRoles_SystemName");

            b.HasIndex(e => e.OrgId)
                .IsUnique()
                .HasFilter("\"IsDefault\" = true")
                .HasDatabaseName("IX_ContactPersonRoles_Default");

            b.HasIndex(e => new { e.OrgId, e.DisplayOrder, e.RoleName })
                .HasDatabaseName("IX_ContactPersonRoles_Order");
        });

        modelBuilder.Entity<ContactBankDetail>(b =>
        {
            b.HasKey(e => e.ContactBankDetailId);
            b.HasIndex(e => new { e.OrgId, e.ContactId });

            // At most one default payout account per contact.
            b.HasIndex(e => new { e.OrgId, e.ContactId })
                .IsUnique()
                .HasFilter("\"IsDefault\" = true")
                .HasDatabaseName("IX_ContactBankDetails_Default");

            b.Property(e => e.AccountKind).HasConversion<string>().HasMaxLength(10);

            b.HasOne<Contact>()
                .WithMany()
                .HasForeignKey(e => e.ContactId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContactLicence>(b =>
        {
            b.HasKey(e => e.ContactLicenceId);
            b.HasIndex(e => new { e.OrgId, e.ContactId });

            // The expiring-licences report scans this: everything with a date,
            // ordered by it. Filtered, because licences without one never match.
            b.HasIndex(e => new { e.OrgId, e.ExpiresOn })
                .HasFilter("\"ExpiresOn\" IS NOT NULL")
                .HasDatabaseName("IX_ContactLicences_Expiry");

            b.Property(e => e.LicenceType).HasConversion<string>().HasMaxLength(20);

            b.HasOne<Contact>()
                .WithMany()
                .HasForeignKey(e => e.ContactId)
                .OnDelete(DeleteBehavior.Cascade);

            b.ToTable(table => table.HasCheckConstraint(
                "chk_licence_dates",
                "\"IssuedOn\" IS NULL OR \"ExpiresOn\" IS NULL OR \"ExpiresOn\" >= \"IssuedOn\""));
        });

        modelBuilder.Entity<ContactAttachment>(b =>
        {
            b.HasKey(e => e.ContactAttachmentId);
            b.HasIndex(e => new { e.OrgId, e.ContactId });

            // One row per stored blob. Two rows pointing at one file would make
            // deleting either of them orphan or destroy the other's bytes.
            b.HasIndex(e => new { e.OrgId, e.StoragePath })
                .IsUnique()
                .HasDatabaseName("IX_ContactAttachments_Storage");

            b.Property(e => e.DocumentType).HasConversion<string>().HasMaxLength(20);

            b.HasOne<Contact>()
                .WithMany()
                .HasForeignKey(e => e.ContactId)
                .OnDelete(DeleteBehavior.Cascade);

            b.ToTable(table => table.HasCheckConstraint(
                "chk_attachment_size",
                "\"FileSizeBytes\" > 0"));
        });

        // Base class applies query filters, OrgId indexes and xmin last so it
        // sees every entity configured above.
        base.OnModelCreating(modelBuilder);
    }
}
