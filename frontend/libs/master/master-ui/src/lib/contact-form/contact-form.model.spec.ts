import { describe, expect, it } from 'vitest';
import {
  AddressCard,
  ContactAddressDto,
  ContactPersonDto,
  PersonRow,
  blankAddress,
  blankPerson,
  chooseDefaultPerson,
  isBlankAddress,
  isBlankPerson,
  joinName,
  rowProblems,
  splitName,
  toFormRows,
  toPayloadRows,
} from './contact-form.model';

/** The contact form's pure steps (TK-188). */

function card(overrides: Partial<AddressCard> = {}): AddressCard {
  return { ...blankAddress(1, false), addressLine1: '12 Avinashi Road', city: 'Coimbatore', ...overrides };
}

function person(addressKey: string | null, overrides: Partial<PersonRow> = {}): PersonRow {
  return { ...blankPerson(7, addressKey), name: 'Kavitha R', mobileNumber: '9843011234', ...overrides };
}

function storedAddress(id: number, overrides: Partial<ContactAddressDto> = {}): ContactAddressDto {
  const { key: _key, ...rest } = card();
  return { ...rest, contactAddressId: id, ...overrides };
}

function storedPerson(id: number, addressId: number | null, overrides: Partial<ContactPersonDto> = {}): ContactPersonDto {
  return {
    contactPersonId: id,
    contactPersonRoleId: 7,
    contactAddressId: addressId,
    salutation: null,
    firstName: 'Suresh',
    lastName: 'M',
    designation: 'Stores',
    email: 'stores@contact.in',
    phoneNumber: null,
    mobileNumber: '9843055120',
    website: null,
    isDefault: false,
    isActive: true,
    ...overrides,
  };
}

describe('names', () => {
  it('CF-01: splits at the first space, as the server does for a quick contact', () => {
    expect(splitName('Kavitha R')).toEqual({ firstName: 'Kavitha', lastName: 'R' });
    expect(splitName('  Suresh   Kumar  M ')).toEqual({ firstName: 'Suresh', lastName: 'Kumar M' });
    expect(splitName('Madonna')).toEqual({ firstName: 'Madonna', lastName: null });
  });

  it('CF-02: joins without a stray space when there is no last name', () => {
    expect(joinName('Kavitha', 'R')).toBe('Kavitha R');
    expect(joinName('Madonna', null)).toBe('Madonna');
  });
});

describe('blank rows', () => {
  it('CF-03: the first address is the default billing and shipping address; later ones are neither', () => {
    expect(blankAddress(1, true)).toMatchObject({ isDefaultBilling: true, isDefaultShipping: true, kind: 'RegisteredOffice' });
    expect(blankAddress(1, false)).toMatchObject({ isDefaultBilling: false, isDefaultShipping: false });
  });

  it('CF-04: an untouched card and an untouched person are blank; a saved one never is', () => {
    expect(isBlankAddress(blankAddress(1, true))).toBe(true);
    expect(isBlankAddress({ ...blankAddress(1, true), city: 'Coimbatore' })).toBe(false);
    expect(isBlankAddress({ ...blankAddress(1, true), contactAddressId: 4 })).toBe(false);
    expect(isBlankPerson(blankPerson(7, 'a1'))).toBe(true);
    expect(isBlankPerson({ ...blankPerson(7, 'a1'), email: 'a@b.in' })).toBe(false);
    expect(isBlankPerson({ ...blankPerson(7, 'a1'), contactPersonId: 3 })).toBe(false);
  });
});

describe('loading', () => {
  it('CF-05: ties each stored person to the card of the address they name', () => {
    const rows = toFormRows([storedAddress(11), storedAddress(12)], [storedPerson(21, 12), storedPerson(22, null)]);

    expect(rows.persons[0].addressKey).toBe(rows.addresses[1].key);
    expect(rows.persons[1].addressKey).toBeNull();
    expect(rows.persons[0].name).toBe('Suresh M');
  });

  it('CF-06: keeps what the form does not show, so a save sends it back unchanged', () => {
    const rows = toFormRows([], [storedPerson(21, null, { designation: 'Stores', website: 'https://x.in' })]);
    const sent = toPayloadRows(rows.addresses, rows.persons).persons[0];

    expect(sent.designation).toBe('Stores');
    expect(sent.website).toBe('https://x.in');
  });
});

