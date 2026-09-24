import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PayrollApiService } from '@bill-book/payroll-core';
import {
  MessageBoxComponent,
  NumberInputComponent,
  TextInputComponent,
  UiMessage,
} from '@bill-book/ui-components';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-fnf-settlement-page',
  standalone: true,
  imports: [CommonModule, DecimalPipe, FormsModule, NumberInputComponent, TextInputComponent, MessageBoxComponent],
  templateUrl: './fnf-settlement.page.html',
  styleUrl: '../payroll-page.scss',
})
export class FnfSettlementPage implements OnInit {
  private readonly api = inject(PayrollApiService);

  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);

  protected employeeId = 1;
  protected lastWorkingDate = new Date().toISOString().substring(0, 10);
  protected noticeShortfallDays = 0;
  protected completedYearsOfService = 5;
  protected remarks = '';

  protected settlement: any = null;

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    try {
      this.busy.set(true);
      this.settlement = await this.api.fnfSettlementByEmployee(this.employeeId);
    } catch {
      this.settlement = null;
    } finally {
      this.busy.set(false);
    }
  }

  protected async calculate(): Promise<void> {
    try {
      this.busy.set(true);
      const res = await this.api.calculateFnf({
        employeeId: this.employeeId,
        lastWorkingDate: this.lastWorkingDate,
        noticeShortfallDays: this.noticeShortfallDays,
        completedYearsOfService: this.completedYearsOfService,
        remarks: this.remarks,
      });
      this.settlement = await this.api.fnfSettlement(res.id);
      this.messages.set([{ tone: 'success', text: 'Settlement computed successfully.' }]);
    } catch (err: any) {
      this.messages.set([{ tone: 'error', text: err?.message || 'Calculation failed.' }]);
    } finally {
      this.busy.set(false);
    }
  }

  protected async approve(): Promise<void> {
    if (!this.settlement) return;
    try {
      this.busy.set(true);
      await this.api.approveFnf(this.settlement.fullAndFinalSettlementId);
      await this.load();
      this.messages.set([{ tone: 'success', text: 'Settlement approved.' }]);
    } catch {
      this.messages.set([{ tone: 'error', text: 'Approval failed.' }]);
    } finally {
      this.busy.set(false);
    }
  }

  protected async post(): Promise<void> {
    if (!this.settlement) return;
    try {
      this.busy.set(true);
      const res = await this.api.postFnf(this.settlement.fullAndFinalSettlementId);
      await this.load();
      this.messages.set([{ tone: 'success', text: `Settlement posted through FullAndFinal Run #${res.runId}.` }]);
    } catch {
      this.messages.set([{ tone: 'error', text: 'Posting settlement failed.' }]);
    } finally {
      this.busy.set(false);
    }
  }
}
