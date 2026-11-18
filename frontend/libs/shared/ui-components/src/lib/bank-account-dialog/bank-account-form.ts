/**
 * The decisions behind the bank and cash account popup (TK-268), as plain
 * functions: what each kind of account asks for, what makes the form complete,
 * and the body `POST` or `PUT api/bank-accounts` takes.
 *
 * Kept out of the component for the reason `allocation-modal.model.ts` and
 * `label-print.model.ts` are: signal inputs cannot be set from outside a
 * component fixture and this workspace's Vitest cannot compile a `templateUrl`
 * component at all, so anything left inside the component is untested.
 */

/** What kind of account it is, as Accounting names it (`BankAccountType`). */
export type BankAccountKind =
  | 'Savings'
  | 'Current'
  | 'OverDraft'
  | 'CashCredit'
  | 'CreditCard'
  | 'Wallet'
  | 'Cash'
  | 'EEFC';

export interface BankAccountKindOption {
  value: BankAccountKind;
  label: string;
}

/** Every kind, in the order the Type list shows them. */
export const BANK_ACCOUNT_KINDS: readonly BankAccountKindOption[] = [
  { value: 'Savings', label: 'Savings' },
  { value: 'Current', label: 'Current' },
  { value: 'OverDraft', label: 'Overdraft' },
  { value: 'CashCredit', label: 'Cash credit' },
  { value: 'CreditCard', label: 'Credit card' },
  { value: 'Wallet', label: 'Wallet' },
  { value: 'Cash', label: 'Cash in hand' },
  { value: 'EEFC', label: 'EEFC account' },
];

/** An account as `GET api/bank-accounts` returns it, the part the form edits. */
export interface BankAccountRecord {
  bankAccountId: number;
  bankId: number | null;
  accountName: string;
  accountNumber: string;
  /** A `BankAccountKind`; the server sends it as a name. */
  accountType: string;
  ifsc: string | null;
  micr: string | null;
  swiftCode: string | null;
  iban: string | null;
  branchName: string | null;
  currencyCode: string;
  odLimit: number | null;
  isActive: boolean;
}

/**
 * What the form holds. Text fields are strings and never null, because that is
 * what the text inputs publish; the request turns a blank into null.
 */
export interface BankAccountForm {
  bankId: number | null;
  accountName: string;
  accountNumber: string;
  accountType: BankAccountKind;
  ifsc: string;
  micr: string;
  swiftCode: string;
  iban: string;
  branchName: string;
  currencyCode: string;
  odLimit: number | null;
  isActive: boolean;
}

/** The body of `POST` and `PUT api/bank-accounts`. */
export interface BankAccountRequest {
  bankId: number | null;
  accountName: string;
  accountNumber: string;
  accountType: BankAccountKind;
  ifsc: string | null;
  micr: string | null;
  swiftCode: string | null;
  iban: string | null;
  branchName: string | null;
  currencyCode: string;
  odLimit: number | null;
  isActive: boolean;
}

/** What the popup tells its opener once the account is saved. */
export interface BankAccountSaved {
  accountName: string;
  accountType: BankAccountKind;
}

/** A bank as the form needs it: to pick one, or to find the one just typed. */
export interface BankChoice {
  bankId: number;
  bankName: string;
}

/** Cash in hand and wallets have no institution behind them; everything else names a bank. */
export function kindNeedsBank(kind: BankAccountKind): boolean {
  return kind !== 'Cash' && kind !== 'Wallet';
}

/** Only borrowing kinds take a limit. */
export function kindCanOverdraw(kind: BankAccountKind): boolean {
  return kind === 'OverDraft' || kind === 'CashCredit' || kind === 'CreditCard';
}

/** What the ledger account will be: a liability for anything that can be overdrawn. */
export function kindLedgerNote(kind: BankAccountKind): string {
  if (!kindNeedsBank(kind)) {
    return 'Creates an Asset account under Cash in Hand.';
  }

  return kindCanOverdraw(kind)
    ? 'Creates a Liability account under Bank OD & Credit Cards, because an overdrawn account is borrowing.'
    : 'Creates an Asset account under Bank Accounts.';
}

/** The kinds the Type list offers: all of them, or only those the opener allows. */
export function allowedKinds(allowed: readonly BankAccountKind[] | null): readonly BankAccountKindOption[] {
  return allowed === null ? BANK_ACCOUNT_KINDS : BANK_ACCOUNT_KINDS.filter((kind) => allowed.includes(kind.value));
}