describe('saving', () => {
  it('CF-07: a person on a new card is sent by position, on a stored card by id', () => {
    const stored = card({ contactAddressId: 11 });
    const added = card({ addressLine1: 'Plot 44' });
    const payload = toPayloadRows([stored, added], [person(stored.key), person(added.key, { name: 'Suresh M' })]);

    expect(payload.persons[0]).toMatchObject({ contactAddressId: 11, addressIndex: null });
    expect(payload.persons[1]).toMatchObject({ contactAddressId: null, addressIndex: 1 });
  });

  it('CF-08: a blank card is dropped, and positions count only the cards sent', () => {
    const blank = blankAddress(1, false);
    const added = card({ addressLine1: 'Plot 44' });
    const payload = toPayloadRows([blank, added], [person(added.key)]);

    expect(payload.addresses).toHaveLength(1);
    expect(payload.persons[0].addressIndex).toBe(0);
  });

  it('CF-09: a blank person is dropped; the address card shows one whether filled or not', () => {
    const address = card();
    const payload = toPayloadRows([address], [blankPerson(7, address.key)]);

    expect(payload.persons).toHaveLength(0);
  });

  it('CF-10: the main contact is the one ticked, else the person at the default billing address, else the first', () => {
    const office = card({ isDefaultBilling: true });
    const yard = card({ addressLine1: 'Plot 44' });
    const atYard = person(yard.key, { name: 'Suresh M' });
    const atOffice = person(office.key);

    expect(chooseDefaultPerson([office, yard], [atYard, atOffice])).toBe(atOffice.key);
    expect(chooseDefaultPerson([office, yard], [{ ...atYard, isDefault: true }, atOffice])).toBe(atYard.key);
    expect(chooseDefaultPerson([yard], [atYard, atOffice])).toBe(atYard.key);

    const payload = toPayloadRows([office, yard], [atYard, atOffice]);
    expect(payload.persons.map((p) => p.isDefault)).toEqual([false, true]);
  });

  it('CF-11: names are split, GSTINs upper-cased and blank text sent as null', () => {
    const address = card({ gstin: '33aabce1234f1z5', postalCode: '  ' });
    const payload = toPayloadRows([address], [person(address.key, { email: '' })]);

    expect(payload.addresses[0]).toMatchObject({ gstin: '33AABCE1234F1Z5', postalCode: null });
    expect(payload.persons[0]).toMatchObject({ firstName: 'Kavitha', lastName: 'R', email: null });
    expect('key' in payload.addresses[0]).toBe(false);
  });
});

describe('checks', () => {
  it('CF-12: a draft needs no person, a save does', () => {
    expect(rowProblems([], [], true)).toBeNull();
    expect(rowProblems([], [], false)).toMatch(/contact person/);
  });

  it('CF-13: the main contact needs an email or a phone', () => {
    const address = card();
    expect(rowProblems([address], [person(address.key, { mobileNumber: null, email: null })], false)).toMatch(/main contact/);
    expect(rowProblems([address], [person(address.key)], false)).toBeNull();
  });

  it('CF-14: a started address needs its first line and a city, even in a draft', () => {
    expect(rowProblems([card({ addressLine1: '' })], [], true)).toMatch(/first line/);
    expect(rowProblems([card({ city: '' })], [], true)).toMatch(/city/);
  });

  it('CF-15: a person with a phone but no name is refused, even in a draft', () => {
    const address = card();
    expect(rowProblems([address], [person(address.key, { name: '' })], true)).toMatch(/name/);
  });
});
