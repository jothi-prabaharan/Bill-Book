import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PortalApi } from '../retail/portal-api.service';
import { PortalPayment } from '../retail/portal.models';

/** How many times to ask before saying the gateway has not answered yet. */
const MAX_POLLS = 15;

/**
 * What became of an online payment (TK-98), read from the server. While the
 * gateway's callback has not arrived, it asks again every two seconds; the
 * gateway's redirect back to the portal is never taken as proof.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-portal-payment-result',
  standalone: true,
  imports: [DecimalPipe, RouterLink],
  templateUrl: './portal-payment-result.page.html',
  styleUrl: '../retail/retail-portal.scss',
})
export class PortalPaymentResultPage implements OnInit, OnDestroy {
  private readonly api = inject(PortalApi);
  private readonly route = inject(ActivatedRoute);
  private timer: ReturnType<typeof setTimeout> | null = null;
  private polls = 0;

  protected readonly payment = signal<PortalPayment | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly waitedTooLong = signal(false);

  ngOnInit(): void {
    void this.poll();
  }

  ngOnDestroy(): void {
    if (this.timer) clearTimeout(this.timer);
  }

  private async poll(): Promise<void> {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    try {
      const payment = await this.api.payment(id);
      this.payment.set(payment);
      if (payment.status === 'Created') {
        if (++this.polls >= MAX_POLLS) {
          this.waitedTooLong.set(true);
          return;
        }
        this.timer = setTimeout(() => void this.poll(), 2_000);
      }
    } catch {
      this.error.set('That payment could not be found.');
    }
  }
}
