import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ComponentKind, PayrollApiService, SalaryComponentView, SalaryValueType, SaveSalaryComponent } from '@bill-book/payroll-core';
import {
  BbSelectOption,
  CheckboxComponent,
  MessageBoxComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
} from '@bill-book/ui-components';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-salary-components-page',
  standalone: true,
  imports: [FormsModule, TextInputComponent, SelectComponent, CheckboxComponent, MessageBoxComponent],
  templateUrl: './salary-components.page.html',
  styleUrl: '../payroll-page.scss',
})
export class SalaryComponentsPage implements OnInit {
  private readonly api = inject(PayrollApiService);

  protected readonly rows = signal<SalaryComponentView[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly editingId = signal<number | null | undefined>(undefined);

  protected form: SaveSalaryComponent = this.blank();

  protected readonly kinds: BbSelectOption<ComponentKind>[] = [
    { value: 'Earning', label: 'Earning' },
    { value: 'Deduction', label: 'Deduction' },
    { value: 'Statutory', label: 'Statutory' },
  ];

  protected readonly valueTypes: BbSelectOption<SalaryValueType>[] = [
    { value: 'FlatAmount', label: 'Flat Amount' },
    { value: 'Percentage', label: 'Percentage' },
    { value: 'Formula', label: 'Formula' },
  ];

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    try {
      this.busy.set(true);
      const res = await this.api.components();
      this.rows.set(res);
    } catch {
      this.messages.set([{ tone: 'error', text: 'Could not load salary components.' }]);
    } finally {
      this.busy.set(false);
    }
  }

  protected startAdd(): void {
    this.form = this.blank();
    this.editingId.set(null);
  }

  protected startEdit(row: SalaryComponentView): void {
    this.form = {
      name: row.name,
      kind: row.kind,
      valueType: row.valueType,
      isTaxable: row.isTaxable,
      formula: row.formula,
      ledgerAccountId: row.ledgerAccountId,
    };
    this.editingId.set(row.salaryComponentId);
  }

  protected async save(): Promise<void> {
    if (!this.form.name.trim()) {
      this.messages.set([{ tone: 'error', text: 'Component name is required.' }]);
      return;
    }

    try {
      this.busy.set(true);
      await this.api.saveComponent(this.editingId() ?? null, this.form);
      this.editingId.set(undefined);
      await this.load();
      this.messages.set([{ tone: 'info', text: 'Salary component saved.' }]);
    } catch {
      this.messages.set([{ tone: 'error', text: 'Failed to save salary component.' }]);
    } finally {
      this.busy.set(false);
    }
  }

  protected async deleteRow(id: number): Promise<void> {
    try {
      this.busy.set(true);
      await this.api.deleteComponent(id);
      await this.load();
    } catch {
      this.messages.set([{ tone: 'error', text: 'Could not delete component.' }]);
    } finally {
      this.busy.set(false);
    }
  }

  private blank(): SaveSalaryComponent {
    return {
      name: '',
      kind: 'Earning',
      valueType: 'FlatAmount',
      isTaxable: true,
    };
  }
}
