import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { SessionContextService, normaliseBusinessType } from '@bill-book/auth';
import {
  BbFileSelection,
  BbSelectOption,
  BranchDateInputComponent,
  CheckboxComponent,
  ColumnDef,
  CustomFieldDefinition,
  CustomFieldValues,
  CustomFieldsComponent,
  DataGridCellTemplateDirective,
  DataGridComponent,
  EditHistoryComponent,
  EmailInputComponent,
  FileInputComponent,
  MessageBoxComponent,
  MoneyInputComponent,
  NumberInputComponent,
  PercentageInputComponent,
  PhoneInputComponent,
  RichTextInputComponent,
  SelectComponent,
  TextInputComponent,
  compactValues,
  defaultValues,
  isEmptyValue,
} from '@bill-book/ui-components';
import {
  ContactPersonRole,
  ContactPersonRolesDialog,
  RoleSavedEvent,
} from '../contact-person-roles-dialog/contact-person-roles.dialog';
import {
  ADDRESS_KINDS,
  AddressCard,
  ContactAddressDto,
  ContactPersonDto,
  PersonRow,
  FieldError,
  blankAddress,
  blankPerson,
  chooseDefaultPerson,
  isBlankPerson,
  rowErrors,
  toFormRows,
  toPayloadRows,
} from './contact-form.model';

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
}

/** The contact as the API returns it. */
interface ContactDetail {
  contactId: number;
  contactCode: string;
  displayName: string;
  contactCategory: string;
  isCustomer: boolean;
  isVendor: boolean;
  isJobWorker: boolean;
  isPrescriber: boolean;
  isGuardian: boolean;
  gstin: string | null;
  gstRegistrationType: string;
  currencyCode: string;
  creditLimit: number | null;
  isActive: boolean;
  isDraft: boolean;
  isSubLedgerLinked: boolean;
  legalName: string | null;
  pan: string | null;
  tan: string | null;
  placeOfSupplyStateId: number | null;
  countryId: number | null;
  paymentTermId: number | null;
  maxOutstandingDays: number | null;
  maxDiscountPercent: number | null;
  receivableAccountId: number | null;
  payableAccountId: number | null;
  isTdsApplicable: boolean;
  tdsSection: string | null;
  lowerDeductionCertificateNo: string | null;
  lowerDeductionRate: number | null;
  lowerDeductionValidFrom: string | null;
  lowerDeductionValidTo: string | null;
  lowerDeductionLimit: number | null;
  isMsme: boolean;
  udyamNumber: string | null;
  notes: string | null;
  customFields: CustomFieldValues | null;
  addresses: ContactAddressDto[] | null;
  persons: ContactPersonDto[] | null;
  bankDetails: BankDetailModel[] | null;
  licences: LicenceModel[] | null;
  attachments: AttachmentModel[] | null;
}

/** The scalar half of the form; addresses and people are rows of their own. */
type ContactFields = Omit<ContactDetail, 'addresses' | 'persons' | 'bankDetails' | 'licences' | 'attachments' | 'customFields'> & {
  customFields: CustomFieldValues;
};

interface PortalLinkMade {
  code: string;
  url: string | null;
  expiresAt: string;
}

interface PortalAccessState {
  liveLinks: number;
  lastUsedAt: string | null;
  expiresAt: string | null;
}

interface Named {
  id: number;
  label: string;
}

/** Mirrors Master.Entity.Enums.LicenceType. */
const LICENCE_TYPES: readonly { value: string; label: string }[] = [
  { value: 'DrugLicence', label: 'Drug licence' },
  { value: 'Fssai', label: 'FSSAI' },
  { value: 'Bis', label: 'BIS' },
  { value: 'MedicalRegistration', label: 'Medical registration' },
  { value: 'Other', label: 'Other' },
];

