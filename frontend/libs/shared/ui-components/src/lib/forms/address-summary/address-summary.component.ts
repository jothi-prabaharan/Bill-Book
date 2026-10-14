import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * A compact, read-only summary of an address chosen from a dropdown.
 * Replaces manual multi-line textareas across sales and purchase transaction screens.
 */
@Component({
  selector: 'bb-address-summary',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './address-summary.component.html',
  styleUrl: './address-summary.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AddressSummaryComponent {
  /** The party or organization branch name. */
  readonly title = input<string | null | undefined>();

  /** Optional badge, e.g. "Default Billing", "Head Office", "Same as billing". */
  readonly badge = input<string | null | undefined>();

  /** Formatted address lines. */
  readonly lines = input<string | null | undefined>();

  /** Optional contact/party email. */
  readonly email = input<string | null | undefined>();

  /** Optional phone number. */
  readonly phone = input<string | null | undefined>();

  /** Optional GSTIN. */
  readonly gstin = input<string | null | undefined>();

  /** Placeholder when no address is selected. */
  readonly emptyText = input<string>('No address selected. Choose from dropdown above.');
}
