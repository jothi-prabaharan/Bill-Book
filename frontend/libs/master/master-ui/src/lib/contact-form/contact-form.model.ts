/*
 * The contact form's shapes and the pure steps between them and the API
 * (TK-188). Kept apart from the page so they can be tested without a template:
 * the address-to-person link and the draft rules are the parts most easily got
 * wrong, and neither needs a browser to check.
 */

/** Mirrors Master.Entity.Enums.AddressKind. */
export type AddressKind = 'RegisteredOffice' | 'Warehouse' | 'Branch' | 'Other';

export const ADDRESS_KINDS: readonly { value: AddressKind; label: string }[] = [
  { value: 'RegisteredOffice', label: 'Registered office' },
  { value: 'Warehouse', label: 'Warehouse' },
  { value: 'Branch', label: 'Branch' },
  { value: 'Other', label: 'Other' },
];

/** An address as the API sends and takes it. */
export interface ContactAddressDto {
  contactAddressId: number;
  kind: AddressKind;
  isDefaultBilling: boolean;
  isDefaultShipping: boolean;
  label: string | null;
  addressLine1: string;
  addressLine2: string | null;
  landmark: string | null;
  city: string;
  stateId: number | null;
  countryId: number;
  postalCode: string | null;
  gstin: string | null;
  isActive: boolean;
}

