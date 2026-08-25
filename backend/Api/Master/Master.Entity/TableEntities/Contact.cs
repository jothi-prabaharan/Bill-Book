using System.ComponentModel.DataAnnotations;
using Master.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Master.Entity.TableEntities;

/// <summary>
/// Customers, vendors, job workers and prescribers — one table with role flags,
/// not four tables. In Indian SMB books the same party is routinely both a
/// customer and a vendor; separate tables would mean duplicate GSTINs, duplicate
/// addresses and two ledgers for one entity. Receivable and payable stay apart
/// through their sub-accounts, not through the master.
///
/// Contact-level email and phone deliberately do not exist here. They live on
/// ContactPersons, where exactly one row is the default — one place to look up
/// where an invoice is emailed, rather than two that can disagree.
/// </summary>
public class Contact : OrgScopedEntity
{
    public long ContactId { get; set; }

    [Required(ErrorMessage = "Contact code is required.")]
    [MaxLength(20, ErrorMessage = "Contact code cannot exceed 20 characters.")]
    public string ContactCode { get; set; } = null!;

    public bool IsCustomer { get; set; }

    public bool IsVendor { get; set; }

    /// <summary>Karigars and third-party processors who hold our stock.</summary>
    public bool IsJobWorker { get; set; }

    /// <summary>Doctors, for the Schedule H1 register.</summary>
    public bool IsPrescriber { get; set; }

    public ContactCategory ContactCategory { get; set; } = ContactCategory.Business;

    [Required(ErrorMessage = "Display name is required.")]
    [MaxLength(200, ErrorMessage = "Display name cannot exceed 200 characters.")]
    public string DisplayName { get; set; } = null!;

    /// <summary>Name as on the GST certificate. Prints on documents when set.</summary>
    [MaxLength(200, ErrorMessage = "Legal name cannot exceed 200 characters.")]
    public string? LegalName { get; set; }

    [MaxLength(15, ErrorMessage = "GSTIN must be 15 characters.")]
    [RegularExpression(
        @"^\d{2}[A-Z]{5}\d{4}[A-Z]{1}[A-Z\d]{1}[Z]{1}[A-Z\d]{1}$",
        ErrorMessage = "GSTIN is not in a valid format.")]
    public string? Gstin { get; set; }

    public GstRegistrationType GstRegistrationType { get; set; } = GstRegistrationType.Unregistered;

    [MaxLength(10, ErrorMessage = "PAN must be 10 characters.")]
    [RegularExpression(@"^[A-Z]{5}\d{4}[A-Z]$", ErrorMessage = "PAN is not in a valid format.")]
    public string? Pan { get; set; }

    [MaxLength(10, ErrorMessage = "TAN cannot exceed 10 characters.")]
    public string? Tan { get; set; }

    /// <summary>
    /// Unenforced reference to mst.States — cross-database, so validated in C#.
    /// Decides CGST+SGST versus IGST when no address overrides it.
    /// </summary>
    public int? PlaceOfSupplyStateId { get; set; }

    /// <summary>Unenforced reference to mst.Countries.</summary>
    public int? CountryId { get; set; }

    [Required(ErrorMessage = "Currency is required.")]
    [MaxLength(3, ErrorMessage = "Currency must be a 3-letter code.")]
    public string CurrencyCode { get; set; } = "INR";

    /// <summary>Unenforced reference to acc.PaymentTerms — another service, so no FK.</summary>
    public long? PaymentTermId { get; set; }

    /// <summary>Null means no limit.</summary>
    [Range(0, 999999999999.99, ErrorMessage = "Credit limit cannot be negative.")]
    public decimal? CreditLimit { get; set; }

    /// <summary>
    /// Counted from the due date, not the invoice date — otherwise a Net 60
    /// customer trips a 45-day limit while still perfectly within terms.
    /// </summary>
    [Range(0, 3650, ErrorMessage = "Maximum outstanding days must be between 0 and 3650.")]
    public int? MaxOutstandingDays { get; set; }

    [Range(0, 100, ErrorMessage = "Maximum discount must be between 0 and 100.")]
    public decimal? MaxDiscountPercent { get; set; }

    /// <summary>Overrides the org's control account. Unenforced reference to acc.Accounts.</summary>
    public long? ReceivableAccountId { get; set; }

    public long? PayableAccountId { get; set; }

    public bool IsTdsApplicable { get; set; }

    [MaxLength(10, ErrorMessage = "TDS section cannot exceed 10 characters.")]
    public string? TdsSection { get; set; }

    public bool IsMsme { get; set; }

    [MaxLength(20, ErrorMessage = "Udyam number cannot exceed 20 characters.")]
    public string? UdyamNumber { get; set; }

    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }

    /// <summary>
    /// When Accounting created this contact's receivable and payable
    /// sub-accounts. Null means it has none — the contact saved but the call
    /// failed, so it cannot be posted against yet.
    ///
    /// Stored here rather than asked of Accounting per row: the contact list is
    /// the classic N+1, and a question asked once per contact would be one HTTP
    /// call per row.
    /// </summary>
    public DateTimeOffset? SubLedgerProvisionedAt { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? DefaultPriceListId { get; set; }
}

