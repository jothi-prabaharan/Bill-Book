import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  afterNextRender,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { readApiFailure } from '@bill-book/api-client';
import { firstValueFrom } from 'rxjs';
import { CheckboxComponent } from '../forms/checkbox/checkbox.component';
import { SelectComponent } from '../forms/select/select.component';
import { MessageBoxComponent } from '../message-box/message-box.component';
import { NumberInputComponent } from '../number-input/number-input.component';
import { TextInputComponent } from '../text-input/text-input.component';
import {
  BankAccountForm,
  BankAccountKind,
  BankAccountRecord,
  BankAccountSaved,
  BankChoice,
  accountRequestFrom,
  blankAccountForm,
  kindCanOverdraw,
  changeAccountKind,
  findBankByName,
  accountFormFromRecord,
  accountFormProblem,
  allowedKinds,
  kindLedgerNote,
  kindNeedsBank,
} from './bank-account-form';

/**
 * Adds or edits one of the branch's own bank or cash accounts, as a popup
 * (TK-268). It opens from Banking › Bank accounts, and from the till's Tender
 * window beside **Into**, so a payment account can be added where it is missing
 * without leaving what is on the screen.
 *
 * Creating an account creates its ledger account, in Accounting, in the same
 * request; nothing here writes a ledger row.
 *
 * **It saves for itself and tells the opener what it saved.** The opener
 * reloads whatever list it shows and, as the contact form does with a role
 * added from its dropdown, picks the new account out by its name. A refusal is
 * shown inside the popup, where the person is looking, rather than behind it.
 *
 * **A bank that is not on the list is typed in place.** With no bank on file
 * the popup starts that way, because there is nothing to choose from; the bank
 * is added to Banking › Banks first, then the account is saved under it.
 *
 * Its decisions are in `bank-account-form.ts`, as plain functions.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-bank-account-dialog',
  standalone: true,
  imports: [FormsModule, TextInputComponent, SelectComponent, CheckboxComponent, NumberInputComponent, MessageBoxComponent],
  templateUrl: './bank-account-dialog.component.html',
  styleUrl: './bank-account-dialog.component.scss',
  host: { '(document:keydown.escape)': 'close()' },
})
export class BankAccountDialogComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);

  /** The account being edited. Null starts a new one. */
  readonly account = input<BankAccountRecord | null>(null);

  /** The kinds the Type list offers. Null offers every kind. */
  readonly kinds = input<readonly BankAccountKind[] | null>(null);

  /** What a new account starts with. Not used when an account is being edited. */
  readonly preset = input<Partial<BankAccountForm>>({});

  /** The heading, when the opener has words of its own for it. */
  readonly heading = input<string>('');

  /** Closed without saving, or after the opener was told of a save. */
  readonly closed = output<void>();

  /** The account was saved. */
  readonly saved = output<BankAccountSaved>();

  protected readonly form = signal<BankAccountForm>(blankAccountForm());
  protected readonly banks = signal<BankChoice[]>([]);
  protected readonly addingBank = signal(false);
  protected readonly newBankName = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly dirty = signal(false);

  protected readonly editing = computed(() => this.account() !== null);
  protected readonly title = computed(
    () => this.heading() || (this.editing() ? 'Edit bank account' : 'New bank account'),
  );
  protected readonly kindOptions = computed(() => allowedKinds(this.kinds()));
  protected readonly bankOptions = computed(() =>
    this.banks().map((bank) => ({ value: bank.bankId, label: bank.bankName })),
  );
  protected readonly bankNeeded = computed(() => kindNeedsBank(this.form().accountType));
  protected readonly overdraws = computed(() => kindCanOverdraw(this.form().accountType));
  protected readonly note = computed(() => kindLedgerNote(this.form().accountType));
  protected readonly problem = computed(() =>
    accountFormProblem(this.form(), this.addingBank() ? this.newBankName() : null),
  );
  protected readonly canSave = computed(() => !this.busy() && this.problem() === null);

  constructor() {
    // The first field takes the cursor, with what a preset put in it selected.
    afterNextRender(() => {
      const first = this.element.nativeElement.querySelector<HTMLInputElement>('.bad-fields input');
      first?.focus();
      first?.select();
    });
  }

  ngOnInit(): void {
    const existing = this.account();
    this.form.set(existing === null ? blankAccountForm(this.preset()) : accountFormFromRecord(existing));
    void this.loadBanks();
  }

  close(): void {
    if (!this.busy()) {
      this.closed.emit();
    }
  }

  /** A click on the dimmed page closes it, unless that would throw away what has been typed. */
  protected onBackdrop(): void {
    if (!this.dirty()) {
      this.close();
    }
  }

  protected set<K extends keyof BankAccountForm>(key: K, value: BankAccountForm[K]): void {
    this.dirty.set(true);
    this.form.update((form) => ({ ...form, [key]: value }));
  }

  protected setKind(kind: BankAccountKind | null): void {
    if (kind !== null) {
      this.dirty.set(true);
      this.form.update((form) => changeAccountKind(form, kind));
    }
  }

  protected setNewBank(name: string): void {
    this.dirty.set(true);
    this.newBankName.set(name);
  }

  protected async save(): Promise<void> {
    const problem = this.problem();
    if (problem !== null) {
      this.error.set(problem);
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    try {
      if (kindNeedsBank(this.form().accountType) && this.addingBank()) {
        const bankId = await this.bankIdFor(this.newBankName());
        // From here on the bank exists, so a refusal of the account does not ask for it again.
        this.form.update((form) => ({ ...form, bankId }));
        this.addingBank.set(false);
      }

      const request = accountRequestFrom(this.form());
      const existing = this.account();

      if (existing === null) {
        await firstValueFrom(this.http.post('/api/bank-accounts', request));
      } else {
        await firstValueFrom(this.http.put(`/api/bank-accounts/${existing.bankAccountId}`, request));
      }

      this.saved.emit({ accountName: request.accountName, accountType: request.accountType });
    } catch (error) {
      this.error.set(readApiFailure(error).text);
    } finally {
      this.busy.set(false);
    }
  }

  /** The bank typed in: the one already on the list under that name, or a new one added now. */
  private async bankIdFor(name: string): Promise<number> {
    const known = findBankByName(this.banks(), name);
    if (known !== null) {
      return known;
    }

    await firstValueFrom(this.http.post('/api/banks', { bankName: name.trim(), isActive: true }));

    const banks = await this.fetchBanks();
    this.banks.set(banks);

    const created = findBankByName(banks, name);
    if (created === null) {
      throw new Error('The bank was added but is not on the list.');
    }

    return created;
  }

  private async loadBanks(): Promise<void> {
    try {
      this.banks.set(await this.fetchBanks());
    } catch {
      // Nothing to choose from; a bank can still be typed in, and cash in hand needs none.
    }

    // A new account with no bank on file starts with the bank to be typed in.
    if (this.account() === null && this.banks().length === 0) {
      this.addingBank.set(true);
    }
  }

  private async fetchBanks(): Promise<BankChoice[]> {
    const rows = await firstValueFrom(this.http.get<BankChoice[]>('/api/banks'));
    return rows.map(({ bankId, bankName }) => ({ bankId, bankName }));
  }
}
