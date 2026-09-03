import { ChangeDetectionStrategy, Component, EventEmitter, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { CustomerService, TicketPriority } from '@bill-book/customer-core';
import { MasterSelectComponent } from '@bill-book/ui-components';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-ticket-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MasterSelectComponent],
  templateUrl: './ticket-form.component.html',
  styleUrl: './ticket-form.component.scss'
})
export class TicketFormComponent {
  private readonly customerService = inject(CustomerService);
  
  @Output() saved = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  readonly saving = signal(false);
  readonly priorities = Object.values(TicketPriority);

  readonly form = new FormGroup({
    subject: new FormControl('', { nonNullable: true, validators: Validators.required }),
    description: new FormControl('', { nonNullable: true, validators: Validators.required }),
    priority: new FormControl<TicketPriority>(TicketPriority.Medium, { nonNullable: true, validators: Validators.required }),
    contactId: new FormControl('', { nonNullable: true, validators: Validators.required })
  });

  async save() {
    if (this.form.invalid) return;
    this.saving.set(true);
    try {
      await this.customerService.createTicket(this.form.getRawValue());
      this.saved.emit();
    } catch (err) {
      console.error('Failed to save ticket', err);
    } finally {
      this.saving.set(false);
    }
  }
}
