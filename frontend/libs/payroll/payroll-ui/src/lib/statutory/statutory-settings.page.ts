import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PayrollApiService } from '@bill-book/payroll-core';
import {
  CheckboxComponent,
  MessageBoxComponent,
  NumberInputComponent,
  UiMessage,
} from '@bill-book/ui-components';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-statutory-settings-page',
  standalone: true,
  imports: [FormsModule, NumberInputComponent, CheckboxComponent, MessageBoxComponent],
  templateUrl: './statutory-settings.page.html',
  styleUrl: '../payroll-page.scss',
})
export class StatutorySettingsPage implements OnInit {
  private readonly api = inject(PayrollApiService);

  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);

  protected pf: any = {
    effectiveFrom: '2020-01-01',
    employeeContributionRate: 12.0,
    employerContributionRate: 12.0,
    wageCeiling: 15000.0,
    restrictToWageCeiling: true,
    adminChargesRate: 0.5,
    edliRate: 0.5,
  };

  protected esi: any = {
    effectiveFrom: '2020-01-01',
    employeeContributionRate: 0.75,
    employerContributionRate: 3.25,
    wageCeiling: 21000.0,
  };

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    try {
      this.busy.set(true);
      const [pfData, esiData] = await Promise.all([
        this.api.pfSetting(),
        this.api.esiSetting(),
      ]);
      if (pfData) this.pf = { ...this.pf, ...pfData };
      if (esiData) this.esi = { ...this.esi, ...esiData };
    } catch {
      this.messages.set([{ tone: 'error', text: 'Could not load statutory settings.' }]);
    } finally {
      this.busy.set(false);
    }
  }

  protected async saveAll(): Promise<void> {
    try {
      this.busy.set(true);
      await Promise.all([
        this.api.savePfSetting(this.pf),
        this.api.saveEsiSetting(this.esi),
      ]);
      this.messages.set([{ tone: 'info', text: 'Statutory settings saved successfully.' }]);
    } catch {
      this.messages.set([{ tone: 'error', text: 'Failed to save statutory settings.' }]);
    } finally {
      this.busy.set(false);
    }
  }
}
