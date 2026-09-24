import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PayrollApiService, PayrollRunView, PayslipView } from '@bill-book/payroll-core';
import { MessageBoxComponent, UiMessage } from '@bill-book/ui-components';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-payroll-run-page',
  standalone: true,
  imports: [CommonModule, RouterModule, MessageBoxComponent],
  templateUrl: './payroll-run.page.html',
  styleUrl: '../payroll-page.scss',
})
export class PayrollRunPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(PayrollApiService);

  protected runId = 0;
  protected readonly run = signal<PayrollRunView | null>(null);
  protected readonly payslips = signal<PayslipView[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.runId = Number(idParam);
      void this.load();
    }
  }

  private async load(): Promise<void> {
    try {
      this.busy.set(true);
      const [r, slips] = await Promise.all([
        this.api.run(this.runId),
        this.api.payslips(this.runId),
      ]);
      this.run.set(r);
      this.payslips.set(slips);
    } catch {
      this.messages.set([{ tone: 'error', text: 'Could not load payroll run details.' }]);
    } finally {
      this.busy.set(false);
    }
  }

  protected async approve(): Promise<void> {
    try {
      this.busy.set(true);
      await this.api.approveRun(this.runId);
      await this.load();
      this.messages.set([{ tone: 'info', text: 'Payroll run approved.' }]);
    } catch {
      this.messages.set([{ tone: 'error', text: 'Failed to approve run.' }]);
    } finally {
      this.busy.set(false);
    }
  }

  protected async post(): Promise<void> {
    try {
      this.busy.set(true);
      await this.api.postRun(this.runId);
      await this.load();
      this.messages.set([{ tone: 'info', text: 'Payroll posted to General Ledger.' }]);
    } catch {
      this.messages.set([{ tone: 'error', text: 'Failed to post payroll run to Accounting.' }]);
    } finally {
      this.busy.set(false);
    }
  }

  protected async markPaid(): Promise<void> {
    try {
      this.busy.set(true);
      await this.api.markPaidRun(this.runId);
      await this.load();
      this.messages.set([{ tone: 'info', text: 'Payroll marked as paid.' }]);
    } catch {
      this.messages.set([{ tone: 'error', text: 'Failed to mark run as paid.' }]);
    } finally {
      this.busy.set(false);
    }
  }

  protected async reverse(): Promise<void> {
    try {
      this.busy.set(true);
      await this.api.reverseRun(this.runId);
      await this.load();
      this.messages.set([{ tone: 'info', text: 'Payroll run reversed.' }]);
    } catch {
      this.messages.set([{ tone: 'error', text: 'Failed to reverse run.' }]);
    } finally {
      this.busy.set(false);
    }
  }
}
