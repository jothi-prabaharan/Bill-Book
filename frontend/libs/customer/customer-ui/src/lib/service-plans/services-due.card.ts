import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterModule } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { FormatSettingsService } from '@bill-book/currency-format';
import { SERVICE_PLANS_API, ServicePlan, dueLabel, dueTone } from './service-plans.model';

/**
 * The dashboard's "Services due" card (TK-140): active service plans due in the
 * next fortnight or already late, soonest first. A person without the
 * `support` permission sees that the card is not theirs, not an error.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-services-due-card',
  standalone: true,
  imports: [RouterModule],
  templateUrl: './services-due.card.html',
  styleUrl: './services-due.card.scss',
})
export class ServicesDueCard implements OnInit {
  private readonly http = inject(HttpClient);
  protected readonly formats = inject(FormatSettingsService);

  protected readonly plans = signal<ServicePlan[]>([]);
  protected readonly state = signal<'loading' | 'ready' | 'unavailable'>('loading');

  protected readonly dueLabel = dueLabel;
  protected readonly dueTone = dueTone;

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    try {
      const plans = await firstValueFrom(this.http.get<ServicePlan[]>(`${SERVICE_PLANS_API}?dueWithinDays=14`));
      this.plans.set(plans.slice(0, 6));
      this.state.set('ready');
    } catch {
      this.state.set('unavailable');
    }
  }
}