/** Mirrors Master.Entity.Enums.AttachmentDocumentType. */
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
 * A contact's own page (TK-188), at `/contacts/new` and `/contacts/:id`: the
 * owner's "New customer" design, five sections in one column, with Save draft
 * beside Save.
 *
 * **A draft** keeps a display name and a role and nothing else is checked; it
 * gets no sub-ledger and no document can pick it until it is saved. A saved
 * contact never goes back to draft, so Save draft is offered only while the
 * contact is new or still a draft.
 *
 * **Every address carries its own contact person**, the one to ask for there.
 * A person on a card added in this save has no address id to point at yet, so
 * it is sent with the card's position instead (`contact-form.model.ts`).
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-contact-form-page',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    BranchDateInputComponent,
    CheckboxComponent,
    ContactPersonRolesDialog,
    CustomFieldsComponent,
    DataGridCellTemplateDirective,
    DataGridComponent,
    EditHistoryComponent,
    EmailInputComponent,
    FileInputComponent,
    MessageBoxComponent,
    MoneyInputComponent,
    NumberInputComponent,
    PercentageInputComponent,
    PhoneInputComponent,
    RichTextInputComponent,
    SelectComponent,
    TextInputComponent,
  ],
  templateUrl: './contact-form.page.html',
  styleUrl: './contact-form.page.scss',
})
export class ContactFormPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly session = inject(SessionContextService);

  /** India until a branch's own country reaches the client. */
  private readonly defaultCountryId = 1;

  protected readonly contactId = signal<number | null>(null);
  protected readonly loaded = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly rolesOpen = signal(false);
  protected readonly rolesDialogMode = signal<'add' | 'edit' | 'manage'>('add');
  protected roleTargetPerson: PersonRow | null = null;
  protected readonly uploading = signal(false);
  protected readonly portalLink = signal<PortalLinkMade | null>(null);
  protected readonly portalAccess = signal<PortalAccessState | null>(null);

  /** Each field's problem, by the field's id; shown under it until it is edited. */
  protected readonly errors = signal<Record<string, string>>({});

  protected readonly roles = signal<ContactPersonRole[]>([]);
  protected readonly customFieldDefs = signal<CustomFieldDefinition[]>([]);
  protected readonly states = signal<{ stateId: number; stateCode: string; stateName: string }[]>([]);
  protected readonly countries = signal<Named[]>([]);
  protected readonly currencies = signal<string[]>([]);
  protected readonly terms = signal<{ paymentTermId: number; termName: string; isDefault: boolean }[]>([]);
  protected readonly accounts = signal<{ accountId: number; accountTypeId: number; accountCode: string; accountName: string; isActive: boolean }[]>([]);
  protected readonly tdsSectionOptions = signal<BbSelectOption<string>[]>([]);

  /** The form. Plain fields, bound with ngModel; the page marks itself on every change it makes. */
  form: ContactFields = this.blank();
  addresses: AddressCard[] = [];
  persons: PersonRow[] = [];
  bankDetails: BankDetailModel[] = [];
  licences: LicenceModel[] = [];
  attachments: AttachmentModel[] = [];

  /** Whether the contact on screen is stored as a draft; a new one is neither. */
  protected readonly storedAsDraft = signal(false);

  uploadDocumentType = 'Other';
  uploadDescription: string | null = null;
  uploadExpiryDate: string | null = null;

  protected readonly addressKinds = ADDRESS_KINDS;
  protected readonly licenceTypes = LICENCE_TYPES;
  protected readonly documentTypes = DOCUMENT_TYPES;

  protected readonly categoryOptions: BbSelectOption<string>[] = [
    { value: 'Business', label: 'Business' },
    { value: 'Individual', label: 'Individual' },
  ];

  protected readonly gstRegistrationOptions: BbSelectOption<string>[] = [
    { value: 'Regular', label: 'Regular' },
    { value: 'Composition', label: 'Composition' },
    { value: 'Unregistered', label: 'Unregistered' },
    { value: 'Sez', label: 'SEZ' },
    { value: 'Overseas', label: 'Overseas' },
    { value: 'Consumer', label: 'Consumer' },
  ];

  protected readonly bankAccountKindOptions: BbSelectOption<string>[] = [
    { value: 'Savings', label: 'Savings' },
    { value: 'Current', label: 'Current' },
    { value: 'Other', label: 'Other' },
  ];

  protected readonly bankOptions: BbSelectOption<string>[] = [
    { value: 'State Bank of India', label: 'State Bank of India (SBI)' },
    { value: 'HDFC Bank', label: 'HDFC Bank' },
    { value: 'ICICI Bank', label: 'ICICI Bank' },
    { value: 'Axis Bank', label: 'Axis Bank' },
    { value: 'Kotak Mahindra Bank', label: 'Kotak Mahindra Bank' },
    { value: 'Punjab National Bank', label: 'Punjab National Bank (PNB)' },
    { value: 'Bank of Baroda', label: 'Bank of Baroda' },
    { value: 'Canara Bank', label: 'Canara Bank' },
    { value: 'Union Bank of India', label: 'Union Bank of India' },
    { value: 'IndusInd Bank', label: 'IndusInd Bank' },
    { value: 'Yes Bank', label: 'Yes Bank' },
    { value: 'IDFC FIRST Bank', label: 'IDFC FIRST Bank' },
    { value: 'Federal Bank', label: 'Federal Bank' },
    { value: 'Indian Bank', label: 'Indian Bank' },
    { value: 'Central Bank of India', label: 'Central Bank of India' },
    { value: 'Other Bank', label: 'Other Bank' },
  ];

  protected bankSelectOptions(currentBank?: string): BbSelectOption<string>[] {
    if (!currentBank) return this.bankOptions;
    const exists = this.bankOptions.some((b) => b.value.toLowerCase() === currentBank.toLowerCase());
    if (!exists && currentBank.trim().length > 0) {
      return [{ value: currentBank, label: currentBank }, ...this.bankOptions];
    }
    return this.bankOptions;
  }

  protected readonly documentColumns: ColumnDef[] = [
    { field: 'file', header: 'File' },
    { field: 'type', header: 'Type' },
    { field: 'size', header: 'Size' },
    { field: 'expires', header: 'Expires' },
    { field: 'actions', header: '' },
  ];

  protected readonly stateOptions = computed<BbSelectOption<number>[]>(() =>
    this.states().map((state) => ({ value: state.stateId, label: state.stateName })),
  );

  protected readonly countryOptions = computed<BbSelectOption<number>[]>(() =>
    this.countries().map((country) => ({ value: country.id, label: country.label })),
  );

  protected readonly currencyOptions = computed<BbSelectOption<string>[]>(() => {
    const codes = new Set(this.currencies());
    // The contact's own currency stays choosable even if the branch has since
    // switched it off.
    if (this.form.currencyCode) codes.add(this.form.currencyCode);
    return [...codes].map((code) => ({ value: code, label: code }));
  });

  protected readonly termOptions = computed<BbSelectOption<number>[]>(() =>
    this.terms().map((term) => ({ value: term.paymentTermId, label: term.termName })),
  );

  protected readonly roleOptions = computed<BbSelectOption<number>[]>(() =>
    this.roles().map((role) => ({ value: role.contactPersonRoleId, label: role.roleName })),
  );

  protected readonly addressLinkOptions = computed<BbSelectOption<string>[]>(() => [
    { value: '', label: 'All / General' },
    ...this.addresses.map((card, i) => ({
      value: card.key,
      label: `Address ${i + 1} (${card.city || card.addressLine1 || card.kind})`,
    })),
  ]);

  /** Receivables are assets (type 1) and payables liabilities (type 2), ids fixed by mst.AccountTypes. */
  protected readonly receivableOptions = computed<BbSelectOption<number>[]>(() => this.accountOptions(1));
  protected readonly payableOptions = computed<BbSelectOption<number>[]>(() => this.accountOptions(2));

  /** "New customer", "New vendor", or the contact's name. */
  protected readonly heading = signal('New contact');

  protected isSchool(): boolean {
    return normaliseBusinessType(this.session.context()?.businessType) === 'School';
  }

  protected get isNew(): boolean {
    return this.contactId() === null;
  }

  /** Save draft is for a contact not yet saved: a saved one never returns to draft. */
  protected get canSaveDraft(): boolean {
    return this.isNew || this.storedAsDraft();
  }

  protected get gstinAllowed(): boolean {
    return ['Regular', 'Composition', 'Sez'].includes(this.form.gstRegistrationType);
  }

  ngOnInit(): void {
    // A notice carried from the page that saved a new draft: /contacts/new
    // and /contacts/:id are two routes, so the page is a new instance.
    const carried = (history.state as { notice?: string } | null)?.notice;
    if (carried) this.notice.set(carried);

    this.route.paramMap.subscribe((params) => {
      const rawId = params.get('id');
      const id = rawId ? Number(rawId) : NaN;
      void this.loadLookups().then(() => {
        if (Number.isFinite(id) && id > 0) {
          void this.open(id);
        } else {
          this.startNew();
        }
      });
    });
  }

  /** The card's "Contact person": the first person linked to it. */
  protected personFor(card: AddressCard): PersonRow | undefined {
    return this.persons.find((row) => row.addressKey === card.key);
  }

  /** People who belong to the contact as a whole, or to a card beyond its first person. */
  protected get otherPersons(): PersonRow[] {
    const firstOnCard = new Set(
      this.addresses
        .map((card) => this.persons.find((row) => row.addressKey === card.key)?.key)
        .filter((key): key is string => key !== undefined),
    );
    return this.persons.filter((row) => !firstOnCard.has(row.key));
  }

  addAddress(): void {
    const card = blankAddress(this.form.countryId ?? this.defaultCountryId, this.addresses.length === 0);
    this.addresses = [...this.addresses, card];
  }

  removeAddress(card: AddressCard): void {
    this.addresses = this.addresses.filter((row) => row !== card);
    // Unlink any person that was linked to this address card
    this.persons = this.persons.map((row) => (row.addressKey === card.key ? { ...row, addressKey: null } : row));
  }

  /** One default billing and one default shipping address: ticking one clears the rest. */
  setDefault(card: AddressCard, flag: 'isDefaultBilling' | 'isDefaultShipping', value: boolean): void {
    this.addresses = this.addresses.map((row) =>
      row === card ? { ...row, [flag]: value } : value ? { ...row, [flag]: false } : row,
    );
  }

  /** Writes into the card in place: ngModel already holds the object. */
  protected cardChanged(): void {
    this.addresses = [...this.addresses];
  }

  addPerson(): void {
    this.persons = [...this.persons, blankPerson(this.defaultRoleId(), null)];
  }

  removePerson(person: PersonRow): void {
    this.persons = this.persons.filter((row) => row !== person);
    if (this.persons.length === 0) {
      this.persons = [blankPerson(this.defaultRoleId(), null)];
    }
  }

  /**
   * Whether this person is the main contact: the one ticked, else the one the
   * save would choose (the person at the default billing address, else the first).
   */
  protected isMain(person: PersonRow): boolean {
    return chooseDefaultPerson(this.addresses, this.persons.filter((row) => !isBlankPerson(row))) === person.key;
  }

  /** The main contact: invoices and reminders go to their email or phone. */
  setMainPerson(person: PersonRow): void {
    this.persons = this.persons.map((row) => ({ ...row, isDefault: row === person }));
  }

  onRegistrationChange(): void {
    if (!this.gstinAllowed) {
      this.form.gstin = null;
    }
  }

  addBankDetail(): void {
    this.bankDetails = [
      ...this.bankDetails,
      {
        contactBankDetailId: 0,
        accountHolderName: this.form.legalName ?? this.form.displayName,
        bankName: '',
        accountNumber: '',
        ifsc: null,
        branchName: null,
        accountKind: 'Current',
        upiId: null,
        isDefault: !this.bankDetails.some((bank) => bank.isDefault),
        isActive: true,
      },
    ];
  }

  removeBankDetail(index: number): void {
    const removed = this.bankDetails[index];
    const remaining = this.bankDetails.filter((_, i) => i !== index);
    if (removed.isDefault && remaining.length > 0) {
      remaining[0] = { ...remaining[0], isDefault: true };
    }
    this.bankDetails = remaining;
  }

  setDefaultBankDetail(index: number): void {
    this.bankDetails = this.bankDetails.map((bank, i) => ({ ...bank, isDefault: i === index }));
  }

  addLicence(): void {
    this.licences = [
      ...this.licences,
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
    this.licences = this.licences.filter((_, i) => i !== index);
  }

  protected licenceStatus(licence: LicenceModel): 'none' | 'expired' | 'soon' | 'valid' {
    if (!licence.expiresOn) {
      return 'none';
    }
    const days = Math.floor(
      (Date.parse(licence.expiresOn) - Date.parse(new Date().toISOString().slice(0, 10))) / 86_400_000,
    );
    return days < 0 ? 'expired' : days <= 30 ? 'soon' : 'valid';
  }

  /** Save, or Save draft. A draft is checked only for a name and a role. */
  async save(draft: boolean): Promise<void> {
    this.error.set(null);
    this.notice.set(null);

    if (!this.validate(draft)) {
      return;
    }

    const rows = toPayloadRows(this.addresses, this.persons);
    const payload = {
      ...this.form,
      contactCode: this.clean(this.form.contactCode),
      displayName: this.form.displayName.trim(),
      legalName: this.clean(this.form.legalName),
      gstin: this.gstinAllowed ? (this.clean(this.form.gstin)?.toUpperCase() ?? null) : null,
      pan: this.clean(this.form.pan)?.toUpperCase() ?? null,
      tan: this.clean(this.form.tan)?.toUpperCase() ?? null,
      currencyCode: (this.form.currencyCode || 'INR').trim().toUpperCase(),
      tdsSection: this.form.isTdsApplicable ? this.clean(this.form.tdsSection) : null,
      lowerDeductionCertificateNo: this.form.isTdsApplicable ? this.clean(this.form.lowerDeductionCertificateNo) : null,
      lowerDeductionRate: this.form.isTdsApplicable ? (this.form.lowerDeductionRate ?? null) : null,
      lowerDeductionValidFrom: this.form.isTdsApplicable ? this.form.lowerDeductionValidFrom || null : null,
      lowerDeductionValidTo: this.form.isTdsApplicable ? this.form.lowerDeductionValidTo || null : null,
      lowerDeductionLimit: this.form.isTdsApplicable ? (this.form.lowerDeductionLimit ?? null) : null,
      udyamNumber: this.form.isMsme ? this.clean(this.form.udyamNumber) : null,
      notes: this.clean(this.form.notes),
      customFields: compactValues(this.form.customFields ?? {}),
      isDraft: draft,
      addresses: rows.addresses,
      persons: rows.persons,
      bankDetails: this.bankDetails
        .filter(
          (bank) =>
            this.hasText(bank.accountHolderName) ||
            this.hasText(bank.bankName) ||
            this.hasText(bank.accountNumber) ||
            this.hasText(bank.ifsc) ||
            this.hasText(bank.branchName) ||
            this.hasText(bank.upiId),
        )
        .map((bank) => ({
          ...bank,
          accountHolderName: bank.accountHolderName.trim() || this.form.displayName.trim(),
          bankName: bank.bankName.trim(),
          accountNumber: bank.accountNumber.trim(),
          ifsc: this.clean(bank.ifsc)?.toUpperCase() ?? null,
          branchName: this.clean(bank.branchName),
          upiId: this.clean(bank.upiId),
        })),
      licences: this.licences.map((licence) => ({
        ...licence,
        licenceNumber: licence.licenceNumber.trim(),
        description: this.clean(licence.description),
        issuingAuthority: this.clean(licence.issuingAuthority),
      })),
    };

    this.busy.set(true);
    try {
      const id = this.contactId();
      let savedId = id;
      if (id === null) {
        const result = await this.send<{ contactId: number }>('POST', '/api/contacts', payload);
        savedId = result.contactId;
      } else {
        await this.send('PUT', `/api/contacts/${id}`, payload);
      }

      // Opened from a document's "new contact" link: hand the new id back.
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
      const returnMaster = this.route.snapshot.queryParamMap.get('returnMaster');
      if (id === null && !draft && returnUrl && returnMaster && savedId) {
        void this.router.navigateByUrl(`${returnUrl}?returnMaster=${returnMaster}&returnId=${savedId}`);
        return;
      }

      if (draft) {
        // Stay on the draft: it is somewhere to come back to, not a finished record.
        const notice = 'Draft saved. It stays out of every document until you save it.';
        if (savedId) {
          await this.open(savedId);
          this.notice.set(notice);
          if (id === null) {
            void this.router.navigate(['/contacts', savedId], { replaceUrl: true, state: { notice } });
          }
          return;
        }
        return;
      }

      void this.router.navigate(['/contacts']);
    } catch (err: unknown) {
      this.handleApiError(err);
    } finally {
      this.busy.set(false);
    }
  }

  protected clearError(field: string): void {
    if (this.errors()[field]) {
      this.errors.update((all) => {
        const next = { ...all };
        delete next[field];
        return next;
      });
    }
  }

  /**
   * Scrolls to a field and puts the cursor in it, opening the folded section
   * it sits in first. A `bb-*` input carries its id on the host and on the
   * control inside it, so the first focusable element under the match is used.
   */
  protected focusField(fieldId: string): void {
    setTimeout(() => {
      const el = document.getElementById(fieldId);
      if (!el) return;
      const folded = el.closest('details');
      if (folded && !folded.open) folded.open = true;
      const target = el.matches('input, select, textarea, button, [contenteditable]')
        ? el
        : (el.querySelector<HTMLElement>('input, select, textarea, button, [contenteditable], [tabindex]') ?? el);
      target.focus?.();
      el.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }, 50);
  }

  /**
   * The server's refusal, put where it belongs: field errors from model
   * validation by their field, and a rule's sentence on the field it is about,
   * so the cursor lands on what to fix.
   */
  private handleApiError(err: unknown): void {
    const body = (err as { error?: { message?: string; errors?: Record<string, string[]> } })?.error;
    const message = body?.message ?? this.messageOf(err, 'Could not save this contact.');
    this.error.set(message);

    const found: Record<string, string> = {};
    for (const [key, messages] of Object.entries(body?.errors ?? {})) {
      if (messages?.length) found[key.charAt(0).toLowerCase() + key.slice(1)] = messages[0];
    }

    const lower = message.toLowerCase();
    const field = lower.includes('code') ? 'contactCode'
      : lower.includes('gstin') ? 'gstin'
        : lower.includes('udyam') ? 'udyamNumber'
          : lower.includes('tds') || lower.includes('certificate') ? 'tdsSection'
            : lower.includes('display name') ? 'displayName'
              : lower.includes('role') && !lower.includes('person') ? 'roles'
                : Object.keys(found)[0];
    if (field) {
      if (!found[field]) found[field] = message;
      this.focusField(field);
    }
    this.errors.set(found);
  }

  cancel(): void {
    void this.router.navigate(['/contacts']);
  }

  /** Creates the sub-accounts for a saved contact whose call to Accounting failed. */
  async linkSubLedger(): Promise<void> {
    const id = this.contactId();
    if (id === null) return;
    this.busy.set(true);
    try {
      await this.send('POST', `/api/contacts/${id}/link-sub-ledger`, {});
      this.form.isSubLedgerLinked = true;
      this.notice.set('Sub-accounts created.');
    } catch (err: unknown) {
      this.error.set(this.messageOf(err, 'Could not create the sub-accounts.'));
    } finally {
      this.busy.set(false);
    }
  }

  async makePortalLink(): Promise<void> {
    const id = this.contactId();
    if (id === null) return;
    this.busy.set(true);
    try {
      const link = await this.send<PortalLinkMade>('POST', `/api/contacts/${id}/portal-link`, {});
      this.portalLink.set(link);
      await this.loadPortalAccess(id);
    } catch (err: unknown) {
      this.error.set(this.messageOf(err, 'Could not make a portal link.'));
    } finally {
      this.busy.set(false);
    }
  }

  async revokePortalAccess(): Promise<void> {
    const id = this.contactId();
    if (id === null) return;
    this.busy.set(true);
    try {
      await this.send('POST', `/api/contacts/${id}/portal-access/revoke`, {});
      this.portalLink.set(null);
      this.notice.set('Portal access revoked. Any open session ends within the hour.');
      await this.loadPortalAccess(id);
    } catch (err: unknown) {
      this.error.set(this.messageOf(err, 'Could not revoke portal access.'));
    } finally {
      this.busy.set(false);
    }
  }

  /** Uploads at once: a file cannot ride in the contact's JSON body. */
  async uploadFile(selection: BbFileSelection): Promise<void> {
    const file = selection.files[0];
    const id = this.contactId();
    if (!file || id === null) return;

    const body = new FormData();
    body.append('file', file);
    body.append('documentType', this.uploadDocumentType);
    if (this.uploadDescription) body.append('description', this.uploadDescription);
    if (this.uploadExpiryDate) body.append('expiryDate', this.uploadExpiryDate);

    this.uploading.set(true);
    try {
      await this.send('POST', `/api/contacts/${id}/attachments`, body);
      this.attachments = await this.get<AttachmentModel[]>(`/api/contacts/${id}/attachments`);
      this.uploadDescription = null;
      this.uploadExpiryDate = null;
      this.notice.set('File uploaded.');
    } catch (err: unknown) {
      this.error.set(this.messageOf(err, 'Could not upload that file.'));
    } finally {
      this.uploading.set(false);
    }
  }

  async downloadFile(attachment: AttachmentModel): Promise<void> {
    try {
      const blob = await new Promise<Blob>((resolve, reject) =>
        this.http
          .get(`/api/contacts/attachments/${attachment.contactAttachmentId}/download`, { responseType: 'blob' })
          .subscribe({ next: resolve, error: reject }),
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = attachment.fileName;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch {
      this.error.set('Could not download that file.');
    }
  }

  async deleteFile(attachment: AttachmentModel): Promise<void> {
    try {
      await this.send('DELETE', `/api/contacts/attachments/${attachment.contactAttachmentId}`, {});
      this.attachments = this.attachments.filter((a) => a.contactAttachmentId !== attachment.contactAttachmentId);
      this.notice.set('File removed.');
    } catch (err: unknown) {
      this.error.set(this.messageOf(err, 'Could not remove that file.'));
    }
  }

  protected formatSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    return bytes < 1_048_576 ? `${Math.round(bytes / 1024)} KB` : `${(bytes / 1_048_576).toFixed(1)} MB`;
  }

  protected labelOf(options: readonly { value: string; label: string }[], value: string): string {
    return options.find((option) => option.value === value)?.label ?? value;
  }

  openAddRole(person: PersonRow): void {
    this.roleTargetPerson = person;
    this.rolesDialogMode.set('add');
    this.rolesOpen.set(true);
  }

  openManageRoles(): void {
    this.roleTargetPerson = null;
    this.rolesDialogMode.set('manage');
    this.rolesOpen.set(true);
  }

  async onRoleSaved(event: RoleSavedEvent): Promise<void> {
    try {
      const refreshedRoles = await this.get<ContactPersonRole[]>('/api/contact-person-roles');
      this.roles.set(refreshedRoles);

      if (this.roleTargetPerson) {
        const match = event.roleId
          ? refreshedRoles.find((r) => r.contactPersonRoleId === event.roleId)
          : refreshedRoles.find((r) => r.roleName.trim().toLowerCase() === event.roleName.trim().toLowerCase());

        if (match) {
          this.roleTargetPerson.contactPersonRoleId = match.contactPersonRoleId;
          this.clearError(`prole_${this.roleTargetPerson.key}`);
        }
      }
    } catch {
      // keep quiet
    } finally {
      this.rolesOpen.set(false);
      this.roleTargetPerson = null;
    }
  }

  onRolesClosed(): void {
    this.rolesOpen.set(false);
    this.roleTargetPerson = null;
    void this.get<ContactPersonRole[]>('/api/contact-person-roles')
      .then((roles) => this.roles.set(roles))
      .catch(() => undefined);
  }

  private startNew(): void {
    const role = this.route.snapshot.queryParamMap.get('role')?.toLowerCase();
    if (!role) {
      // The top bar's New menu opens /contacts/new with no role. Name the role
      // in the address, so the breadcrumb reads "New customer" like the page.
      void this.router.navigate([], {
        relativeTo: this.route,
        queryParams: { role: 'customer' },
        queryParamsHandling: 'merge',
        replaceUrl: true,
      });
    }
    this.form = this.blank();
    this.form.isCustomer = role !== 'vendor' && role !== 'jobworker' && role !== 'prescriber' && role !== 'guardian';
    this.form.isVendor = role === 'vendor';
    this.form.isJobWorker = role === 'jobworker';
    this.form.isPrescriber = role === 'prescriber';
    this.form.isGuardian = role === 'guardian';
    this.heading.set(
      role === 'vendor' ? 'New vendor'
        : role === 'jobworker' ? 'New job worker'
          : role === 'prescriber' ? 'New prescriber'
            : role === 'guardian' ? 'New guardian'
              : 'New customer',
    );
    this.addresses = [blankAddress(this.form.countryId ?? this.defaultCountryId, true)];
    this.persons = [blankPerson(this.defaultRoleId(), null)];
    this.bankDetails = [];
    this.licences = [];
    this.attachments = [];
    this.storedAsDraft.set(false);
    this.contactId.set(null);
    this.errors.set({});
    this.loaded.set(true);
    this.focusField('displayName');
  }

  private async open(id: number): Promise<void> {
    this.busy.set(true);
    try {
      const detail = await this.get<ContactDetail>(`/api/contacts/${id}`);
      const { addresses, persons, bankDetails, licences, attachments, ...fields } = detail;
      this.form = { ...fields, customFields: fields.customFields ?? {} };
      const rows = toFormRows(addresses ?? [], persons ?? []);
      if (rows.addresses.length === 0) {
        // Somewhere to start typing, as on a new contact; left blank, it is not sent.
        rows.addresses.push(blankAddress(fields.countryId ?? this.defaultCountryId, true));
      }
      this.addresses = rows.addresses;
      if (rows.persons.length === 0) {
        rows.persons.push(blankPerson(this.defaultRoleId(), null));
      }
      this.persons = rows.persons;
      this.bankDetails = bankDetails ?? [];
      this.licences = licences ?? [];
      this.attachments = attachments ?? [];
      this.storedAsDraft.set(detail.isDraft);
      this.contactId.set(id);
      this.heading.set(detail.displayName);
      this.loaded.set(true);
      void this.loadPortalAccess(id);
    } catch {
      this.error.set('Could not open that contact.');
    } finally {
      this.busy.set(false);
    }
  }

  private async loadLookups(): Promise<void> {
    // Each is optional to the form: a failed one leaves its picker empty, and
    // the server checks the value regardless.
    const orgId = localStorage.getItem('bb.orgId') ?? '';
    await Promise.all([
      this.get<ContactPersonRole[]>('/api/contact-person-roles').then((r) => this.roles.set(r)).catch(() => undefined),
      this.get<CustomFieldDefinition[]>('/api/contacts/custom-fields').then((d) => this.customFieldDefs.set(d)).catch(() => undefined),
      this.get<{ paymentTermId: number; termName: string; isDefault: boolean }[]>('/api/payment-terms').then((t) => this.terms.set(t)).catch(() => undefined),
      this.get<{ stateId: number; stateCode: string; stateName: string }[]>(`/api/master/countries/${this.defaultCountryId}/states`)
        .then((s) => this.states.set(s))
        .catch(() => undefined),
    ]);
    await Promise.all([
      this.get<{ countryId: number; countryName: string }[]>('/api/master/countries')
        .then((c) => this.countries.set(c.map((row) => ({ id: row.countryId, label: row.countryName }))))
        .catch(() => undefined),
      orgId
        ? this.get<{ code: string; isActive?: boolean }[]>(`/api/organizations/${orgId}/currencies`)
          .then((c) => this.currencies.set(c.filter((row) => row.isActive !== false).map((row) => row.code)))
          .catch(() => undefined)
        : Promise.resolve(),
      this.get<{ accountId: number; accountTypeId: number; accountCode: string; accountName: string; isActive: boolean }[]>('/api/accounts')
        .then((a) => this.accounts.set(a))
        .catch(() => undefined),
      this.get<{ sectionCode: string; natureOfPayment: string; isActive: boolean }[]>('/api/tds/sections')
        .then((sections) => {
          const seen = new Set<string>();
          this.tdsSectionOptions.set(
            sections
              .filter((s) => s.isActive && !seen.has(s.sectionCode) && seen.add(s.sectionCode))
              .map((s) => ({ value: s.sectionCode, label: `${s.sectionCode} · ${s.natureOfPayment}` })),
          );
        })
        .catch(() => undefined),
    ]);
  }

  private async loadPortalAccess(id: number): Promise<void> {
    try {
      const state = await this.get<PortalAccessState>(`/api/contacts/${id}/portal-access`);
      if (this.contactId() === id) this.portalAccess.set(state);
    } catch {
      // The contact still opens; the access line is left off.
    }
  }

  /**
   * Every problem with this save, each under its field; the first is shown at
   * the top and focused. A draft needs only a name and a role, plus rows that
   * are not half-filled; a save needs everything the server checks.
   */
  private validate(draft: boolean): boolean {
    const f = this.form;
    const found: FieldError[] = [];
    const add = (field: string, message: string) => found.push({ field, message });

    if (!this.hasText(f.displayName)) add('displayName', 'Enter a display name.');
    if (!f.isCustomer && !f.isVendor && !f.isJobWorker && !f.isPrescriber && !f.isGuardian) {
      add('roles', 'Tick at least one role: customer, vendor, job worker, prescriber or guardian.');
    }

    if (!draft) {
      const gstin = (f.gstin ?? '').trim().toUpperCase();
      if (this.gstinAllowed) {
        if (!gstin) {
          add('gstin', 'Enter the GSTIN, or change the GST registration.');
        } else if (gstin.length !== 15) {
          add('gstin', 'A GSTIN is 15 characters.');
        } else {
          const state = this.states().find((row) => row.stateId === f.placeOfSupplyStateId);
          if (state && !gstin.startsWith(state.stateCode)) {
            add('gstin', "The GSTIN's first two digits do not match the place of supply.");
          }
        }
      }
      if (this.hasText(f.pan) && f.pan!.trim().length !== 10) add('pan', 'A PAN is 10 characters.');
      if ((f.currencyCode ?? '').trim().length !== 3) add('currencyCode', 'Choose the currency.');
      if (f.isTdsApplicable && !this.hasText(f.tdsSection)) add('tdsSection', 'Choose the TDS section, or turn TDS off.');
      const certificate = [
        this.hasText(f.lowerDeductionCertificateNo),
        f.lowerDeductionRate != null,
        this.hasText(f.lowerDeductionValidFrom),
        this.hasText(f.lowerDeductionValidTo),
        f.lowerDeductionLimit != null,
      ];
      if (f.isTdsApplicable && certificate.some((x) => x) && !certificate.every((x) => x)) {
        add('lowerDeductionCertificateNo', 'A lower-deduction certificate needs its number, rate, limit and validity dates.');
      } else if (f.isTdsApplicable && f.lowerDeductionValidFrom && f.lowerDeductionValidTo
        && f.lowerDeductionValidTo < f.lowerDeductionValidFrom) {
        add('lowerDeductionValidTo', 'The certificate must end on or after the day it starts.');
      }
      if (f.isMsme && !this.hasText(f.udyamNumber)) add('udyamNumber', 'Enter the Udyam number, or untick Registered MSME.');

      // Required custom fields (TK-111); a draft is let off them, as on the server.
      for (const def of this.customFieldDefs()) {
        if (def.isActive && def.isRequired && isEmptyValue(f.customFields?.[def.key])) {
          add('customFields', `${def.label} is required.`);
        }
      }
    }

    found.push(...rowErrors(this.addresses, this.persons, draft));

    this.bankDetails.forEach((bank, i) => {
      const hasAny =
        this.hasText(bank.accountHolderName) ||
        this.hasText(bank.bankName) ||
        this.hasText(bank.accountNumber) ||
        this.hasText(bank.ifsc) ||
        this.hasText(bank.branchName) ||
        this.hasText(bank.upiId);
      if (hasAny) {
        if (!this.hasText(bank.accountHolderName) && !this.hasText(this.form.displayName)) {
          add(`bankHolder_${i}`, `Payout account ${i + 1} needs the holder's name.`);
        }
        if (!this.hasText(bank.bankName)) add(`bankName_${i}`, `Payout account ${i + 1} needs the bank.`);
        if (!this.hasText(bank.accountNumber)) add(`bankAccount_${i}`, `Payout account ${i + 1} needs the account number.`);
      }
    });

    this.licences.forEach((licence, i) => {
      if (!this.hasText(licence.licenceNumber)) add(`licNo_${i}`, `Licence ${i + 1} needs its number.`);
    });

    const byField: Record<string, string> = {};
    for (const problem of found) byField[problem.field] ??= problem.message;
    this.errors.set(byField);

    const first = found[0];
    if (first) {
      this.error.set(first.message);
      this.focusField(first.field);
      return false;
    }
    return true;
  }

  private accountOptions(typeId: number): BbSelectOption<number>[] {
    return this.accounts()
      .filter((account) => account.accountTypeId === typeId && account.isActive)
      .map((account) => ({ value: account.accountId, label: `${account.accountCode} ${account.accountName}` }));
  }

  private defaultRoleId(): number {
    return (this.roles().find((role) => role.isDefault) ?? this.roles()[0])?.contactPersonRoleId ?? 0;
  }

  private blank(): ContactFields {
    return {
      contactId: 0,
      contactCode: '',
      displayName: '',
      contactCategory: 'Business',
      isCustomer: true,
      isVendor: false,
      isJobWorker: false,
      isPrescriber: false,
      isGuardian: false,
      gstin: null,
      gstRegistrationType: 'Unregistered',
      currencyCode: this.currencies()[0] ?? 'INR',
      creditLimit: null,
      isActive: true,
      isDraft: false,
      isSubLedgerLinked: false,
      legalName: null,
      pan: null,
      tan: null,
      placeOfSupplyStateId: null,
      countryId: this.defaultCountryId,
      paymentTermId: this.terms().find((term) => term.isDefault)?.paymentTermId ?? null,
      maxOutstandingDays: null,
      maxDiscountPercent: null,
      receivableAccountId: null,
      payableAccountId: null,
      isTdsApplicable: false,
      tdsSection: null,
      lowerDeductionCertificateNo: null,
      lowerDeductionRate: null,
      lowerDeductionValidFrom: null,
      lowerDeductionValidTo: null,
      lowerDeductionLimit: null,
      isMsme: false,
      udyamNumber: null,
      notes: null,
      customFields: defaultValues(this.customFieldDefs()),
    };
  }

  private hasText(value: string | null | undefined): boolean {
    return (value ?? '').trim().length > 0;
  }

  private clean(value: string | null | undefined): string | null {
    const trimmed = (value ?? '').trim();
    return trimmed.length > 0 ? trimmed : null;
  }

  private messageOf(err: unknown, fallback: string): string {
    return (err as { error?: { message?: string } })?.error?.message ?? fallback;
  }

  private get<T>(url: string): Promise<T> {
    return new Promise((resolve, reject) => this.http.get<T>(url).subscribe({ next: resolve, error: reject }));
  }

  private send<T = unknown>(method: 'POST' | 'PUT' | 'DELETE', url: string, body: unknown): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.request<T>(method, url, { body }).subscribe({ next: resolve as (value: T) => void, error: reject }),
    );
  }
}
