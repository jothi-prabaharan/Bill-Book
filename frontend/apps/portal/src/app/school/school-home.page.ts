import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { SchoolPortalApi } from './school-portal-api.service';
import { PortalChild, PortalDemand, PortalReceipt, totalDue } from './school-portal.models';

/**
 * The parent portal's home (S9, TK-69): the guardian's children, the fees
 * addressed to them with what is still owed, and the payments they have made.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-school-home',
  standalone: true,
  imports: [RouterLink, DecimalPipe],
  templateUrl: './school-home.page.html',
  styleUrl: './school-portal.scss',
})
export class SchoolHomePage implements OnInit {
  private readonly api = inject(SchoolPortalApi);

  protected readonly children = signal<PortalChild[]>([]);
  protected readonly demands = signal<PortalDemand[]>([]);
  protected readonly receipts = signal<PortalReceipt[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly due = computed(() => totalDue(this.demands()));

  ngOnInit(): void {
    void this.load();
  }

  protected childName(studentId: number): string {
    return this.children().find((c) => c.studentId === studentId)?.studentName ?? '';
  }

  private async load(): Promise<void> {
    try {
      const [children, demands, receipts] = await Promise.all([this.api.children(), this.api.demands(), this.api.receipts()]);
      this.children.set(children);
      this.demands.set(demands);
      this.receipts.set(receipts);
    } catch {
      this.error.set('Your details could not be loaded. Open the link from the school again, or try later.');
    } finally {
      this.loading.set(false);
    }
  }
}
