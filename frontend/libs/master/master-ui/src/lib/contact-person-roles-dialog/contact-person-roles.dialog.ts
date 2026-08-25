import { ChangeDetectionStrategy } from '@angular/core';
import { Component, output } from '@angular/core';
import { ContactPersonRolesList } from '../contact-person-roles-list/contact-person-roles.list';

export type { ContactPersonRole } from '../contact-person-roles-list/contact-person-roles.list';

/**
 * The role master as a popup. It opens from the contact list and from the role
 * dropdown on the contact form, so somebody halfway through a contact can add a
 * missing role without abandoning what they have typed.
 *
 * <b>Chrome only.</b> The list, and every action on it, is
 * <see cref="ContactPersonRolesList"/>, which the Settings page shows without a
 * dialog around it. What differs between the two surfaces is how they are
 * dismissed and nothing else.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-contact-person-roles-dialog',
  standalone: true,
  imports: [ContactPersonRolesList],
  templateUrl: './contact-person-roles.dialog.html',
  styleUrl: './contact-person-roles.dialog.scss',
  // Escape closes it. Clicking the backdrop does too, but that is a pointer
  // gesture with no keyboard equivalent — and a modal a keyboard user cannot
  // dismiss is a trap.
  host: { '(document:keydown.escape)': 'close()' },
})
export class ContactPersonRolesDialog {
  /** Fires on close so the opener can reload its role list. */
  readonly closed = output<void>();

  close(): void {
    this.closed.emit();
  }
}

