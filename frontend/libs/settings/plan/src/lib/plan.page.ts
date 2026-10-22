import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  BillingCycle,
  MonthlyUsage,
  PlanPrices,
  PlanService,
  PlanSummary,
  readApiFailure,
} from '@bill-book/api-client';
import { SessionContextService } from '@bill-book/auth';
import {
  MessageBoxComponent,
  RadioGroupComponent,
  UiMessage,
  UpgradeDialogService,
  UsageMeterComponent,
} from '@bill-book/ui-components';
import {
  PlanCard,
  buildPlanCards,
  chosenCycle,
  cycleOptions,
  planMeters,
  requestedPlan,
} from './plan.page.model';

/**
 * The customer's plan (TK-211): what it allows and how much of that the branch
 * has used, set beside the two paid plans with their prices, and a way to ask
 * for a higher one.
 *
 * There is no payment here (D-70). A request is recorded and mailed to the
 * platform's team, who change the plan; the screen shows the limits the server
 * gives each plan and the prices it keeps in a table (D-71), and holds no figure
 * of its own.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-plan-page',
  standalone: true,
  imports: [FormsModule, MessageBoxComponent, RadioGroupComponent, UsageMeterComponent],
  templateUrl: './plan.page.html',
  styleUrl: './plan.page.scss',
})
export class PlanPage implements OnInit {
  private readonly plans = inject(PlanService);
  private readonly upgradeDialog = inject(UpgradeDialogService);
  private readonly session = inject(SessionContextService);

  protected readonly summary = signal<PlanSummary | null>(null);
  protected readonly prices = signal<PlanPrices | null>(null);
  protected readonly sales = signal<MonthlyUsage | null>(null);
  protected readonly purchase = signal<MonthlyUsage | null>(null);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly loading = signal(true);
  protected readonly cycle = signal<BillingCycle>('Quarterly');

  protected readonly meters = computed(() => {
    const summary = this.summary();
    return summary ? planMeters(summary, this.sales(), this.purchase()) : [];
  });

  protected readonly cycleOptions = computed(() => cycleOptions(this.prices()));

  protected readonly cards = computed(() => {
    const summary = this.summary();
    const prices = this.prices();
    return summary && prices ? buildPlanCards(summary, prices, this.cycle()) : [];
  });

  /** Whether the branch's trade has a price to show. Without one the screen says the price is agreed. */
  protected readonly hasPrices = computed(() => (this.prices()?.cycles.length ?? 0) > 0);

  ngOnInit(): void {
    void this.load();
  }

  protected async request(card: PlanCard): Promise<void> {
    await this.upgradeDialog.open({
      toPlan: requestedPlan(card),
      canRequest: this.session.has('settings.edit'),
    });

    // The request may have gone through while the prompt was up.
    await this.refreshSummary();
  }

  private async load(): Promise<void> {
    this.loading.set(true);

    // Four reads that do not depend on one another. The plan and the prices
    // are the page; each document count is an extra that a branch which cannot
    // give it simply goes without, as the register badges do.
    const [summary, prices, sales, purchase] = await Promise.allSettled([
      this.plans.summary(),
      this.plans.prices(),
      this.plans.usage('SalesTransactions'),
      this.plans.usage('PurchaseTransactions'),
    ]);

    const problems: UiMessage[] = [];

    if (summary.status === 'fulfilled') {
      this.summary.set(summary.value);
    } else {
      const failure = readApiFailure(summary.reason);
      problems.push({ tone: 'error', text: failure.text, detail: failure.detail });
    }

    if (prices.status === 'fulfilled') {
      this.prices.set(prices.value);
      this.cycle.set(chosenCycle(prices.value, this.cycle())?.cycle ?? this.cycle());
    } else if (summary.status === 'fulfilled') {
      // Without prices there is nothing to compare, but the plan itself is still worth showing.
      problems.push({ tone: 'warning', text: 'The plans could not be compared right now.' });
    }

    this.sales.set(sales.status === 'fulfilled' ? sales.value : null);
    this.purchase.set(purchase.status === 'fulfilled' ? purchase.value : null);

    this.messages.set(problems);
    this.loading.set(false);
  }

  private async refreshSummary(): Promise<void> {
    try {
      this.summary.set(await this.plans.summary());
    } catch {
      // The screen already shows the last plan it read.
    }
  }
}
