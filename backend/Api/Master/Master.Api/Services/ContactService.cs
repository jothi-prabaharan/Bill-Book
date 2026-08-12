using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;

namespace Master.Api.Services;

/// <summary>
/// Customers, vendors, job workers and prescribers. Saved as an aggregate —
/// contact, addresses and people in one payload — because the rules that matter
/// are rules about the set: at least one person, exactly one default, one
/// default address per type. Split across endpoints, a valid contact could not
/// be created in a single step.
/// </summary>
public sealed class ContactService
{
    private readonly ContactsDbContext _db;
    private readonly INumberGenerator _numbers;
    private readonly IStateDirectory _states;
    private readonly IAccountingSubAccounts _subAccounts;
    private readonly TimeProvider _clock;

    public ContactService(
        ContactsDbContext db,
        INumberGenerator numbers,
        IStateDirectory states,
        IAccountingSubAccounts subAccounts,
        TimeProvider clock)
    {
        _db = db;
        _numbers = numbers;
        _states = states;
        _subAccounts = subAccounts;
        _clock = clock;
    }

    /// <summary>
    /// The list. Email, mobile and city come from the default person and default
    /// billing address, resolved in one extra query each rather than per row —
    /// this screen is the classic N+1.
    /// </summary>
    public async Task<IReadOnlyList<ContactListItem>> ListAsync(
        string? search, string? role, bool includeInactive, CancellationToken ct)
    {
        IQueryable<Contact> query = _db.Contacts;

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        query = role?.ToLowerInvariant() switch
        {
            "customer" => query.Where(c => c.IsCustomer),
            "vendor" => query.Where(c => c.IsVendor),
            "jobworker" => query.Where(c => c.IsJobWorker),
            "prescriber" => query.Where(c => c.IsPrescriber),
            _ => query,
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(c =>
                EF.Functions.ILike(c.DisplayName, $"%{term}%")
                || EF.Functions.ILike(c.ContactCode, $"%{term}%")
                || (c.Gstin != null && EF.Functions.ILike(c.Gstin, $"%{term}%")));
        }

        List<Contact> contacts = await query
            .OrderBy(c => c.DisplayName)
            .Take(500)
            .ToListAsync(ct);

        List<long> ids = contacts.Select(c => c.ContactId).ToList();

        Dictionary<long, ContactPerson> defaultPersons = await _db.ContactPersons
            .Where(p => ids.Contains(p.ContactId) && p.IsDefault)
            .ToDictionaryAsync(p => p.ContactId, ct);

        Dictionary<long, string> cities = await _db.ContactAddresses
            .Where(a => ids.Contains(a.ContactId)
                && a.IsDefault
                && a.AddressType == AddressType.Billing)
            .ToDictionaryAsync(a => a.ContactId, a => a.City, ct);

        return contacts.Select(c =>
        {
            ContactListItem item = MapSummary(c);
            if (defaultPersons.TryGetValue(c.ContactId, out ContactPerson? person))
            {
                item.Email = person.Email;
                item.MobileNumber = person.MobileNumber;
            }

            item.City = cities.GetValueOrDefault(c.ContactId);
            return item;
        }).ToList();
    }

    public async Task<ContactDetail?> GetAsync(long contactId, CancellationToken ct)
    {
        Contact? contact = await _db.Contacts.FirstOrDefaultAsync(c => c.ContactId == contactId, ct);
        if (contact is null)
        {
            return null;
        }

        List<ContactAddress> addresses = await _db.ContactAddresses
            .Where(a => a.ContactId == contactId)
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.Label)
            .ToListAsync(ct);

        List<ContactPerson> persons = await _db.ContactPersons
            .Where(p => p.ContactId == contactId)
            .ToListAsync(ct);

        Dictionary<long, string> roleNames = await _db.ContactPersonRoles
            .ToDictionaryAsync(r => r.ContactPersonRoleId, r => r.RoleName, ct);

