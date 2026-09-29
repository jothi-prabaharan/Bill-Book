import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import {
  MessageBoxComponent,
} from '@bill-book/ui-components';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PortalApi } from '../retail/portal-api.service';
import { PortalSummary } from '../retail/portal.models';

/**
 * The customer portal's dashboard (TK-95): what is owed and how much of it is
 * overdue, what has been traded this financial year and in all, and the last
 * few documents.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-portal-dashboard',
  standalone: true,
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    MessageBoxComponent,
  ],
  templateUrl: './portal-dashboard.page.html',
  styleUrl: '../retail/retail-portal.scss',
})
export class PortalDashboardPage implements OnInit {
  private readonly api = inject(PortalApi);

  protected readonly summary = signal<PortalSummary | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    try {
      this.summary.set(await this.api.summary());
    } catch {
      this.error.set('Your account could not be loaded. Try again in a moment.');
    } finally {
      this.loading.set(false);
    }
  }
}
