import { ChangeDetectionStrategy, Component, forwardRef, input } from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { FormFieldComponent } from '../form-field/form-field.component';
import { BbNumericControlBase } from './numeric-control.base';

/**
 * An amount of money.
 *
 * Invoice totals, payment amounts, discount amounts, tax amounts, opening
 * balances, a journal line's debit and credit, an allocated amount.
 *
 * **Two decimals, because every money column in the schema has two** —
 * `decimal(28,2)` on the documents, `decimal(18,2)` on the ledger. Unit price
 * is the exception and has a component of its own; do not reach for this one
 * for a rate per unit.
 *
 * **`minorDigits` is the thing to get right, and it is not the same everywhere
 * in this product.** Accounting works in decimal rupees and wants the default,
 * `0`. The sales and purchase line grid works in **integer paise** and wants
 * `2`. Neither is a conversion this component invents: it publishes exactly the
 * units the caller declared, so migrating a field never changes what reaches
 * the API.
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
   * The symbol to show inside the field. Empty shows none, which is what a
   * column of amounts under a currency heading wants.
   */
  readonly currencySymbol = input<string>('');

  readonly allowNegative = input<boolean, boolean | string>(false, {
    transform: (value) => value === '' || value === 'true' || value === true,
  });

  protected override defaultDecimals(): number {
    return 2;
  }

  protected override defaultMin(): number | null {
    return this.allowNegative() ? null : 0;
  }

  protected override defaultPrefix(): string {
    return this.currencySymbol();
  }

  protected override fallbackAriaLabel(): string {
    return 'Amount';
  }

  protected idPrefix(): string {
    return 'bb-money';
  }
}
