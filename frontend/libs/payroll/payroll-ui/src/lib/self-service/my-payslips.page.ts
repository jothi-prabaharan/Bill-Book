import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { readApiFailure } from '@bill-book/api-client';
import {
  MyPayslipSummary,
  PayrollApiService,
  PayslipView,
} from '@bill-book/payroll-core';
import { MessageBoxComponent, UiMessage } from '@bill-book/ui-components';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-my-payslips-page',
  standalone: true,
  imports: [CommonModule, MessageBoxComponent],
  templateUrl: './my-payslips.page.html',
  styleUrl: '../payroll-page.scss',
})
export class MyPayslipsPage implements OnInit {
  private readonly api = inject(PayrollApiService);

  protected readonly payslips = signal<MyPayslipSummary[]>([]);
  protected readonly selectedPayslip = signal<PayslipView | null>(null);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly loadingDetail = signal(false);

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.busy.set(true);
    this.messages.set([]);
    try {
      const list = await this.api.myPayslips();
      this.payslips.set(list);
    } catch (err) {
      const failure = readApiFailure(err);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.busy.set(false);
    }
  }

  protected async viewDetail(id: number): Promise<void> {
    this.loadingDetail.set(true);
    try {
      const detail = await this.api.myPayslip(id);
      this.selectedPayslip.set(detail);
    } catch (err) {
      const failure = readApiFailure(err);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.loadingDetail.set(false);
    }
  }

  protected closeDetail(): void {
    this.selectedPayslip.set(null);
  }

  protected async downloadHtml(id: number): Promise<void> {
    try {
      const html = await this.api.downloadPayslip(id);
      const blob = new Blob([html], { type: 'text/html' });
      const url = URL.createObjectURL(blob);
      const w = window.open(url, '_blank');
      if (w) {
        w.focus();
      }
    } catch (err) {
      const failure = readApiFailure(err);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    }
  }
}