/** A new account's form, with what the opener presets laid over the defaults. */
export function blankAccountForm(preset: Partial<BankAccountForm> = {}): BankAccountForm {
  return settled({
    bankId: null,
    accountName: '',
    accountNumber: '',
    accountType: 'Current',
    ifsc: '',
    micr: '',
    swiftCode: '',
    iban: '',
    branchName: '',
    currencyCode: 'INR',
    odLimit: null,
    isActive: true,
    ...preset,
  });
}

/** The form for an account that already exists. */
export function accountFormFromRecord(record: BankAccountRecord): BankAccountForm {
  return settled({
    bankId: record.bankId,
    accountName: record.accountName,
    accountNumber: record.accountNumber,
    accountType: record.accountType as BankAccountKind,
    ifsc: record.ifsc ?? '',
    micr: record.micr ?? '',
    swiftCode: record.swiftCode ?? '',
    iban: record.iban ?? '',
    branchName: record.branchName ?? '',
    currencyCode: record.currencyCode,
    odLimit: record.odLimit,
    isActive: record.isActive,
  });
}

/** Changing the kind drops what the new kind does not use, so it is never sent by accident. */
export function changeAccountKind(form: BankAccountForm, kind: BankAccountKind): BankAccountForm {
  return settled({ ...form, accountType: kind });
}

/**
 * The first thing wrong with the form, in the order the fields read, or null
 * when it can be saved.
 *
 * `newBankName` is null while a bank is being chosen from the list, and the
 * text typed while a new bank is being added in its place.
 */
export function accountFormProblem(form: BankAccountForm, newBankName: string | null): string | null {
  if (!hasText(form.accountName)) {
    return 'Account name is required.';
  }

  if (!hasText(form.accountNumber)) {
    return 'Account number is required.';
  }

  if (kindNeedsBank(form.accountType)) {
    if (newBankName === null && form.bankId === null) {
      return 'Choose a bank for this account type.';
    }

    if (newBankName !== null && !hasText(newBankName)) {
      return 'Enter the name of the bank.';
    }
  }

  if (form.currencyCode.trim().length !== 3) {
    return 'Currency must be a 3-letter code.';
  }

  if (kindCanOverdraw(form.accountType) && form.odLimit !== null && form.odLimit < 0) {
    return 'The limit cannot be negative.';
  }

  return null;
}

/** The request body: trimmed, codes upper-cased, a blank optional field sent as null. */
export function accountRequestFrom(form: BankAccountForm): BankAccountRequest {
  const bank = kindNeedsBank(form.accountType);

  return {
    bankId: bank ? form.bankId : null,
    accountName: form.accountName.trim(),
    accountNumber: form.accountNumber.trim(),
    accountType: form.accountType,
    ifsc: bank ? optional(form.ifsc)?.toUpperCase() ?? null : null,
    micr: bank ? optional(form.micr) : null,
    swiftCode: bank ? optional(form.swiftCode)?.toUpperCase() ?? null : null,
    iban: bank ? optional(form.iban) : null,
    branchName: bank ? optional(form.branchName) : null,
    currencyCode: form.currencyCode.trim().toUpperCase(),
    odLimit: kindCanOverdraw(form.accountType) ? form.odLimit : null,
    isActive: form.isActive,
  };
}

/**
 * The bank whose name is the one typed, ignoring case and surrounding space, so
 * a bank typed in the popup that is already on the list is chosen rather than
 * refused as a duplicate.
 */
export function findBankByName(banks: readonly BankChoice[], name: string): number | null {
  const wanted = name.trim().toLowerCase();
  if (wanted === '') {
    return null;
  }

  return banks.find((bank) => bank.bankName.trim().toLowerCase() === wanted)?.bankId ?? null;
}

function settled(form: BankAccountForm): BankAccountForm {
  const bank = kindNeedsBank(form.accountType);

  return {
    ...form,
    bankId: bank ? form.bankId : null,
    ifsc: bank ? form.ifsc : '',
    micr: bank ? form.micr : '',
    swiftCode: bank ? form.swiftCode : '',
    iban: bank ? form.iban : '',
    branchName: bank ? form.branchName : '',
    odLimit: kindCanOverdraw(form.accountType) ? form.odLimit : null,
  };
}

function hasText(value: string | null | undefined): boolean {
  return (value ?? '').trim().length > 0;
}

function optional(value: string): string | null {
  const cleaned = value.trim();
  return cleaned.length > 0 ? cleaned : null;
}
