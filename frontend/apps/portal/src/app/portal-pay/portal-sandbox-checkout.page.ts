import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { PortalApi } from '../retail/portal-api.service';

/**
 * The sandbox gateway's checkout (TK-98, D-25). It takes no money: it sends
 * the callback a real gateway would send, and the result page then reads what
 * the server recorded. It exists until a real gateway is chosen.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-portal-sandbox-checkout',
  standalone: true,
  templateUrl: './portal-sandbox-checkout.page.html',
  styleUrl: '../retail/retail-portal.scss',
})
export class PortalSandboxCheckoutPage {
  private readonly api = inject(PortalApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async finish(succeed: boolean): Promise<void> {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.busy.set(true);
    try {
      await this.api.sandboxCheckout(id, succeed);
      await this.router.navigate(['/pay/result', id], { replaceUrl: true });
    } catch {
      this.error.set('The sandbox could not complete the payment.');
      this.busy.set(false);
    }
  }
}
