import { describe, expect, it } from 'vitest';
import {
  CustomFieldDefinition,
  compactValues,
  defaultValues,
  missingRequired,
  offeredOptions,
  visibleFields,
} from './custom-fields.model';

function field(partial: Partial<CustomFieldDefinition>): CustomFieldDefinition {
  return {
    customFieldDefinitionId: 1,
    entityKind: 'Contact',
    key: 'region',
    label: 'Region',
    dataType: 'Dropdown',
    options: [
      { value: 'north', label: 'North', active: true },
      { value: 'east', label: 'East', active: false },
    ],
    isRequired: false,
    defaultValue: null,
    maxLength: null,
    showInList: false,
    showOnPrint: false,
    isSearchable: false,
    carryForward: false,
    displayOrder: 0,
    isActive: true,
    ...partial,
  };
}

describe('custom fields', () => {
  it('shows active fields in order, and an inactive one only while it holds a value', () => {
    const defs = [
      field({ key: 'b', label: 'B', displayOrder: 2 }),
      field({ key: 'a', label: 'A', displayOrder: 1 }),
      field({ key: 'old', label: 'Old', isActive: false }),
    ];
    expect(visibleFields(defs, {}).map((d) => d.key)).toEqual(['a', 'b']);
    expect(visibleFields(defs, { old: 'kept' }).map((d) => d.key)).toEqual(['old', 'a', 'b']);
  });

  it('types defaults as they are stored', () => {
    expect(
      defaultValues([
        field({ key: 'n', dataType: 'Integer', defaultValue: '3' }),
        field({ key: 'b', dataType: 'Boolean', defaultValue: 'true' }),
        field({ key: 'm', dataType: 'MultiSelect', defaultValue: 'north, south' }),
        field({ key: 't', dataType: 'Text', defaultValue: 'x' }),
        field({ key: 'off', dataType: 'Text', defaultValue: 'x', isActive: false }),
      ]),
    ).toEqual({ n: 3, b: true, m: ['north', 'south'], t: 'x' });
  });

  it('names required active fields left empty', () => {
    const defs = [field({ key: 'po', label: 'PO ref', dataType: 'Text', isRequired: true })];
    expect(missingRequired(defs, {})).toEqual(['PO ref']);
    expect(missingRequired(defs, { po: '  ' })).toEqual(['PO ref']);
    expect(missingRequired(defs, { po: 'A1' })).toEqual([]);
  });

  it('offers a deactivated option only where it is already chosen', () => {
    const region = field({});
    expect(offeredOptions(region, null).map((o) => o.value)).toEqual(['north']);
    expect(offeredOptions(region, 'east').map((o) => o.value)).toEqual(['north', 'east']);
  });

  it('leaves empty values out of a save', () => {
    expect(compactValues({ a: '', b: [], c: null, d: 0, e: false, f: 'x' })).toEqual({ d: 0, e: false, f: 'x' });
  });
});

describe('custom field list columns', () => {
  it('adds a column per shown active field, with the value as text', async () => {
    const { customListColumns, withCustomColumns, customFieldText } = await import('./custom-fields.model');
    const region = field({ showInList: true });
    const hidden = field({ key: 'note', label: 'Note', dataType: 'Text', showInList: false });
    const vip = field({ key: 'vip', label: 'VIP', dataType: 'Boolean', showInList: true, displayOrder: 5 });

    expect(customListColumns([region, hidden, vip])).toEqual([
      { field: 'cf_region', header: 'REGION' },
      { field: 'cf_vip', header: 'VIP' },
    ]);
    const [row] = withCustomColumns([{ id: 1, customFields: { region: 'north', vip: true } }], [region, hidden, vip]);
    expect(row['cf_region']).toBe('North');
    expect(row['cf_vip']).toBe('Yes');
    expect(customFieldText(field({ dataType: 'MultiSelect' }), ['north', 'east'])).toBe('North, East');
  });
});
