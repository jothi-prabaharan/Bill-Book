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
  selector: 'bb-tax-declarations-page',
  standalone: true,
  imports: [FormsModule, NumberInputComponent, TextInputComponent, MessageBoxComponent],
  templateUrl: './tax-declarations.page.html',
  styleUrl: '../payroll-page.scss',
})
export class TaxDeclarationsPage implements OnInit {
  private readonly api = inject(PayrollApiService);

  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);

  protected employeeId = 1;
  protected financialYear = '2026-2027';
  protected regime = 'New';
  protected isLocked = false;
  protected declarationId: number | null = null;

  protected sec80C = 0;
  protected sec80D = 0;
  protected hra = 0;

  // Previous employer
  protected prevEmployerName = '';
  protected prevGross = 0;
  protected prevTds = 0;

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    try {
      this.busy.set(true);
      const decl = await this.api.taxDeclaration(this.employeeId, this.financialYear);
      if (decl) {
        this.declarationId = decl.taxDeclarationId;
        this.regime = decl.regime;
        this.isLocked = decl.isLocked;
        for (const line of decl.lines || []) {
          if (line.section === '80C') this.sec80C = line.declaredAmount;
          if (line.section === '80D') this.sec80D = line.declaredAmount;
          if (line.section === 'HRA') this.hra = line.declaredAmount;
        }
      }
      const prev = await this.api.previousEmployerIncome(this.employeeId, this.financialYear);
      if (prev) {
        this.prevEmployerName = prev.employerName || '';
        this.prevGross = prev.grossIncome;
        this.prevTds = prev.totalTdsDeducted;
      }
    } catch {
      // If 404 or new, keep defaults
    } finally {
      this.busy.set(false);
    }
  }

  protected async save(): Promise<void> {
    try {
      this.busy.set(true);
      const lines = [
        { section: '80C', declaredAmount: this.sec80C, proofAmount: 0, verifiedAmount: 0 },
        { section: '80D', declaredAmount: this.sec80D, proofAmount: 0, verifiedAmount: 0 },
        { section: 'HRA', declaredAmount: this.hra, proofAmount: 0, verifiedAmount: 0 },
      ];
      await this.api.saveTaxDeclaration({
        employeeId: this.employeeId,
        financialYear: this.financialYear,
        regime: this.regime,
        lines,
      });

      if (this.prevGross > 0 || this.prevTds > 0) {
        await this.api.savePreviousEmployerIncome({
          employeeId: this.employeeId,
          financialYear: this.financialYear,
          employerName: this.prevEmployerName,
          grossIncome: this.prevGross,
          totalTdsDeducted: this.prevTds,
        });
      }

      this.messages.set([{ tone: 'success', text: 'Tax declaration and previous income saved.' }]);
    } catch (err: any) {
      this.messages.set([{ tone: 'error', text: err?.message || 'Could not save declaration.' }]);
    } finally {
      this.busy.set(false);
    }
  }

  protected async toggleLock(): Promise<void> {
    if (!this.declarationId) return;
    try {
      this.busy.set(true);
      if (this.isLocked) {
        await this.api.unlockTaxDeclaration(this.declarationId);
        this.isLocked = false;
        this.messages.set([{ tone: 'success', text: 'Declaration unlocked.' }]);
      } else {
        await this.api.lockTaxDeclaration(this.declarationId);
        this.isLocked = true;
        this.messages.set([{ tone: 'success', text: 'Declaration locked.' }]);
      }
    } catch {
      this.messages.set([{ tone: 'error', text: 'Failed to update lock state.' }]);
    } finally {
      this.busy.set(false);
    }
  }
}