        ContactDetail detail = MapDetail(contact);
        detail.Addresses = addresses.Select(MapAddress).ToList();
        detail.Persons = persons
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.FirstName)
            .Select(p =>
            {
                ContactPersonModel model = MapPerson(p);
                model.RoleName = roleNames.GetValueOrDefault(p.ContactPersonRoleId);
                return model;
            })
            .ToList();

        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        detail.BankDetails = await _db.ContactBankDetails
            .Where(b => b.ContactId == contactId)
            .OrderByDescending(b => b.IsDefault)
            .ThenBy(b => b.BankName)
            .Select(b => new ContactBankDetailModel
            {
                ContactBankDetailId = b.ContactBankDetailId,
                AccountHolderName = b.AccountHolderName,
                BankName = b.BankName,
                AccountNumber = b.AccountNumber,
                Ifsc = b.Ifsc,
                BranchName = b.BranchName,
                AccountKind = b.AccountKind.ToString(),
                UpiId = b.UpiId,
                IsDefault = b.IsDefault,
                IsActive = b.IsActive,
            })
            .ToListAsync(ct);

        List<ContactLicence> licences = await _db.ContactLicences
            .Where(l => l.ContactId == contactId)
            .OrderBy(l => l.ExpiresOn ?? DateOnly.MaxValue)
            .ToListAsync(ct);

        detail.Licences = licences.Select(l => new ContactLicenceModel
        {
            ContactLicenceId = l.ContactLicenceId,
            LicenceType = l.LicenceType.ToString(),
            LicenceNumber = l.LicenceNumber,
            Description = l.Description,
            IssuingAuthority = l.IssuingAuthority,
            IssuedOn = l.IssuedOn,
            ExpiresOn = l.ExpiresOn,
            IsActive = l.IsActive,
            // Negative once lapsed, so the screen can say so without recomputing.
            DaysUntilExpiry = l.ExpiresOn is DateOnly expires
                ? expires.DayNumber - today.DayNumber
                : null,
        }).ToList();

        detail.Attachments = await _db.ContactAttachments
            .Where(a => a.ContactId == contactId && a.IsActive)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new ContactAttachmentModel
            {
                ContactAttachmentId = a.ContactAttachmentId,
                ContactId = a.ContactId,
                DocumentType = a.DocumentType.ToString(),
                FileName = a.FileName,
                ContentType = a.ContentType,
                FileSizeBytes = a.FileSizeBytes,
                Description = a.Description,
                ExpiryDate = a.ExpiryDate,
                UploadedAt = a.CreatedAt,
            })
            .ToListAsync(ct);

        ContactPerson? defaultPerson = persons.FirstOrDefault(p => p.IsDefault);
        detail.Email = defaultPerson?.Email;
        detail.MobileNumber = defaultPerson?.MobileNumber;
        detail.City = addresses
            .FirstOrDefault(a => a.IsDefault && a.AddressType == AddressType.Billing)?.City;

        return detail;
    }

    public async Task<SaveContactResult> CreateAsync(SaveContactRequest request, CancellationToken ct)
    {
        if (!TryParse(request, out ContactCategory category, out GstRegistrationType registration))
        {
            return new SaveContactResult(SaveContactOutcome.InvalidValue, null, null);
        }

        SaveContactOutcome invalid = await ValidateAsync(request, registration, null, ct);
        if (invalid != SaveContactOutcome.Ok)
        {
            return new SaveContactResult(invalid, null, null);
        }

        string code = await ResolveCodeAsync(request, ct);

        if (await _db.Contacts.AnyAsync(c => c.ContactCode == code, ct))
        {
            return new SaveContactResult(SaveContactOutcome.DuplicateCode, null, null);
        }

        var contact = new Contact { ContactCode = code };
        Apply(contact, request, category, registration);

        _db.Contacts.Add(contact);
        await _db.SaveChangesAsync(ct);

        await ReplaceChildrenAsync(contact.ContactId, request, ct);
        await _db.SaveChangesAsync(ct);

        // Two sub-accounts, receivable and payable, written by Accounting.
        //
        // Reported, not rolled back. An HTTP call cannot join this transaction,
        // so undoing the contact after Accounting had already written its rows
        // would be the worse failure — the contact stays, plainly missing its
        // sub-ledger, and can be retried. Same shape as an unlinked bank
        // account.
        bool provisioned = await _subAccounts.ProvisionForContactAsync(
            contact.ContactId, contact.DisplayName, ct);

        if (!provisioned)
        {
            return new SaveContactResult(
                SaveContactOutcome.SubLedgerUnavailable, contact.ContactId, contact.ContactCode);
        }

        contact.SubLedgerProvisionedAt = _clock.GetUtcNow();
        await _db.SaveChangesAsync(ct);

        return new SaveContactResult(SaveContactOutcome.Ok, contact.ContactId, contact.ContactCode);
    }

    /// <summary>
    /// Creates the sub-accounts for a contact whose call failed when it was
    /// saved. Idempotent on this side by the timestamp, and Accounting's own
    /// provisioning is idempotent too, so running it twice is harmless.
    /// </summary>
    public async Task<SaveContactOutcome> RetrySubLedgerAsync(
        long contactId, CancellationToken ct)
    {
        Contact? contact = await _db.Contacts
            .FirstOrDefaultAsync(c => c.ContactId == contactId, ct);

        if (contact is null)
        {
            return SaveContactOutcome.NotFound;
        }

        if (contact.SubLedgerProvisionedAt is not null)
        {
            return SaveContactOutcome.SubLedgerAlreadyLinked;
        }

        bool provisioned = await _subAccounts.ProvisionForContactAsync(
            contact.ContactId, contact.DisplayName, ct);

        if (!provisioned)
        {
            return SaveContactOutcome.SubLedgerUnavailable;
        }

        contact.SubLedgerProvisionedAt = _clock.GetUtcNow();
        await _db.SaveChangesAsync(ct);

        return SaveContactOutcome.Ok;
    }

    public async Task<SaveContactResult> UpdateAsync(
        long contactId, SaveContactRequest request, CancellationToken ct)
    {
        Contact? contact = await _db.Contacts.FirstOrDefaultAsync(c => c.ContactId == contactId, ct);
        if (contact is null)
        {
            return new SaveContactResult(SaveContactOutcome.NotFound, null, null);
        }

        if (!TryParse(request, out ContactCategory category, out GstRegistrationType registration))
        {
            return new SaveContactResult(SaveContactOutcome.InvalidValue, null, null);
        }

        SaveContactOutcome invalid = await ValidateAsync(request, registration, contactId, ct);
        if (invalid != SaveContactOutcome.Ok)
        {
            return new SaveContactResult(invalid, null, null);
        }

        // The code is not reissued on update: it is on documents already, and
        // the numbering series has moved on.
        Apply(contact, request, category, registration);

        await ReplaceChildrenAsync(contactId, request, ct);
        await _db.SaveChangesAsync(ct);

        return new SaveContactResult(SaveContactOutcome.Ok, contact.ContactId, contact.ContactCode);
    }

    /// <summary>
    /// Deactivates. Contacts are never hard-deleted — documents point at them,
    /// and their sub-accounts hold ledger history.
    /// </summary>
    public async Task<SaveContactOutcome> DeactivateAsync(long contactId, CancellationToken ct)
    {
        Contact? contact = await _db.Contacts.FirstOrDefaultAsync(c => c.ContactId == contactId, ct);
        if (contact is null)
        {
            return SaveContactOutcome.NotFound;
        }

        contact.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return SaveContactOutcome.Ok;
    }

    /// <summary>
    /// Every rule that spans rows or crosses a service, in one place. Row-level
    /// rules are check constraints on the table and are not repeated here.
    /// </summary>
    private async Task<SaveContactOutcome> ValidateAsync(
        SaveContactRequest request,
        GstRegistrationType registration,
        long? existingContactId,
        CancellationToken ct)
    {
        if (!request.IsCustomer && !request.IsVendor && !request.IsJobWorker && !request.IsPrescriber)
        {
            return SaveContactOutcome.NoRole;
        }

        bool gstinRequired = registration is GstRegistrationType.Regular
            or GstRegistrationType.Composition
            or GstRegistrationType.Sez;

        if (gstinRequired && string.IsNullOrWhiteSpace(request.Gstin))
        {
            return SaveContactOutcome.GstinRequired;
        }

        if (!gstinRequired && !string.IsNullOrWhiteSpace(request.Gstin))
        {
            return SaveContactOutcome.GstinNotAllowed;
        }

        if (request.IsTdsApplicable && string.IsNullOrWhiteSpace(request.TdsSection))
        {
            return SaveContactOutcome.TdsSectionRequired;
        }

        if (request.IsMsme && string.IsNullOrWhiteSpace(request.UdyamNumber))
        {
            return SaveContactOutcome.UdyamNumberRequired;
        }

        // The check CLAUDE.md singles out: a GSTIN whose first two digits do not
        // match the place of supply sends every document down the wrong branch of
        // tax determination — CGST+SGST where IGST belongs, silently.
        if (!string.IsNullOrWhiteSpace(request.Gstin) && request.PlaceOfSupplyStateId is int stateId)
        {
            string? stateCode = await _states.GetStateCodeAsync(stateId, ct);
            if (stateCode is null)
            {
                return SaveContactOutcome.UnknownState;
            }

            if (!request.Gstin.StartsWith(stateCode, StringComparison.Ordinal))
            {
                return SaveContactOutcome.GstinStateMismatch;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Gstin))
        {
            bool duplicate = await _db.Contacts.AnyAsync(
                c => c.Gstin == request.Gstin
                    && (existingContactId == null || c.ContactId != existingContactId),
                ct);

            if (duplicate)
            {
                return SaveContactOutcome.DuplicateGstin;
            }
        }

        List<ContactPersonModel> persons = request.Persons.Where(p => p.IsActive).ToList();
        if (persons.Count == 0)
        {
            return SaveContactOutcome.NoContactPerson;
        }

        List<ContactPersonModel> defaults = persons.Where(p => p.IsDefault).ToList();
        if (defaults.Count != 1)
        {
            return SaveContactOutcome.NoDefaultPerson;
        }

        // Contact-level email and phone resolve to this row, so a default with
        // neither leaves invoices and reminders with nowhere to go.
        ContactPersonModel defaultPerson = defaults[0];
        if (string.IsNullOrWhiteSpace(defaultPerson.Email)
            && string.IsNullOrWhiteSpace(defaultPerson.MobileNumber))
        {
            return SaveContactOutcome.DefaultPersonNeedsContactDetail;
        }

        long[] roleIds = persons.Select(p => p.ContactPersonRoleId).Distinct().ToArray();
        int known = await _db.ContactPersonRoles
            .CountAsync(r => roleIds.Contains(r.ContactPersonRoleId), ct);

        if (known != roleIds.Length)
        {
            return SaveContactOutcome.UnknownRole;
        }

        // The filtered unique index would reject this too, but as a 500 rather
        // than something the user can act on.
        bool duplicateDefaults = request.Addresses
            .Where(a => a.IsActive && a.IsDefault)
            .GroupBy(a => a.AddressType)
            .Any(g => g.Count() > 1);

        if (duplicateDefaults)
        {
            return SaveContactOutcome.MultipleDefaultAddresses;
        }

        // Same rule as addresses and people: the filtered unique index would
        // catch it, but as a 500 rather than something a user can act on.
        return request.BankDetails.Count(b => b.IsActive && b.IsDefault) > 1
            ? SaveContactOutcome.MultipleDefaultBankDetails
            : SaveContactOutcome.Ok;
    }

    /// <summary>
    /// Takes the next code from the numbering series unless the caller supplied
    /// one. Allocated here, inside the same transaction as the insert, so a
    /// failed save gives the number back.
    /// </summary>
    private async Task<string> ResolveCodeAsync(SaveContactRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.ContactCode))
        {
            return request.ContactCode.Trim();
        }

        // A party that both buys and sells gets a customer code: it is the one
        // most often quoted back by the customer themselves.
        string seriesCode = request.IsCustomer || request.IsPrescriber ? "CUSTOMER" : "VENDOR";
        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        NumberAllocation allocation = await _numbers.NextAsync(seriesCode, today, ct);
        return allocation.Code;
    }

    /// <summary>
    /// Replaces the addresses and people. Rows arriving with an id are updated,
    /// rows without one inserted, and rows no longer present are removed — the
    /// aggregate is what the caller sent, not a merge of it.
    /// </summary>
    private async Task ReplaceChildrenAsync(
        long contactId, SaveContactRequest request, CancellationToken ct)
    {
        List<ContactAddress> existingAddresses = await _db.ContactAddresses
            .Where(a => a.ContactId == contactId)
            .ToListAsync(ct);

        long[] keptAddressIds = request.Addresses
            .Where(a => a.ContactAddressId > 0)
            .Select(a => a.ContactAddressId)
            .ToArray();

        _db.ContactAddresses.RemoveRange(
            existingAddresses.Where(a => !keptAddressIds.Contains(a.ContactAddressId)));

        foreach (ContactAddressModel model in request.Addresses)
        {
            ContactAddress? row = model.ContactAddressId > 0
                ? existingAddresses.FirstOrDefault(a => a.ContactAddressId == model.ContactAddressId)
                : null;

            if (row is null)
            {
                row = new ContactAddress { ContactId = contactId };
                _db.ContactAddresses.Add(row);
            }

            row.AddressType = Enum.TryParse(model.AddressType, ignoreCase: true, out AddressType type)
                ? type
                : AddressType.Billing;
            row.IsDefault = model.IsDefault;
            row.Label = model.Label;
            row.AddressLine1 = model.AddressLine1;
            row.AddressLine2 = model.AddressLine2;
            row.Landmark = model.Landmark;
            row.City = model.City;
            row.StateId = model.StateId;
            row.CountryId = model.CountryId;
            row.PostalCode = model.PostalCode;
            row.Gstin = model.Gstin;
            row.ContactPersonName = model.ContactPersonName;
            row.PhoneNumber = model.PhoneNumber;
            row.MobileNumber = model.MobileNumber;
            row.IsActive = model.IsActive;
        }

        List<ContactBankDetail> existingBankDetails = await _db.ContactBankDetails
            .Where(b => b.ContactId == contactId)
            .ToListAsync(ct);

        long[] keptBankIds = request.BankDetails
            .Where(b => b.ContactBankDetailId > 0)
            .Select(b => b.ContactBankDetailId)
            .ToArray();

        _db.ContactBankDetails.RemoveRange(
            existingBankDetails.Where(b => !keptBankIds.Contains(b.ContactBankDetailId)));

        // Cleared before any is set, so the filtered unique index never sees two.
        foreach (ContactBankDetail row in existingBankDetails)
        {
            row.IsDefault = false;
        }

        foreach (ContactBankDetailModel model in request.BankDetails)
        {
            ContactBankDetail? row = model.ContactBankDetailId > 0
                ? existingBankDetails.FirstOrDefault(
                    b => b.ContactBankDetailId == model.ContactBankDetailId)
                : null;

            if (row is null)
            {
                row = new ContactBankDetail { ContactId = contactId };
                _db.ContactBankDetails.Add(row);
            }

            row.AccountHolderName = model.AccountHolderName.Trim();
            row.BankName = model.BankName.Trim();
            row.AccountNumber = model.AccountNumber.Trim();
            row.Ifsc = string.IsNullOrWhiteSpace(model.Ifsc)
                ? null
                : model.Ifsc.Trim().ToUpperInvariant();
            row.BranchName = model.BranchName;
            row.AccountKind = Enum.TryParse(model.AccountKind, ignoreCase: true, out BankAccountKind kind)
                ? kind
                : BankAccountKind.Current;
            row.UpiId = model.UpiId;
            row.IsDefault = model.IsDefault;
            row.IsActive = model.IsActive;
        }

        List<ContactLicence> existingLicences = await _db.ContactLicences
            .Where(l => l.ContactId == contactId)
            .ToListAsync(ct);

        long[] keptLicenceIds = request.Licences
            .Where(l => l.ContactLicenceId > 0)
            .Select(l => l.ContactLicenceId)
            .ToArray();

        _db.ContactLicences.RemoveRange(
            existingLicences.Where(l => !keptLicenceIds.Contains(l.ContactLicenceId)));

        foreach (ContactLicenceModel model in request.Licences)
        {
            ContactLicence? row = model.ContactLicenceId > 0
                ? existingLicences.FirstOrDefault(l => l.ContactLicenceId == model.ContactLicenceId)
                : null;

            if (row is null)
            {
                row = new ContactLicence { ContactId = contactId };
                _db.ContactLicences.Add(row);
            }

            row.LicenceType = Enum.TryParse(model.LicenceType, ignoreCase: true, out LicenceType type)
                ? type
                : LicenceType.Other;
            row.LicenceNumber = model.LicenceNumber.Trim();
            row.Description = model.Description;
            row.IssuingAuthority = model.IssuingAuthority;
            row.IssuedOn = model.IssuedOn;
            row.ExpiresOn = model.ExpiresOn;
            row.IsActive = model.IsActive;
        }

        List<ContactPerson> existingPersons = await _db.ContactPersons
            .Where(p => p.ContactId == contactId)
            .ToListAsync(ct);

        long[] keptPersonIds = request.Persons
            .Where(p => p.ContactPersonId > 0)
            .Select(p => p.ContactPersonId)
            .ToArray();

        _db.ContactPersons.RemoveRange(
            existingPersons.Where(p => !keptPersonIds.Contains(p.ContactPersonId)));

        // Defaults are cleared before any is set, so the filtered unique index
        // never sees two at once mid-save.
        foreach (ContactPerson person in existingPersons)
        {
            person.IsDefault = false;
        }

        foreach (ContactPersonModel model in request.Persons)
        {
            ContactPerson? row = model.ContactPersonId > 0
                ? existingPersons.FirstOrDefault(p => p.ContactPersonId == model.ContactPersonId)
                : null;

            if (row is null)
            {
                row = new ContactPerson { ContactId = contactId };
                _db.ContactPersons.Add(row);
            }

            row.ContactPersonRoleId = model.ContactPersonRoleId;
            row.Salutation = model.Salutation;
            row.FirstName = model.FirstName;
            row.LastName = model.LastName;
            row.Designation = model.Designation;
            row.Email = model.Email;
            row.PhoneNumber = model.PhoneNumber;
            row.MobileNumber = model.MobileNumber;
            row.Website = model.Website;
            row.IsDefault = model.IsDefault;
            row.IsActive = model.IsActive;
        }
    }

    private static void Apply(
        Contact contact,
        SaveContactRequest request,
        ContactCategory category,
        GstRegistrationType registration)
    {
        contact.IsCustomer = request.IsCustomer;
        contact.IsVendor = request.IsVendor;
        contact.IsJobWorker = request.IsJobWorker;
        contact.IsPrescriber = request.IsPrescriber;
        contact.ContactCategory = category;
        contact.DisplayName = request.DisplayName.Trim();
        contact.LegalName = request.LegalName;
        contact.Gstin = string.IsNullOrWhiteSpace(request.Gstin) ? null : request.Gstin.Trim().ToUpperInvariant();
        contact.GstRegistrationType = registration;
        contact.Pan = string.IsNullOrWhiteSpace(request.Pan) ? null : request.Pan.Trim().ToUpperInvariant();
        contact.Tan = request.Tan;
        contact.PlaceOfSupplyStateId = request.PlaceOfSupplyStateId;
        contact.CountryId = request.CountryId;
        contact.CurrencyCode = request.CurrencyCode;
        contact.PaymentTermId = request.PaymentTermId;
        contact.CreditLimit = request.CreditLimit;
        contact.MaxOutstandingDays = request.MaxOutstandingDays;
        contact.MaxDiscountPercent = request.MaxDiscountPercent;
        contact.ReceivableAccountId = request.ReceivableAccountId;
        contact.PayableAccountId = request.PayableAccountId;
        contact.IsTdsApplicable = request.IsTdsApplicable;
        contact.TdsSection = request.TdsSection;
        contact.IsMsme = request.IsMsme;
        contact.UdyamNumber = request.UdyamNumber;
        contact.Notes = request.Notes;
        contact.IsActive = request.IsActive;
    }

    private static bool TryParse(
        SaveContactRequest request,
        out ContactCategory category,
        out GstRegistrationType registration) =>
        Enum.TryParse(request.ContactCategory, ignoreCase: true, out category)
        & Enum.TryParse(request.GstRegistrationType, ignoreCase: true, out registration);

    private static ContactListItem MapSummary(Contact c) => new()
    {
        ContactId = c.ContactId,
        ContactCode = c.ContactCode,
        DisplayName = c.DisplayName,
        ContactCategory = c.ContactCategory.ToString(),
        IsCustomer = c.IsCustomer,
        IsVendor = c.IsVendor,
        IsJobWorker = c.IsJobWorker,
        IsPrescriber = c.IsPrescriber,
        Gstin = c.Gstin,
        GstRegistrationType = c.GstRegistrationType.ToString(),
        CurrencyCode = c.CurrencyCode,
        CreditLimit = c.CreditLimit,
        IsActive = c.IsActive,
        IsSubLedgerLinked = c.SubLedgerProvisionedAt is not null,
    };

    private static ContactDetail MapDetail(Contact c) => new()
    {
        ContactId = c.ContactId,
        ContactCode = c.ContactCode,
        DisplayName = c.DisplayName,
        ContactCategory = c.ContactCategory.ToString(),
        IsCustomer = c.IsCustomer,
        IsVendor = c.IsVendor,
        IsJobWorker = c.IsJobWorker,
        IsPrescriber = c.IsPrescriber,
        Gstin = c.Gstin,
        GstRegistrationType = c.GstRegistrationType.ToString(),
        CurrencyCode = c.CurrencyCode,
        CreditLimit = c.CreditLimit,
        IsActive = c.IsActive,
        IsSubLedgerLinked = c.SubLedgerProvisionedAt is not null,
        LegalName = c.LegalName,
        Pan = c.Pan,
        Tan = c.Tan,
        PlaceOfSupplyStateId = c.PlaceOfSupplyStateId,
        CountryId = c.CountryId,
        PaymentTermId = c.PaymentTermId,
        MaxOutstandingDays = c.MaxOutstandingDays,
        MaxDiscountPercent = c.MaxDiscountPercent,
        ReceivableAccountId = c.ReceivableAccountId,
        PayableAccountId = c.PayableAccountId,
        IsTdsApplicable = c.IsTdsApplicable,
        TdsSection = c.TdsSection,
        IsMsme = c.IsMsme,
        UdyamNumber = c.UdyamNumber,
        Notes = c.Notes,
    };

    private static ContactAddressModel MapAddress(ContactAddress a) => new()
    {
        ContactAddressId = a.ContactAddressId,
        AddressType = a.AddressType.ToString(),
        IsDefault = a.IsDefault,
        Label = a.Label,
        AddressLine1 = a.AddressLine1,
        AddressLine2 = a.AddressLine2,
        Landmark = a.Landmark,
        City = a.City,
        StateId = a.StateId,
        CountryId = a.CountryId,
        PostalCode = a.PostalCode,
        Gstin = a.Gstin,
        ContactPersonName = a.ContactPersonName,
        PhoneNumber = a.PhoneNumber,
        MobileNumber = a.MobileNumber,
        IsActive = a.IsActive,
    };

    private static ContactPersonModel MapPerson(ContactPerson p) => new()
    {
        ContactPersonId = p.ContactPersonId,
        ContactPersonRoleId = p.ContactPersonRoleId,
        Salutation = p.Salutation,
        FirstName = p.FirstName,
        LastName = p.LastName,
        Designation = p.Designation,
        Email = p.Email,
        PhoneNumber = p.PhoneNumber,
        MobileNumber = p.MobileNumber,
        Website = p.Website,
        IsDefault = p.IsDefault,
        IsActive = p.IsActive,
    };
}
