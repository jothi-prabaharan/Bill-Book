import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { readApiFailure } from '@bill-book/api-client';
import { IfCanDirective } from '@bill-book/auth';
import { HrmApiService, PolicyDocument } from '@bill-book/hrm-core';
import { CheckboxComponent, DateInputComponent, MessageBoxComponent, TextInputComponent, UiMessage } from '@bill-book/ui-components';

/** People › Policies (H1, TK-48). HRMS only. Acknowledgements come with self-service (H8). */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-hrm-policies-page',
  standalone: true,
  imports: [FormsModule, TextInputComponent, DateInputComponent, CheckboxComponent, MessageBoxComponent, IfCanDirective],
  templateUrl: './policies.page.html',
  styleUrl: '../hrm-page.scss',
})
export class PoliciesPage implements OnInit {
  private readonly api = inject(HrmApiService);

  protected readonly rows = signal<PolicyDocument[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly editingId = signal<number | null | undefined>(undefined);
  protected form: PolicyDocument = this.blank();

  ngOnInit(): void {
    void this.load();
  }

  protected startAdd(): void {
    this.form = this.blank();
    this.editingId.set(null);
  }

  protected startEdit(row: PolicyDocument): void {
    this.form = { ...row };
    this.editingId.set(row.policyDocumentId ?? null);
  }

  protected async save(): Promise<void> {
    if (!this.form.title.trim() || !this.form.attachmentKey.trim()) {
      this.messages.set([{ tone: 'error', text: 'Give a title and the policy file.' }]);
      return;
    }
    this.busy.set(true);
    try {
      await this.api.savePolicy(this.editingId() ?? null, this.form);
      this.messages.set([{ tone: 'success', text: 'The policy is saved.' }]);
      this.editingId.set(undefined);
      await this.load();
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    try {
      this.rows.set(await this.api.policies());
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    }
  }

  private blank(): PolicyDocument {
    return { title: '', attachmentKey: '', effectiveDate: new Date().toISOString().slice(0, 10), isAcknowledgementRequired: false, isActive: true };
  }
}
