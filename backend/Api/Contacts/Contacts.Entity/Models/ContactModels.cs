using System.ComponentModel.DataAnnotations;

namespace Contacts.Entity.Models;

/// <summary>A row on the contact list. Email and phone come from the default person.</summary>
public class ContactListItem
{
    public long ContactId { get; set; }

    public string ContactCode { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string ContactCategory { get; set; } = null!;

    public bool IsCustomer { get; set; }

    public bool IsVendor { get; set; }

    public bool IsJobWorker { get; set; }

    public bool IsPrescriber { get; set; }

    public string? Gstin { get; set; }

    public string GstRegistrationType { get; set; } = null!;

    public string CurrencyCode { get; set; } = null!;

    public decimal? CreditLimit { get; set; }

    /// <summary>
    /// False when Accounting never created this contact's receivable and
    /// payable sub-accounts. The contact is saved and editable; it just cannot
    /// be posted against until the sub-ledger exists.
    /// </summary>
    public bool IsSubLedgerLinked { get; set; }

    public bool IsActive { get; set; }

    /// <summary>From the default contact person — the contact itself stores neither.</summary>
    public string? Email { get; set; }

    public string? MobileNumber { get; set; }

    public string? City { get; set; }
}

public class ContactDetail : ContactListItem
{
    public string? LegalName { get; set; }

    public string? Pan { get; set; }

    public string? Tan { get; set; }

    public int? PlaceOfSupplyStateId { get; set; }

    public int? CountryId { get; set; }

    public long? PaymentTermId { get; set; }

    public int? MaxOutstandingDays { get; set; }

    public decimal? MaxDiscountPercent { get; set; }

    public long? ReceivableAccountId { get; set; }

    public long? PayableAccountId { get; set; }

    public bool IsTdsApplicable { get; set; }

    public string? TdsSection { get; set; }

    public bool IsMsme { get; set; }

    public string? UdyamNumber { get; set; }

    public string? Notes { get; set; }

    public List<ContactAddressModel> Addresses { get; set; } = [];

    public List<ContactPersonModel> Persons { get; set; } = [];

    public List<ContactBankDetailModel> BankDetails { get; set; } = [];

    public List<ContactLicenceModel> Licences { get; set; } = [];

    /// <summary>Read-only here; files are uploaded and removed on their own endpoint.</summary>
    public List<ContactAttachmentModel> Attachments { get; set; } = [];
}

public class ContactAddressModel
{
    /// <summary>Zero on a row being added.</summary>
    public long ContactAddressId { get; set; }

    [Required(ErrorMessage = "Address type is required.")]
    public string AddressType { get; set; } = "Billing";

    public bool IsDefault { get; set; }

    [MaxLength(50, ErrorMessage = "Label cannot exceed 50 characters.")]
    public string? Label { get; set; }

    [Required(ErrorMessage = "Address line 1 is required.")]
    [MaxLength(200, ErrorMessage = "Address line 1 cannot exceed 200 characters.")]
    public string AddressLine1 { get; set; } = null!;

    [MaxLength(200, ErrorMessage = "Address line 2 cannot exceed 200 characters.")]
    public string? AddressLine2 { get; set; }

    [MaxLength(100, ErrorMessage = "Landmark cannot exceed 100 characters.")]
    public string? Landmark { get; set; }

