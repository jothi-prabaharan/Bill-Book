import { ChangeDetectionStrategy } from '@angular/core';
import { DataGridComponent, ColumnDef , DateInputComponent , TextInputComponent , NumberInputComponent , SearchInputComponent } from '@bill-book/ui-components';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import {
  ContactPersonRole,
  ContactPersonRolesDialog,
} from '../contact-person-roles-dialog/contact-person-roles.dialog';

interface ContactListItem {
  contactId: number;
  contactCode: string;
  displayName: string;
  contactCategory: string;
  isCustomer: boolean;
  isVendor: boolean;
  isJobWorker: boolean;
  isPrescriber: boolean;
  gstin: string | null;
  gstRegistrationType: string;
  currencyCode: string;
  creditLimit: number | null;
  isActive: boolean;
  isSubLedgerLinked: boolean;
  email: string | null;
  mobileNumber: string | null;
  city: string | null;
}

interface AddressModel {
  contactAddressId: number;
  addressType: 'Billing' | 'Shipping';
  isDefault: boolean;
  label: string | null;
  addressLine1: string;
  addressLine2: string | null;
  landmark: string | null;
  city: string;
  stateId: number | null;
  countryId: number;
  postalCode: string | null;
  gstin: string | null;
  contactPersonName: string | null;
  phoneNumber: string | null;
  mobileNumber: string | null;
  isActive: boolean;
}

interface PersonModel {
  contactPersonId: number;
  contactPersonRoleId: number;
  roleName?: string | null;
  salutation: string | null;
  firstName: string;
  lastName: string | null;
  designation: string | null;
  email: string | null;
  phoneNumber: string | null;
  mobileNumber: string | null;
  website: string | null;
  isDefault: boolean;
  isActive: boolean;
}

interface BankDetailModel {
  contactBankDetailId: number;
  accountHolderName: string;
  bankName: string;
  accountNumber: string;
  ifsc: string | null;
  branchName: string | null;
  accountKind: 'Savings' | 'Current' | 'Other';
  upiId: string | null;
  isDefault: boolean;
  isActive: boolean;
}

interface LicenceModel {
  contactLicenceId: number;
  licenceType: string;
  licenceNumber: string;
  description: string | null;
  issuingAuthority: string | null;
  issuedOn: string | null;
  expiresOn: string | null;
  isActive: boolean;
  daysUntilExpiry: number | null;
}

interface AttachmentModel {
  contactAttachmentId: number;
  contactId: number;
  documentType: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  description: string | null;
  expiryDate: string | null;
  uploadedAt: string | null;
  downloadUrl: string | null;
}

interface ContactDetail extends ContactListItem {
  legalName: string | null;
  pan: string | null;
  tan: string | null;
  placeOfSupplyStateId: number | null;
  countryId: number | null;
  paymentTermId: number | null;
  maxOutstandingDays: number | null;
  maxDiscountPercent: number | null;
  isTdsApplicable: boolean;
  tdsSection: string | null;
  isMsme: boolean;
  udyamNumber: string | null;
  notes: string | null;
  addresses: AddressModel[];
  persons: PersonModel[];
  bankDetails: BankDetailModel[];
  licences: LicenceModel[];
  /** Read-only through the aggregate — files have their own endpoint. */
  attachments: AttachmentModel[];
}

interface PaymentTerm {
  paymentTermId: number;
  termName: string;
  isDefault: boolean;
}

interface State {
  stateId: number;
  stateCode: string;
  stateName: string;
}

type Tab = 'general' | 'addresses' | 'persons' | 'banking' | 'licences' | 'documents';

/** Mirrors Contacts.Entity.Enums.LicenceType. */
const LICENCE_TYPES: readonly { value: string; label: string }[] = [
  { value: 'DrugLicence', label: 'Drug licence' },
  { value: 'Fssai', label: 'FSSAI' },
  { value: 'Bis', label: 'BIS' },
  { value: 'MedicalRegistration', label: 'Medical registration' },
  { value: 'Other', label: 'Other' },
];

