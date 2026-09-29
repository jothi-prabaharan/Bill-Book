/**
 * Custom fields (TK-110): a branch's own fields on a kind of record, defined in
 * Settings › Custom fields and validated by the owning service. These types
 * mirror `Shared.Kernel.CustomFields`; the helpers are pure.
 */

export type CustomFieldDataType =
  | 'Text'
  | 'LongText'
  | 'Integer'
  | 'Decimal'
  | 'Date'
  | 'Boolean'
  | 'Dropdown'
  | 'MultiSelect'
  | 'Email'
  | 'Url'
  | 'Phone';

export interface CustomFieldOption {
  value: string;
  label: string;
  active: boolean;
}

export interface CustomFieldDefinition {
  customFieldDefinitionId: number;
  entityKind: string;
  key: string;
  label: string;
  dataType: CustomFieldDataType;
  options: CustomFieldOption[];
  isRequired: boolean;
  defaultValue: string | null;
  maxLength: number | null;
  showInList: boolean;
  showOnPrint: boolean;
  isSearchable: boolean;
  carryForward: boolean;
  displayOrder: number;
  isActive: boolean;
}

/** A record's values: `{ "<key>": value }`, dates as ISO strings and decimals as numbers. */
export type CustomFieldValues = Record<string, unknown>;

/** Whether a value counts as not given: nothing, blank text or an empty list. */
export function isEmptyValue(value: unknown): boolean {
  return (
    value === null ||
    value === undefined ||
    (typeof value === 'string' && value.trim() === '') ||
    (Array.isArray(value) && value.length === 0)
  );
}

/**
 * The fields a form shows, in order: every active field, and an inactive one
 * only while the record still holds a value for it, so nothing stored
 * disappears from view.
 */
export function visibleFields(definitions: readonly CustomFieldDefinition[], values: CustomFieldValues): CustomFieldDefinition[] {
  return definitions
    .filter((d) => d.isActive || !isEmptyValue(values[d.key]))
    .sort((a, b) => a.displayOrder - b.displayOrder || a.label.localeCompare(b.label));
}

/** A new record's starting values, from each active field's default, typed as they are stored. */
export function defaultValues(definitions: readonly CustomFieldDefinition[]): CustomFieldValues {
  const values: CustomFieldValues = {};
  for (const d of definitions) {
    if (!d.isActive || d.defaultValue === null || d.defaultValue.trim() === '') {
      continue;
    }
    const text = d.defaultValue.trim();
    switch (d.dataType) {
      case 'Integer':
      case 'Decimal':
        values[d.key] = Number(text);
        break;
      case 'Boolean':
        values[d.key] = text.toLowerCase() === 'true';
        break;
      case 'MultiSelect':
        values[d.key] = text
          .split(',')
          .map((v) => v.trim())
          .filter((v) => v !== '');
        break;
      default:
        values[d.key] = text;
    }
  }
  return values;
}

/** The labels of required active fields a new record leaves empty. The server checks the same, on new saves only. */
export function missingRequired(definitions: readonly CustomFieldDefinition[], values: CustomFieldValues): string[] {
  return definitions.filter((d) => d.isActive && d.isRequired && isEmptyValue(values[d.key])).map((d) => d.label);
}

/** The options a field offers: active ones, plus the one already chosen even if it has since been deactivated. */
export function offeredOptions(field: CustomFieldDefinition, current: unknown): CustomFieldOption[] {
  const chosen = new Set(Array.isArray(current) ? current.map(String) : current === null || current === undefined ? [] : [String(current)]);
  return field.options.filter((o) => o.active || chosen.has(o.value));
}

/** Values with empty ones left out, as a save sends them. */
export function compactValues(values: CustomFieldValues): CustomFieldValues {
  return Object.fromEntries(Object.entries(values).filter(([, v]) => !isEmptyValue(v)));
}

/** A value as a list cell shows it: option labels for choices, Yes or No, a list joined by commas. */
export function customFieldText(field: CustomFieldDefinition, value: unknown): string {
  if (isEmptyValue(value)) {
    return '';
  }
  const labelOf = (v: unknown) => field.options.find((o) => o.value === v)?.label ?? String(v);
  switch (field.dataType) {
    case 'Boolean':
      return value === true ? 'Yes' : 'No';
    case 'Dropdown':
      return labelOf(value);
    case 'MultiSelect':
      return Array.isArray(value) ? value.map(labelOf).join(', ') : String(value);
    default:
      return String(value);
  }
}

/** The grid field a custom column is keyed on, kept apart from the record's own properties. */
export function customColumnField(key: string): string {
  return `cf_${key}`;
}

/** Columns for the active fields a branch chose to show in the list (`ShowInList`), in order. */
export function customListColumns(definitions: readonly CustomFieldDefinition[]): { field: string; header: string }[] {
  return definitions
    .filter((d) => d.isActive && d.showInList)
    .sort((a, b) => a.displayOrder - b.displayOrder || a.label.localeCompare(b.label))
    .map((d) => ({ field: customColumnField(d.key), header: d.label.toUpperCase() }));
}

/** Rows with each shown custom field's text added under its column field, for a grid that reads `row[field]`. */
export function withCustomColumns<T extends { customFields?: CustomFieldValues | null }>(
  rows: readonly T[],
  definitions: readonly CustomFieldDefinition[],
): (T & Record<string, unknown>)[] {
  const shown = definitions.filter((d) => d.isActive && d.showInList);
  if (shown.length === 0) {
    return rows as (T & Record<string, unknown>)[];
  }
  return rows.map((row) => {
    const extra: Record<string, string> = {};
    for (const d of shown) {
      extra[customColumnField(d.key)] = customFieldText(d, row.customFields?.[d.key]);
    }
    return { ...row, ...extra };
  });
}