    [Required(ErrorMessage = "City is required.")]
    [MaxLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string City { get; set; } = null!;

    public int? StateId { get; set; }

    public int CountryId { get; set; }

    [MaxLength(10, ErrorMessage = "Postal code cannot exceed 10 characters.")]
    public string? PostalCode { get; set; }

    [MaxLength(15, ErrorMessage = "GSTIN must be 15 characters.")]
    public string? Gstin { get; set; }

    [MaxLength(100, ErrorMessage = "Contact person cannot exceed 100 characters.")]
    public string? ContactPersonName { get; set; }

    [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
    public string? PhoneNumber { get; set; }

    [MaxLength(20, ErrorMessage = "Mobile number cannot exceed 20 characters.")]
    public string? MobileNumber { get; set; }

    public bool IsActive { get; set; } = true;
}

public class ContactPersonModel
{
    /// <summary>Zero on a row being added.</summary>
    public long ContactPersonId { get; set; }

    public long ContactPersonRoleId { get; set; }

    /// <summary>Read-only, filled on the way out so the grid need not join.</summary>
    public string? RoleName { get; set; }

    [MaxLength(10, ErrorMessage = "Salutation cannot exceed 10 characters.")]
    public string? Salutation { get; set; }

    [Required(ErrorMessage = "First name is required.")]
    [MaxLength(100, ErrorMessage = "First name cannot exceed 100 characters.")]
    public string FirstName { get; set; } = null!;

    [MaxLength(100, ErrorMessage = "Last name cannot exceed 100 characters.")]
    public string? LastName { get; set; }

    [MaxLength(100, ErrorMessage = "Designation cannot exceed 100 characters.")]
    public string? Designation { get; set; }

    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [MaxLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
    public string? Email { get; set; }

    [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
    public string? PhoneNumber { get; set; }

    [MaxLength(20, ErrorMessage = "Mobile number cannot exceed 20 characters.")]
    public string? MobileNumber { get; set; }

    [MaxLength(200, ErrorMessage = "Website cannot exceed 200 characters.")]
    public string? Website { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// The whole contact in one payload. Addresses and persons save with the parent
/// rather than through their own endpoints — "at least one person, exactly one
/// default" is a rule about the set, so the set has to arrive together.
/// </summary>
public class SaveContactRequest
{
    /// <summary>
    /// Leave empty to take the next code from the numbering series. Only honoured
    /// when that series allows a manual override.
    /// </summary>
    [MaxLength(20, ErrorMessage = "Contact code cannot exceed 20 characters.")]
    public string? ContactCode { get; set; }

    public bool IsCustomer { get; set; }

    public bool IsVendor { get; set; }

    public bool IsJobWorker { get; set; }

    public bool IsPrescriber { get; set; }

    [Required(ErrorMessage = "Contact category is required.")]
    public string ContactCategory { get; set; } = "Business";

    [Required(ErrorMessage = "Display name is required.")]
    [MaxLength(200, ErrorMessage = "Display name cannot exceed 200 characters.")]
    public string DisplayName { get; set; } = null!;

    [MaxLength(200, ErrorMessage = "Legal name cannot exceed 200 characters.")]
    public string? LegalName { get; set; }

    [MaxLength(15, ErrorMessage = "GSTIN must be 15 characters.")]
    public string? Gstin { get; set; }

    [Required(ErrorMessage = "GST registration type is required.")]
    public string GstRegistrationType { get; set; } = "Unregistered";

    [MaxLength(10, ErrorMessage = "PAN must be 10 characters.")]
    public string? Pan { get; set; }

    [MaxLength(10, ErrorMessage = "TAN cannot exceed 10 characters.")]
    public string? Tan { get; set; }

    public int? PlaceOfSupplyStateId { get; set; }

    public int? CountryId { get; set; }

    [Required(ErrorMessage = "Currency is required.")]
    [MaxLength(3, ErrorMessage = "Currency must be a 3-letter code.")]
    public string CurrencyCode { get; set; } = "INR";

    public long? PaymentTermId { get; set; }

    public decimal? CreditLimit { get; set; }

    public int? MaxOutstandingDays { get; set; }

    public decimal? MaxDiscountPercent { get; set; }

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

    public bool IsActive { get; set; } = true;

    public List<ContactAddressModel> Addresses { get; set; } = [];

    public List<ContactPersonModel> Persons { get; set; } = [];

    public List<ContactBankDetailModel> BankDetails { get; set; } = [];

    public List<ContactLicenceModel> Licences { get; set; } = [];
}

public enum SaveContactOutcome
{
    Ok = 0,
    NotFound = 1,
    NoRole = 2,
    DuplicateCode = 3,
    DuplicateGstin = 4,
    GstinRequired = 5,
    GstinNotAllowed = 6,
    GstinStateMismatch = 7,
    UnknownState = 8,
    NoContactPerson = 9,
    NoDefaultPerson = 10,
    DefaultPersonNeedsContactDetail = 11,
    MultipleDefaultAddresses = 12,
    TdsSectionRequired = 13,
    UdyamNumberRequired = 14,
    InvalidValue = 15,
    UnknownRole = 16,
    /// <summary>More than one payout account marked as the default.</summary>
    MultipleDefaultBankDetails = 17,
    /// <summary>
    /// The contact was saved but Accounting could not create its sub-accounts.
    /// Reported rather than rolled back: an HTTP call cannot join the local
    /// transaction, so undoing the contact after Accounting had already written
    /// would be the worse failure.
    /// </summary>
    SubLedgerUnavailable = 18,
    /// <summary>Retrying the sub-ledger for a contact that already has one.</summary>
    SubLedgerAlreadyLinked = 19,
}

public sealed record SaveContactResult(SaveContactOutcome Outcome, long? ContactId, string? ContactCode);

public class ContactBankDetailModel
{
    /// <summary>Zero on a row being added.</summary>
    public long ContactBankDetailId { get; set; }

    [Required(ErrorMessage = "Account holder name is required.")]
    [MaxLength(100, ErrorMessage = "Account holder name cannot exceed 100 characters.")]
    public string AccountHolderName { get; set; } = null!;

    [Required(ErrorMessage = "Bank name is required.")]
    [MaxLength(100, ErrorMessage = "Bank name cannot exceed 100 characters.")]
    public string BankName { get; set; } = null!;

    [Required(ErrorMessage = "Account number is required.")]
    [MaxLength(30, ErrorMessage = "Account number cannot exceed 30 characters.")]
    public string AccountNumber { get; set; } = null!;

    [MaxLength(11, ErrorMessage = "IFSC must be 11 characters.")]
    public string? Ifsc { get; set; }

    [MaxLength(100, ErrorMessage = "Branch name cannot exceed 100 characters.")]
    public string? BranchName { get; set; }

    [Required(ErrorMessage = "Account kind is required.")]
    public string AccountKind { get; set; } = "Current";

    [MaxLength(100, ErrorMessage = "UPI id cannot exceed 100 characters.")]
    public string? UpiId { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;
}

public class ContactLicenceModel
{
    /// <summary>Zero on a row being added.</summary>
    public long ContactLicenceId { get; set; }

    [Required(ErrorMessage = "Licence type is required.")]
    public string LicenceType { get; set; } = "DrugLicence";

    [Required(ErrorMessage = "Licence number is required.")]
    [MaxLength(50, ErrorMessage = "Licence number cannot exceed 50 characters.")]
    public string LicenceNumber { get; set; } = null!;

    [MaxLength(100, ErrorMessage = "Description cannot exceed 100 characters.")]
    public string? Description { get; set; }

    [MaxLength(100, ErrorMessage = "Issuing authority cannot exceed 100 characters.")]
    public string? IssuingAuthority { get; set; }

    public DateOnly? IssuedOn { get; set; }

    public DateOnly? ExpiresOn { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Filled on the way out: days until expiry, negative once lapsed.</summary>
    public int? DaysUntilExpiry { get; set; }
}

/// <summary>
/// An uploaded file. Read-only through the contact aggregate — files arrive on
/// their own endpoint, because they cannot travel in a JSON body.
/// </summary>
public class ContactAttachmentModel
{
    public long ContactAttachmentId { get; set; }

    public long ContactId { get; set; }

    public string DocumentType { get; set; } = null!;

    public string FileName { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public long FileSizeBytes { get; set; }

    public string? Description { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public DateTimeOffset? UploadedAt { get; set; }

    /// <summary>
    /// Set when the backing store can mint a time-limited link. Null means the
    /// caller should fetch the bytes from the download endpoint instead.
    /// </summary>
    public string? DownloadUrl { get; set; }
}

public class UploadAttachmentRequest
{
    [Required(ErrorMessage = "Document type is required.")]
    public string DocumentType { get; set; } = "Other";

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    public DateOnly? ExpiryDate { get; set; }
}

public enum AttachmentOutcome
{
    Ok = 0,
    ContactNotFound = 1,
    NotFound = 2,
    NoFile = 3,
    /// <summary>Larger than FileStorage:MaxBytes.</summary>
    TooLarge = 4,
    /// <summary>Content type is not on the allowlist.</summary>
    UnsupportedType = 5,
    InvalidValue = 6,
}

/// <summary>A licence that has expired, or is about to.</summary>
public class ExpiringLicenceItem
{
    public long ContactId { get; set; }

    public string ContactCode { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public long ContactLicenceId { get; set; }

    public string LicenceType { get; set; } = null!;

    public string LicenceNumber { get; set; } = null!;

    public DateOnly ExpiresOn { get; set; }

    /// <summary>Negative once lapsed.</summary>
    public int DaysUntilExpiry { get; set; }
}