/** Mirrors Contacts.Entity.Enums.AttachmentDocumentType. */
const DOCUMENT_TYPES: readonly { value: string; label: string }[] = [
  { value: 'GstCertificate', label: 'GST certificate' },
  { value: 'PanCard', label: 'PAN card' },
  { value: 'Agreement', label: 'Agreement' },
  { value: 'PurchaseOrder', label: 'Purchase order' },
  { value: 'CancelledCheque', label: 'Cancelled cheque' },
  { value: 'Msme', label: 'MSME certificate' },
  { value: 'Licence', label: 'Licence' },
  { value: 'Other', label: 'Other' },
];

/**
 * Customers, vendors, job workers and prescribers — one master with role flags,
 * because in Indian SMB books the same party is routinely both a customer and a
 * vendor.
 *
 * The contact saves as a whole: the rules that matter are rules about the set
 * (at least one person, exactly one default, one default address per type), so
 * the set has to arrive together.
 */
changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-contacts-page',
  standalone: true,
  imports: [DataGridComponent, FormsModule, ContactPersonRolesDialog, DateInputComponent, TextInputComponent, NumberInputComponent, SearchInputComponent],
  templateUrl: './contacts.page.html',
  styleUrl: './contacts.page.scss',
})
export class ContactsPage implements OnInit {
  private readonly http = inject(HttpClient);

  /** India. Replace with the org's country once the org context service lands. */
  private readonly defaultCountryId = 1;

  protected readonly rows = signal<ContactListItem[]>([]);
  protected readonly roles = signal<ContactPersonRole[]>([]);
  protected readonly terms = signal<PaymentTerm[]>([]);
  protected readonly states = signal<State[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);
  protected readonly editorOpen = signal(false);
  protected readonly rolesOpen = signal(false);
  protected readonly editingId = signal<number | null>(null);
  protected readonly tab = signal<Tab>('general');
  protected readonly uploading = signal(false);

  protected readonly licenceTypes = LICENCE_TYPES;
  protected readonly documentTypes = DOCUMENT_TYPES;

  search = '';
  roleFilter = '';
  columns: ColumnDef[] = [
    { field: 'contactCode', header: 'CONTACT CODE' },
    { field: 'nameGstin', header: 'NAME · GSTIN' },
    { field: 'city', header: 'CITY' },
    { field: 'role', header: 'ROLE' },
    { field: 'outstanding', header: 'OUTSTANDING', dataType: 'money' },
    { field: 'creditLimit', header: 'CREDIT LIMIT', dataType: 'money' }
  ];

  documentColumns: ColumnDef[] = [
    { field: 'file', header: 'File' },
    { field: 'type', header: 'Type' },
    { field: 'size', header: 'Size' },
    { field: 'expires', header: 'Expires' },
    { field: 'actions', header: 'Actions' }
  ];

  showInactive = false;

  uploadDocumentType = 'Other';
  uploadDescription: string | null = null;
  uploadExpiryDate: string | null = null;

