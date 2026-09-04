import { ChangeDetectionStrategy, Component, EventEmitter, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { CustomerService, LeadSource } from '@bill-book/customer-core';
import { TextInputComponent, MessageBoxComponent } from '@bill-book/ui-components';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-lead-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TextInputComponent, MessageBoxComponent],
  templateUrl: './lead-form.component.html',
  styleUrl: './lead-form.component.scss'
})
export class LeadFormComponent {
  private readonly customerService = inject(CustomerService);
  
  @Output() saved = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly sources = Object.values(LeadSource);

  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: Validators.required }),
    companyName: new FormControl('', { nonNullable: true }),
    email: new FormControl('', { nonNullable: true, validators: Validators.email }),
    phone: new FormControl('', { nonNullable: true }),
    source: new FormControl<LeadSource>(LeadSource.Website, { nonNullable: true, validators: Validators.required })
  });

  async save() {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.saving.set(true);
    this.errorMessage.set(null);
    try {
      await this.customerService.createLead(this.form.getRawValue());
      this.saved.emit();
    } catch (err: any) {
      this.errorMessage.set(err?.error?.message || 'Failed to save lead. Please try again.');
      console.error('Failed to save lead', err);
    } finally {
      this.saving.set(false);
    }
  }
}
