import { CustomFieldDataType, CustomFieldDefinition, CustomFieldOption } from '@bill-book/ui-components';

/** The kinds of record a custom field can belong to, as Settings › Custom fields offers them (TK-110). */
export const ENTITY_KINDS: { value: string; label: string }[] = [
  { value: 'Contact', label: 'Contacts' },
  { value: 'Item', label: 'Items' },
  { value: 'Warehouse', label: 'Warehouses' },
  { value: 'Quote', label: 'Quotes' },
  { value: 'SalesOrder', label: 'Sales orders' },
  { value: 'DeliveryChallan', label: 'Delivery challans' },
  { value: 'Invoice', label: 'Invoices' },
  { value: 'CreditNote', label: 'Credit notes' },
  { value: 'PurchaseOrder', label: 'Purchase orders' },
  { value: 'GoodsReceipt', label: 'Goods receipts' },
  { value: 'Bill', label: 'Bills' },
  { value: 'DebitNote', label: 'Debit notes' },
  { value: 'Lead', label: 'Leads' },
  { value: 'Ticket', label: 'Tickets' },
  { value: 'Project', label: 'Projects' },
];

export const DATA_TYPES: { value: CustomFieldDataType; label: string }[] = [
  { value: 'Text', label: 'Text' },
  { value: 'LongText', label: 'Long text' },
  { value: 'Integer', label: 'Whole number' },
  { value: 'Decimal', label: 'Number' },
  { value: 'Date', label: 'Date' },
  { value: 'Boolean', label: 'Yes / no' },
  { value: 'Dropdown', label: 'Dropdown' },
  { value: 'MultiSelect', label: 'Multi-select' },
  { value: 'Email', label: 'Email' },
  { value: 'Url', label: 'Web address' },
  { value: 'Phone', label: 'Phone' },
];

/** The most active fields a kind may hold; the server refuses the fifty-first. */
export const MAX_ACTIVE_PER_KIND = 50;

export interface CustomFieldForm {
  entityKind: string;
  key: string;
  label: string;
  dataType: CustomFieldDataType;
  options: (CustomFieldOption & { saved: boolean })[];
  isRequired: boolean;
  defaultValue: string;
  maxLength: number | null;
  showInList: boolean;
  showOnPrint: boolean;
  isSearchable: boolean;
  carryForward: boolean;
  displayOrder: number;
  isActive: boolean;
}

export function hasOptions(type: CustomFieldDataType): boolean {
  return type === 'Dropdown' || type === 'MultiSelect';
}

export function isText(type: CustomFieldDataType): boolean {
  return type === 'Text' || type === 'LongText';
}

/**
 * A key suggested from a label: lowercase, words joined by underscores, and a
 * leading letter. "Site reference #" becomes `site_reference`. The key is
 * permanent once saved, so it is only suggested for a new field.
 */
export function keyFromLabel(label: string): string {
  const key = label
    .toLowerCase()
    .normalize('NFKD')
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')
    .slice(0, 40);
  return /^[a-z]/.test(key) ? key : key === '' ? '' : `f_${key}`.slice(0, 40);
}

export function isValidKey(key: string): boolean {
  return /^[a-z][a-z0-9_]{0,39}$/.test(key);
}

export function blankForm(entityKind: string, displayOrder: number): CustomFieldForm {
  return {
    entityKind,
    key: '',
    label: '',
    dataType: 'Text',
    options: [],
    isRequired: false,
    defaultValue: '',
    maxLength: null,
    showInList: false,
    showOnPrint: false,
    isSearchable: false,
    carryForward: false,
    displayOrder,
    isActive: true,
  };
}

export function formOf(field: CustomFieldDefinition): CustomFieldForm {
  return {
    entityKind: field.entityKind,
    key: field.key,
    label: field.label,
    dataType: field.dataType,
    options: field.options.map((o) => ({ ...o, saved: true })),
    isRequired: field.isRequired,
    defaultValue: field.defaultValue ?? '',
    maxLength: field.maxLength,
    showInList: field.showInList,
    showOnPrint: field.showOnPrint,
    isSearchable: field.isSearchable,
    carryForward: field.carryForward,
    displayOrder: field.displayOrder,
    isActive: field.isActive,
  };
}

/** What is wrong with a form before it is sent, or null. The server checks the same and more. */
export function formProblem(form: CustomFieldForm): string | null {
  if (!form.label.trim()) {
    return 'Give the field a label.';
  }
  if (!isValidKey(form.key)) {
    return 'A key starts with a lowercase letter and holds only lowercase letters, digits and underscores.';
  }
  if (hasOptions(form.dataType)) {
    if (!form.options.some((o) => o.active)) {
      return 'A dropdown or multi-select field needs at least one active option.';
    }
    if (form.options.some((o) => !o.value.trim() || !o.label.trim())) {
      return 'Every option needs a value and a label.';
    }
    const values = form.options.map((o) => o.value.trim());
    if (new Set(values).size !== values.length) {
      return 'Two options have the same value.';
    }
  }
  return null;
}

/** The request body for `POST`/`PUT api/custom-fields`. */
export function requestOf(form: CustomFieldForm) {
  return {
    entityKind: form.entityKind,
    key: form.key.trim(),
    label: form.label.trim(),
    dataType: form.dataType,
    options: hasOptions(form.dataType)
      ? form.options.map((o) => ({ value: o.value.trim(), label: o.label.trim(), active: o.active }))
      : [],
    isRequired: form.isRequired,
    defaultValue: form.defaultValue.trim() || null,
    maxLength: isText(form.dataType) ? form.maxLength : null,
    showInList: form.showInList,
    showOnPrint: form.showOnPrint,
    isSearchable: form.isSearchable,
    carryForward: form.carryForward,
    displayOrder: form.displayOrder,
    isActive: form.isActive,
  };
}