  form: ContactDetail = this.blank();

  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  ngOnInit(): void {
    void this.load();
    void this.loadLookups();
    this.route.queryParamMap.subscribe(params => {
      if (params.get('action') === 'create') {
        this.openAdd();
      }
    });
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      const query = new URLSearchParams();
      if (this.search.trim()) {
        query.set('search', this.search.trim());
      }
      if (this.roleFilter) {
        query.set('role', this.roleFilter);
      }
      query.set('includeInactive', String(this.showInactive));

      this.rows.set(await this.get<ContactListItem[]>(`/api/contacts?${query}`));
    } catch {
      this.fail('Could not load contacts.');
    } finally {
      this.busy.set(false);
    }
  }

  async loadLookups(): Promise<void> {
    try {
      this.roles.set(await this.get<ContactPersonRole[]>('/api/contact-person-roles'));
    } catch {
      // The editor needs roles; the list does not. Failing quietly here keeps
      // the list usable when Contacts is up but the roles call fails.
    }

    try {
      this.terms.set(await this.get<PaymentTerm[]>('/api/payment-terms'));
    } catch {
      /* payment terms are optional on a contact */
    }

    try {
      this.states.set(
        await this.get<State[]>(`/api/master/countries/${this.defaultCountryId}/states`),
      );
    } catch {
      /* the GSTIN/state check happens server-side regardless */
    }
  }

  openAdd(): void {
    this.editingId.set(null);
    this.form = this.blank();
    this.addPerson();
    this.tab.set('general');
    this.editorOpen.set(true);
  }

  async openEdit(row: ContactListItem): Promise<void> {
    this.busy.set(true);
    try {
      const detail = await this.get<ContactDetail>(`/api/contacts/${row.contactId}`);

      // The child collections drive @for loops, so an absent one has to become
      // an empty array rather than reach the template as undefined.
      this.form = {
        ...detail,
        addresses: detail.addresses ?? [],
        persons: detail.persons ?? [],
        bankDetails: detail.bankDetails ?? [],
        licences: detail.licences ?? [],
        attachments: detail.attachments ?? [],
      };
      this.editingId.set(row.contactId);
      this.tab.set('general');
      this.editorOpen.set(true);
    } catch {
      this.fail('Could not open that contact.');
    } finally {
      this.busy.set(false);
    }
  }

  addAddress(): void {
    this.form.addresses = [
      ...this.form.addresses,
      {
        contactAddressId: 0,
        addressType: 'Billing',
        isDefault: !this.form.addresses.some((a) => a.addressType === 'Billing' && a.isDefault),
        label: null,
        addressLine1: '',
        addressLine2: null,
        landmark: null,
        city: '',
        stateId: null,
        countryId: this.defaultCountryId,
        postalCode: null,
        gstin: null,
        contactPersonName: null,
        phoneNumber: null,
        mobileNumber: null,
        isActive: true,
      },
    ];
  }

  removeAddress(index: number): void {
    this.form.addresses = this.form.addresses.filter((_, i) => i !== index);
  }

  /** One default per type, so setting one clears the other of the same type. */
  setDefaultAddress(index: number): void {
    const target = this.form.addresses[index];
    this.form.addresses = this.form.addresses.map((a, i) =>
      a.addressType === target.addressType ? { ...a, isDefault: i === index } : a,
    );
  }

  /** Copies the default billing address into a new shipping address. */
  copyBillingToShipping(): void {
    const billing = this.form.addresses.find((a) => a.addressType === 'Billing' && a.isDefault);
    if (!billing) {
      return;
    }

    this.form.addresses = [
      ...this.form.addresses,
      {
        ...billing,
        contactAddressId: 0,
        addressType: 'Shipping',
        isDefault: !this.form.addresses.some((a) => a.addressType === 'Shipping' && a.isDefault),
      },
    ];
  }

  addPerson(): void {
    const defaultRole = this.roles().find((r) => r.isDefault) ?? this.roles()[0];
    this.form.persons = [
      ...this.form.persons,
      {
        contactPersonId: 0,
        contactPersonRoleId: defaultRole?.contactPersonRoleId ?? 0,
        salutation: null,
        firstName: '',
        lastName: null,
        designation: null,
        email: null,
        phoneNumber: null,
        mobileNumber: null,
        website: null,
        isDefault: this.form.persons.length === 0,
        isActive: true,
      },
    ];
  }

  removePerson(index: number): void {
    const removed = this.form.persons[index];
    const remaining = this.form.persons.filter((_, i) => i !== index);

    // Never leave the contact without a default — the first remaining person
    // takes it, because contact-level email and phone resolve from that row.
    if (removed.isDefault && remaining.length > 0) {
      remaining[0] = { ...remaining[0], isDefault: true };
    }

    this.form.persons = remaining;
  }

  setDefaultPerson(index: number): void {
    this.form.persons = this.form.persons.map((p, i) => ({ ...p, isDefault: i === index }));
  }

  addBankDetail(): void {
    this.form.bankDetails = [
      ...this.form.bankDetails,
      {
        contactBankDetailId: 0,
        accountHolderName: this.form.legalName ?? this.form.displayName,
        bankName: '',
        accountNumber: '',
        ifsc: null,
        branchName: null,
        accountKind: 'Current',
        upiId: null,
        // The first account is the default; after that it is a choice.
        isDefault: !this.form.bankDetails.some((b) => b.isDefault),
        isActive: true,
      },
    ];
  }

  removeBankDetail(index: number): void {
    const removed = this.form.bankDetails[index];
    const remaining = this.form.bankDetails.filter((_, i) => i !== index);

    // A contact with accounts but no default would make a payment screen ask a
    // question it has no reason to ask.
    if (removed.isDefault && remaining.length > 0) {
      remaining[0] = { ...remaining[0], isDefault: true };
    }

    this.form.bankDetails = remaining;
  }

  setDefaultBankDetail(index: number): void {
    this.form.bankDetails = this.form.bankDetails.map((b, i) => ({
      ...b,
      isDefault: i === index,
    }));
  }

  addLicence(): void {
    this.form.licences = [
      ...this.form.licences,
      {
        contactLicenceId: 0,
        licenceType: 'DrugLicence',
        licenceNumber: '',
        description: null,
        issuingAuthority: null,
        issuedOn: null,
        expiresOn: null,
        isActive: true,
        daysUntilExpiry: null,
      },
    ];
  }

  removeLicence(index: number): void {
    this.form.licences = this.form.licences.filter((_, i) => i !== index);
  }

  /**
   * Expired / expiring / valid, from the expiry date the user is looking at
   * rather than the one the server sent — the row may have just been edited.
   */
  protected licenceStatus(licence: LicenceModel): 'none' | 'expired' | 'soon' | 'valid' {
    if (!licence.expiresOn) {
      return 'none';
    }

    const days = Math.floor(
      (Date.parse(licence.expiresOn) - Date.parse(new Date().toISOString().slice(0, 10))) / 86_400_000,
    );

    return days < 0 ? 'expired' : days <= 30 ? 'soon' : 'valid';
  }

  /**
   * Uploads immediately rather than on save. Files cannot ride in the contact's
   * JSON body, and holding them in memory until save would lose them on cancel.
   */
  async uploadFile(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    const contactId = this.editingId();

    if (!file || contactId === null) {
      return;
    }

    const body = new FormData();
    body.append('file', file);
    body.append('documentType', this.uploadDocumentType);
    if (this.uploadDescription) {
      body.append('description', this.uploadDescription);
    }
    if (this.uploadExpiryDate) {
      body.append('expiryDate', this.uploadExpiryDate);
    }

    this.uploading.set(true);
    try {
      await new Promise<unknown>((resolve, reject) =>
        this.http
          .post(`/api/contacts/${contactId}/attachments`, body)
          .subscribe({ next: resolve, error: reject }),
      );

      this.form.attachments = await this.get<AttachmentModel[]>(
        `/api/contacts/${contactId}/attachments`,
      );
      this.uploadDescription = null;
      this.uploadExpiryDate = null;
      this.succeed('File uploaded.');
    } catch (err: unknown) {
      this.fail(this.messageOf(err, 'Could not upload that file.'));
    } finally {
      this.uploading.set(false);
      // Clears the picker so choosing the same file again still fires a change.
      input.value = '';
    }
  }

  /**
   * Fetches the bytes through the API rather than linking to them: the request
   * needs the bearer token, which an anchor href cannot carry.
   */
  async downloadFile(attachment: AttachmentModel): Promise<void> {
    try {
      const blob = await new Promise<Blob>((resolve, reject) =>
        this.http
          .get(`/api/contacts/attachments/${attachment.contactAttachmentId}/download`, {
            responseType: 'blob',
          })
          .subscribe({ next: resolve, error: reject }),
      );

      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = attachment.fileName;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch {
      this.fail('Could not download that file.');
    }
  }

  async deleteFile(attachment: AttachmentModel): Promise<void> {
    try {
      await this.send('DELETE', `/api/contacts/attachments/${attachment.contactAttachmentId}`, {});
      this.form.attachments = this.form.attachments.filter(
        (a) => a.contactAttachmentId !== attachment.contactAttachmentId,
      );
      this.succeed('File removed.');
    } catch (err: unknown) {
      this.fail(this.messageOf(err, 'Could not remove that file.'));
    }
  }

  protected formatSize(bytes: number): string {
    if (bytes < 1024) {
      return `${bytes} B`;
    }

    return bytes < 1_048_576
      ? `${Math.round(bytes / 1024)} KB`
      : `${(bytes / 1_048_576).toFixed(1)} MB`;
  }

  protected labelOf(
    options: readonly { value: string; label: string }[],
    value: string,
  ): string {
    return options.find((o) => o.value === value)?.label ?? value;
  }

  onRolesClosed(): void {
    this.rolesOpen.set(false);
    void this.loadLookups();
  }

  async save(): Promise<void> {
    if (!this.validateForm()) {
      return;
    }

    const payload = {
      ...this.form,
      contactCode: this.cleanOptional(this.form.contactCode),
      displayName: this.form.displayName.trim(),
      legalName: this.cleanOptional(this.form.legalName),
      gstin: this.cleanOptional(this.form.gstin)?.toUpperCase() ?? null,
      currencyCode: this.form.currencyCode.trim().toUpperCase(),
      tdsSection: this.cleanOptional(this.form.tdsSection),
      udyamNumber: this.cleanOptional(this.form.udyamNumber),
      notes: this.cleanOptional(this.form.notes),
      addresses: this.form.addresses.map((address) => ({
        ...address,
        label: this.cleanOptional(address.label),
        addressLine1: address.addressLine1.trim(),
        addressLine2: this.cleanOptional(address.addressLine2),
        landmark: this.cleanOptional(address.landmark),
        city: address.city.trim(),
        postalCode: this.cleanOptional(address.postalCode),
        gstin: this.cleanOptional(address.gstin)?.toUpperCase() ?? null,
        contactPersonName: this.cleanOptional(address.contactPersonName),
        phoneNumber: this.cleanOptional(address.phoneNumber),
        mobileNumber: this.cleanOptional(address.mobileNumber),
      })),
      persons: this.form.persons.map((person) => ({
        ...person,
        firstName: person.firstName.trim(),
        lastName: this.cleanOptional(person.lastName),
        designation: this.cleanOptional(person.designation),
        email: this.cleanOptional(person.email),
        phoneNumber: this.cleanOptional(person.phoneNumber),
        mobileNumber: this.cleanOptional(person.mobileNumber),
        website: this.cleanOptional(person.website),
      })),
      bankDetails: this.form.bankDetails.map((bank) => ({
        ...bank,
        accountHolderName: bank.accountHolderName.trim(),
        bankName: bank.bankName.trim(),
        accountNumber: bank.accountNumber.trim(),
        ifsc: this.cleanOptional(bank.ifsc)?.toUpperCase() ?? null,
        branchName: this.cleanOptional(bank.branchName),
        upiId: this.cleanOptional(bank.upiId),
      })),
      licences: this.form.licences.map((licence) => ({
        ...licence,
        licenceType: licence.licenceType.trim(),
        licenceNumber: licence.licenceNumber.trim(),
        description: this.cleanOptional(licence.description),
        issuingAuthority: this.cleanOptional(licence.issuingAuthority),
      })),
    };

    this.busy.set(true);
    try {
      const id = this.editingId();
      let newId: number | null = null;
      if (id === null) {
        const result = await this.send<{ contactId: number }>('POST', '/api/contacts', payload);
        newId = result.contactId;
      } else {
        await this.send('PUT', `/api/contacts/${id}`, payload);
      }
      this.editorOpen.set(false);
      this.succeed('Contact saved.');

      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
      const returnMaster = this.route.snapshot.queryParamMap.get('returnMaster');
      if (id === null && returnUrl && returnMaster && newId) {
        void this.router.navigateByUrl(`${returnUrl}?returnMaster=${returnMaster}&returnId=${newId}`);
      } else {
        await this.load();
      }
    } catch (err: unknown) {
      this.fail(this.messageOf(err, 'Could not save that contact.'));
    } finally {
      this.busy.set(false);
    }
  }

  /**
   * Creates the sub-accounts for a contact whose call failed when it was saved.
   * Safe to run twice — Accounting's provisioning is idempotent.
   */
  async linkSubLedger(row: ContactListItem): Promise<void> {
    this.busy.set(true);
    try {
      await this.send('POST', `/api/contacts/${row.contactId}/link-sub-ledger`, {});
      this.succeed('Sub-accounts created.');
      await this.load();
    } catch (err: unknown) {
      this.fail(this.messageOf(err, 'Could not create the sub-accounts.'));
    } finally {
      this.busy.set(false);
    }
  }

  async deactivate(row: ContactListItem): Promise<void> {
    this.busy.set(true);
    try {
      await this.send('DELETE', `/api/contacts/${row.contactId}`, {});
      this.succeed('Contact deactivated.');
      await this.load();
    } catch (err: unknown) {
      this.fail(this.messageOf(err, 'Could not deactivate that contact.'));
    } finally {
      this.busy.set(false);
    }
  }

  protected roleNameOf(roleId: number): string {
    return this.roles().find((r) => r.contactPersonRoleId === roleId)?.roleName ?? '—';
  }

  /** Unregistered, overseas and consumer contacts cannot carry a GSTIN. */
  protected get gstinAllowed(): boolean {
    return ['Regular', 'Composition', 'Sez'].includes(this.form.gstRegistrationType);
  }

  onRegistrationChange(): void {
    if (!this.gstinAllowed) {
      this.form.gstin = null;
    }
  }

  private validateForm(): boolean {
    if (!this.hasText(this.form.displayName)) {
      this.fail('Display name is required.');
      return false;
    }

    if (!this.hasText(this.form.contactCategory)) {
      this.fail('Contact category is required.');
      return false;
    }

    if (!this.hasText(this.form.gstRegistrationType)) {
      this.fail('GST registration type is required.');
      return false;
    }

    if (!this.hasText(this.form.currencyCode)) {
      this.fail('Currency is required.');
      return false;
    }

    if (!this.form.isCustomer && !this.form.isVendor && !this.form.isJobWorker && !this.form.isPrescriber) {
      this.fail('Select at least one role (Customer, Vendor, Job worker, or Prescriber).');
      return false;
    }

    if (this.form.isTdsApplicable && !this.hasText(this.form.tdsSection)) {
      this.fail('TDS section is required when TDS is applicable.');
      return false;
    }

    if (this.form.isMsme && !this.hasText(this.form.udyamNumber)) {
      this.fail('Udyam number is required for MSME contacts.');
      return false;
    }

    if (this.form.persons.length === 0) {
      this.fail('At least one contact person is required.');
      return false;
    }

    const defaultPersons = this.form.persons.filter((person) => person.isDefault);
    if (defaultPersons.length !== 1) {
      this.fail('Exactly one default contact person is required.');
      return false;
    }

    const primary = defaultPersons[0];
    if (!this.hasText(primary.email) && !this.hasText(primary.mobileNumber)) {
      this.fail('Default contact person needs an email or mobile number.');
      return false;
    }

    for (let i = 0; i < this.form.persons.length; i += 1) {
      const person = this.form.persons[i];
      if (person.contactPersonRoleId <= 0) {
        this.fail(`Choose a role for person row ${i + 1}.`);
        return false;
      }

      if (!this.hasText(person.firstName)) {
        this.fail(`First name is required for person row ${i + 1}.`);
        return false;
      }
    }

    const billingDefaults = this.form.addresses.filter(
      (address) => address.addressType === 'Billing' && address.isDefault,
    );
    if (billingDefaults.length > 1) {
      this.fail('Only one default billing address is allowed.');
      return false;
    }

    const shippingDefaults = this.form.addresses.filter(
      (address) => address.addressType === 'Shipping' && address.isDefault,
    );
    if (shippingDefaults.length > 1) {
      this.fail('Only one default shipping address is allowed.');
      return false;
    }

    for (let i = 0; i < this.form.addresses.length; i += 1) {
      const address = this.form.addresses[i];
      if (!this.hasText(address.addressLine1)) {
        this.fail(`Address line 1 is required for address row ${i + 1}.`);
        return false;
      }

      if (!this.hasText(address.city)) {
        this.fail(`City is required for address row ${i + 1}.`);
        return false;
      }
    }

    for (let i = 0; i < this.form.bankDetails.length; i += 1) {
      const bank = this.form.bankDetails[i];
      if (!this.hasText(bank.accountHolderName)) {
        this.fail(`Account holder name is required for bank row ${i + 1}.`);
        return false;
      }

      if (!this.hasText(bank.bankName)) {
        this.fail(`Bank name is required for bank row ${i + 1}.`);
        return false;
      }

      if (!this.hasText(bank.accountNumber)) {
        this.fail(`Account number is required for bank row ${i + 1}.`);
        return false;
      }

      if (!this.hasText(bank.accountKind)) {
        this.fail(`Account type is required for bank row ${i + 1}.`);
        return false;
      }
    }

    for (let i = 0; i < this.form.licences.length; i += 1) {
      const licence = this.form.licences[i];
      if (!this.hasText(licence.licenceType)) {
        this.fail(`Licence type is required for licence row ${i + 1}.`);
        return false;
      }

      if (!this.hasText(licence.licenceNumber)) {
        this.fail(`Licence number is required for licence row ${i + 1}.`);
        return false;
      }
    }

    return true;
  }

  private blank(): ContactDetail {
    return {
      contactId: 0,
      contactCode: '',
      displayName: '',
      contactCategory: 'Business',
      isCustomer: true,
      isVendor: false,
      isJobWorker: false,
      isPrescriber: false,
      gstin: null,
      gstRegistrationType: 'Unregistered',
      currencyCode: 'INR',
      creditLimit: null,
      isActive: true,
      isSubLedgerLinked: false,
      email: null,
      mobileNumber: null,
      city: null,
      legalName: null,
      pan: null,
      tan: null,
      placeOfSupplyStateId: null,
      countryId: this.defaultCountryId,
      paymentTermId: this.terms().find((t) => t.isDefault)?.paymentTermId ?? null,
      maxOutstandingDays: null,
      maxDiscountPercent: null,
      isTdsApplicable: false,
      tdsSection: null,
      isMsme: false,
      udyamNumber: null,
      notes: null,
      addresses: [],
      persons: [],
      bankDetails: [],
      licences: [],
      attachments: [],
    };
  }

  private succeed(text: string): void {
    this.message.set(text);
    this.messageIsError.set(false);
  }

  private fail(text: string): void {
    this.message.set(text);
    this.messageIsError.set(true);
  }

  private hasText(value: string | null | undefined): boolean {
    return (value ?? '').trim().length > 0;
  }

  private cleanOptional(value: string | null | undefined): string | null {
    const cleaned = (value ?? '').trim();
    return cleaned.length > 0 ? cleaned : null;
  }

  private messageOf(err: unknown, fallback: string): string {
    const anyErr = err as { error?: { message?: string } };
    return anyErr?.error?.message ?? fallback;
  }

  private get<T>(url: string): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.get<T>(url).subscribe({ next: resolve, error: reject }),
    );
  }

  private send<T = unknown>(
    method: 'POST' | 'PUT' | 'PATCH' | 'DELETE',
    url: string,
    body: unknown,
  ): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http
        .request<T>(method, url, { body })
        .subscribe({ next: resolve as (value: T) => void, error: reject }),
    );
  }

  openFilter: string | null = null;
  filterOp: string = 'contains';
  filterVal: string = '';

  toggleFilter(col: string, event: Event) {
    event.stopPropagation();
    this.openFilter = this.openFilter === col ? null : col;
  }

  roleLabels(row: ContactListItem): string {
    const roles: string[] = [];
    if (row.isCustomer) roles.push('Customer');
    if (row.isVendor) roles.push('Vendor');
    if (row.isJobWorker) roles.push('Job worker');
    if (row.isPrescriber) roles.push('Prescriber');
    return roles.join(' & ') || '—';
  }

}


