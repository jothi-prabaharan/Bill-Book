import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ContactPersonRole,
  ContactPersonRolesDialog,
} from '../contact-person-roles/contact-person-roles.dialog';

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

type Tab = 'general' | 'addresses' | 'persons';

/**
 * Customers, vendors, job workers and prescribers — one master with role flags,
 * because in Indian SMB books the same party is routinely both a customer and a
 * vendor.
 *
 * The contact saves as a whole: the rules that matter are rules about the set
 * (at least one person, exactly one default, one default address per type), so
 * the set has to arrive together.
 */
@Component({
  selector: 'bb-contacts-page',
  standalone: true,
  imports: [FormsModule, ContactPersonRolesDialog],
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

  search = '';
  roleFilter = '';
  showInactive = false;

  form: ContactDetail = this.blank();

  ngOnInit(): void {
    void this.load();
    void this.loadLookups();
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
      this.form = await this.get<ContactDetail>(`/api/contacts/${row.contactId}`);
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

  onRolesClosed(): void {
    this.rolesOpen.set(false);
    void this.loadLookups();
  }

  async save(): Promise<void> {
    this.busy.set(true);
    try {
      const id = this.editingId();
      if (id === null) {
        await this.send('POST', '/api/contacts', this.form);
      } else {
        await this.send('PUT', `/api/contacts/${id}`, this.form);
      }
      this.editorOpen.set(false);
      this.succeed('Contact saved.');
      await this.load();
    } catch (err: unknown) {
      this.fail(this.messageOf(err, 'Could not save that contact.'));
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
}
