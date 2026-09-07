import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  computed,
  forwardRef,
  inject,
  input,
} from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import {
  DEFAULT_FORMAT_SETTINGS,
  FormatSettings,
  FormatSettingsService,
} from '@bill-book/currency-format';
import { FormFieldComponent } from '../form-field/form-field.component';
import { BbNumericControlBase } from './numeric-control.base';

/**
 * An amount of money.
 *
 * Invoice totals, payment amounts, discount amounts, tax amounts, opening
 * balances, a journal line's debit and credit, an allocated amount.
 *
 * **Two decimals by default, because every money column in the schema has two**
 * — `decimal(28,2)` on the documents, `decimal(18,2)` on the ledger — but the
 * branch's own currency overrides that, below. Unit price is the exception and
 * has a component of its own; do not reach for this one for a rate per unit.
 *
 * ## The organization's currency format
 *
 * At rest the field shows the amount **grouped the way the branch's base
 * currency asks for it**: `12,34,567.89` for an organization on the rupee,
 * `1,234,567.89` for one on the dollar. Three things come from that currency
 * and none of them are restated here — the grouping mask, the number of decimal
 * places, and (when a symbol is asked for) which side it sits.
 *
 * They arrive from `mst.Currency` through `FormatSettingsService`, which the
 * app shell loads once for every screen. **The same row now also reaches the
 * front end on the organization itself**, as `Currency` on the get-organization
 * response — a screen that already holds the organization can pass those
 * details straight in through `formats` rather than depending on the shell
 * having loaded. Either way it is one row in one table, so the two cannot
 * disagree.
 *
 * A branch mid-setup that has declared no base currency falls back to the
 * shipped defaults, because a screen that cannot draw an amount is worse than
 * one drawing it in the common case.
 *
 * **The grouping is shown only when the field is not being edited.** Focus
 * strips it back to plain digits, so no separator ever moves under the caret.
 *
 * ## `minorDigits` is still the thing to get right
 *
 * Accounting works in decimal rupees and wants the default, `0`. The sales and
 * purchase line grid works in **integer paise** and wants `2`. Neither is a
 * conversion this component invents: it publishes exactly the units the caller
 * declared, so migrating a field never changes what reaches the API. The
 * grouping is applied to the exact decimal *text*, never to a number divided
 * back out of paise.
 *
 * `allowNegative` is off by default. Almost every amount in the product is a
 * magnitude with its direction carried elsewhere — a debit column, a credit
 * column, a document type — and a negative typed into one of those is a bug the
 * ledger will refuse later and more confusingly.
 */
@Component({
  selector: 'bb-money-input',
  standalone: true,
  imports: [FormFieldComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => MoneyInputComponent),
      multi: true,
    },
  ],
  templateUrl: './numeric-control.html',
  styleUrl: './numeric-control.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MoneyInputComponent extends BbNumericControlBase {
  /**
   * The branch's format, when the caller already holds it.
   *
   * A screen that has just read the organization has its `Currency` details in
   * hand and can pass them rather than depending on the shell's fetch. Null —
   * the usual case — reads the shared service instead.
   */
  readonly formats = input<FormatSettings | null>(null);

  /**
   * The symbol to show inside the field.
   *
   * Opt-in rather than automatic: a column of amounts under a currency heading
   * wants none, and a grid full of repeated symbols is noise. `true` takes the
   * branch's own symbol and puts it on the side its currency asks for.
   */
  readonly showCurrency = input<boolean, boolean | string>(false, {
    transform: (value) => value === '' || value === 'true' || value === true,
  });

  /** Overrides the symbol `showCurrency` would take from the branch. */
  readonly currencySymbol = input<string>('');

  readonly allowNegative = input<boolean, boolean | string>(false, {
    transform: (value) => value === '' || value === 'true' || value === true,
  });

  private readonly injector = inject(Injector);

  /** Resolved once, lazily. `undefined` means not yet looked for. */
  private branchFormats: FormatSettingsService | null | undefined;

  private readonly settings = computed<FormatSettings>(
    () => this.formats() ?? this.branch()?.settings() ?? DEFAULT_FORMAT_SETTINGS,
  );

  /**
   * The shared format service, resolved through the injector rather than
   * injected directly.
   *
   * **`inject(FormatSettingsService)` in a field initialiser does not survive
   * being constructed outside an application injector.** The service reaches
   * for `HttpClient`, and a component built by a spec — or by any injector that
   * has no HTTP configured — fails on that rather than falling back. Since this
   * workspace's Vitest builds every component by hand, that would make the
   * money field the one control that cannot be unit tested.
   *
   * Resolving it lazily and treating a failure as "not available" keeps the
   * component working in both places, with the shipped defaults standing where
   * there is nothing to read. In the application there always is: the app shell
   * loads it once for every screen.
   */
  private branch(): FormatSettingsService | null {
    if (this.branchFormats === undefined) {
      try {
        this.branchFormats = this.injector.get(FormatSettingsService);
      } catch {
        this.branchFormats = null;
      }
    }

    return this.branchFormats;
  }

  private readonly symbol = computed(
    () => this.currencySymbol() || (this.showCurrency() ? this.settings().currencySymbol : ''),
  );

  /** Grouped at rest, in the branch's own style. */
  protected override defaultMasked(): boolean {
    return true;
  }

  protected override defaultMask(): string {
    return this.settings().currencyMask;
  }

  /** The currency's own precision, not a constant — JPY has none, KWD has three. */
  protected override defaultDecimals(): number {
    return this.settings().currencyDecimals;
  }

  protected override defaultMin(): number | null {
    return this.allowNegative() ? null : 0;
  }

  protected override defaultPrefix(): string {
    return this.settings().symbolPosition === 'Suffix' ? '' : this.symbol();
  }

  protected override defaultSuffix(): string {
    return this.settings().symbolPosition === 'Suffix' ? this.symbol() : '';
  }

  protected override fallbackAriaLabel(): string {
    return 'Amount';
  }

  protected idPrefix(): string {
    return 'bb-money';
  }
}