/** A contact person as the API sends and takes it. */
export interface ContactPersonDto {
  contactPersonId: number;
  contactPersonRoleId: number;
  roleName?: string | null;
  /** The address this person is the one to ask for at; null for the whole contact. */
  contactAddressId: number | null;
  /** On the way in only: the position in `addresses` of one added in this save. */
  addressIndex?: number | null;
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

/** An address card on the form. `key` ties people to an address that has no id yet. */
export interface AddressCard extends ContactAddressDto {
  key: string;
}

/**
 * A person on the form. `name` is the one box the form shows; `addressKey`
 * names the card they belong to, or null for a person of the whole contact.
 */
export interface PersonRow {
  key: string;
  contactPersonId: number;
  contactPersonRoleId: number;
  addressKey: string | null;
  name: string;
  email: string | null;
  /** Shown as "Phone": a mobile, which is what reminders and OTPs go to. */
  mobileNumber: string | null;
  isDefault: boolean;
  isActive: boolean;
  /** Kept as loaded and sent back unchanged: the form does not show them. */
  salutation: string | null;
  designation: string | null;
  phoneNumber: string | null;
  website: string | null;
}

let sequence = 0;

/** A key unique within the page, for rows that have no id yet. */
export function nextKey(prefix: string): string {
  sequence += 1;
  return `${prefix}${sequence}`;
}

export function blankAddress(countryId: number, first: boolean): AddressCard {
  return {
    key: nextKey('a'),
    contactAddressId: 0,
    kind: 'RegisteredOffice',
    // The first address is where a contact is billed and delivered to until
    // someone says otherwise.
    isDefaultBilling: first,
    isDefaultShipping: first,
    label: null,
    addressLine1: '',
    addressLine2: null,
    landmark: null,
    city: '',
    stateId: null,
    countryId,
    postalCode: null,
    gstin: null,
    isActive: true,
  };
}

export function blankPerson(roleId: number, addressKey: string | null): PersonRow {
  return {
    key: nextKey('p'),
    contactPersonId: 0,
    contactPersonRoleId: roleId,
    addressKey,
    name: '',
    email: null,
    mobileNumber: null,
    isDefault: false,
    isActive: true,
    salutation: null,
    designation: null,
    phoneNumber: null,
    website: null,
  };
}

/** "Kavitha R" → first name "Kavitha", last name "R"; the split CreateQuickAsync uses too. */
export function splitName(name: string): { firstName: string; lastName: string | null } {
  const trimmed = name.trim().replace(/\s+/g, ' ');
  const space = trimmed.indexOf(' ');
  return space < 0
    ? { firstName: trimmed, lastName: null }
    : { firstName: trimmed.slice(0, space), lastName: trimmed.slice(space + 1) };
}

export function joinName(firstName: string | null, lastName: string | null): string {
  return [firstName, lastName].filter((part) => (part ?? '').trim().length > 0).join(' ');
}

/** The loaded addresses and people as form rows, each person tied to its card by key. */
export function toFormRows(
  addresses: readonly ContactAddressDto[],
  persons: readonly ContactPersonDto[],
): { addresses: AddressCard[]; persons: PersonRow[] } {
  const cards = addresses.map((address) => ({ ...address, key: nextKey('a') }));
  const keyById = new Map(cards.map((card) => [card.contactAddressId, card.key]));

  return {
    addresses: cards,
    persons: persons.map((person) => ({
      key: nextKey('p'),
      contactPersonId: person.contactPersonId,
      contactPersonRoleId: person.contactPersonRoleId,
      addressKey: person.contactAddressId != null ? (keyById.get(person.contactAddressId) ?? null) : null,
      name: joinName(person.firstName, person.lastName),
      email: person.email,
      mobileNumber: person.mobileNumber,
      isDefault: person.isDefault,
      isActive: person.isActive,
      salutation: person.salutation,
      designation: person.designation,
      phoneNumber: person.phoneNumber,
      website: person.website,
    })),
  };
}

function hasText(value: string | null | undefined): boolean {
  return (value ?? '').trim().length > 0;
}

function clean(value: string | null | undefined): string | null {
  const trimmed = (value ?? '').trim();
  return trimmed.length > 0 ? trimmed : null;
}

/** A card nobody has typed into: left out of the save rather than refused. */
export function isBlankAddress(card: AddressCard): boolean {
  return (
    card.contactAddressId === 0 &&
    !hasText(card.addressLine1) &&
    !hasText(card.addressLine2) &&
    !hasText(card.city) &&
    !hasText(card.postalCode) &&
    !hasText(card.gstin)
  );
}

/** A person row nobody has typed into: an address's empty "Contact person" block. */
export function isBlankPerson(person: PersonRow): boolean {
  return (
    person.contactPersonId === 0 &&
    !hasText(person.name) &&
    !hasText(person.email) &&
    !hasText(person.mobileNumber)
  );
}

/**
 * The person who is the default: the one marked, else the one at the default
 * billing address, else the first. Contact-level email and phone resolve to
 * that row, so a full save needs exactly one.
 */
export function chooseDefaultPerson(addresses: readonly AddressCard[], persons: readonly PersonRow[]): string | null {
  const active = persons.filter((person) => person.isActive);
  const marked = active.find((person) => person.isDefault);
  if (marked) {
    return marked.key;
  }

  const billingKey = addresses.find((address) => address.isDefaultBilling)?.key;
  const atBilling = billingKey ? active.find((person) => person.addressKey === billingKey) : undefined;
  return (atBilling ?? active[0])?.key ?? null;
}

/**
 * The addresses and people as the API takes them. Blank cards and blank
 * people are dropped; a person on a card that has an id is linked by that id,
 * and one on a card added in this save by its position in the list sent.
 */
export function toPayloadRows(
  addresses: readonly AddressCard[],
  persons: readonly PersonRow[],
): { addresses: ContactAddressDto[]; persons: ContactPersonDto[] } {
  const keptCards = addresses.filter((card) => !isBlankAddress(card));
  const indexByKey = new Map(keptCards.map((card, index) => [card.key, index]));
  const keptPersons = persons.filter((person) => !isBlankPerson(person));
  const defaultKey = chooseDefaultPerson(keptCards, keptPersons);

  return {
    addresses: keptCards.map(({ key: _key, ...card }) => ({
      ...card,
      label: clean(card.label),
      addressLine1: card.addressLine1.trim(),
      addressLine2: clean(card.addressLine2),
      landmark: clean(card.landmark),
      city: card.city.trim(),
      postalCode: clean(card.postalCode),
      gstin: clean(card.gstin)?.toUpperCase() ?? null,
    })),
    persons: keptPersons.map((person) => {
      const { firstName, lastName } = splitName(person.name);
      const index = person.addressKey != null ? indexByKey.get(person.addressKey) : undefined;
      const card = index !== undefined ? keptCards[index] : undefined;
      return {
        contactPersonId: person.contactPersonId,
        contactPersonRoleId: person.contactPersonRoleId,
        contactAddressId: card && card.contactAddressId > 0 ? card.contactAddressId : null,
        addressIndex: card && card.contactAddressId === 0 ? index : null,
        salutation: clean(person.salutation),
        firstName,
        lastName,
        designation: clean(person.designation),
        email: clean(person.email),
        phoneNumber: clean(person.phoneNumber),
        mobileNumber: clean(person.mobileNumber),
        website: clean(person.website),
        isDefault: person.key === defaultKey,
        isActive: person.isActive,
      };
    }),
  };
}

/** A problem with one field, named by the id the page gives that field. */
export interface FieldError {
  field: string;
  message: string;
}

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/**
 * What the address cards and person rows still need before a save, each named
 * by its field: `line1_<card>`, `city_<card>`, `pname_<person>`,
 * `prole_<person>`, `pemail_<person>`. A started address needs its first line
 * and a city, and a person a name and a role, even in a draft; a full save
 * also needs a main contact who can be reached.
 */
export function rowErrors(
  addresses: readonly AddressCard[],
  persons: readonly PersonRow[],
  draft: boolean,
): FieldError[] {
  const errors: FieldError[] = [];
  const kept = addresses.filter((card) => !isBlankAddress(card));
  kept.forEach((card, i) => {
    if (!hasText(card.addressLine1)) {
      errors.push({ field: `line1_${card.key}`, message: `Address ${i + 1} needs its first line.` });
    }
    if (!hasText(card.city)) {
      errors.push({ field: `city_${card.key}`, message: `Address ${i + 1} needs a city.` });
    }
  });

  const people = persons.filter((person) => !isBlankPerson(person));
  for (const person of people) {
    if (!hasText(person.name)) {
      errors.push({ field: `pname_${person.key}`, message: 'Every contact person needs a name.' });
    } else if (person.contactPersonRoleId <= 0) {
      errors.push({ field: `prole_${person.key}`, message: `Choose a role for ${person.name.trim()}.` });
    }
    if (hasText(person.email) && !EMAIL.test(person.email!.trim())) {
      errors.push({ field: `pemail_${person.key}`, message: `${hasText(person.name) ? person.name.trim() + "'s" : 'An'} email is not a valid address.` });
    }
  }

  if (draft) {
    return errors;
  }

  const active = people.filter((person) => person.isActive);
  if (active.length === 0) {
    const firstCard = addresses[0];
    errors.push({
      field: firstCard ? `pname_${persons.find((p) => p.addressKey === firstCard.key)?.key ?? ''}` : 'addPerson',
      message: 'Add a contact person: invoices and reminders go to them.',
    });
    return errors;
  }

  const defaultKey = chooseDefaultPerson(kept, active);
  const main = active.find((person) => person.key === defaultKey);
  if (main && !hasText(main.email) && !hasText(main.mobileNumber)) {
    errors.push({
      field: `pphone_${main.key}`,
      message: `${main.name.trim()} is the main contact, so needs an email or a phone number.`,
    });
  }

  return errors;
}

/** The first of `rowErrors`, or null. */
export function rowProblems(
  addresses: readonly AddressCard[],
  persons: readonly PersonRow[],
  draft: boolean,
): string | null {
  return rowErrors(addresses, persons, draft)[0]?.message ?? null;
}
