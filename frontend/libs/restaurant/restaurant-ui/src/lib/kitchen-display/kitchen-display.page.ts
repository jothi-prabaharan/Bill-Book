import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { IfCanDirective } from '@bill-book/auth';
import {
  KITCHEN_POLL_MS,
  KitchenStatus,
  Kot,
  KotLine,
  MenuStation,
  PrintedDocument,
  RESTAURANT_API,
  STATIONS,
  canCancel,
  cancelNeedsApproval,
  isLate,
  minutesSince,
  nextKitchenSteps,
  orderPlace,
} from '@bill-book/restaurant-core';
import { MessageBoxComponent, TextareaComponent, UiMessage, printHtml } from '@bill-book/ui-components';

/**
 * Restaurant › Kitchen display (TK-147): every ticket still being worked, at
 * one station or all, oldest first. Cooks move each dish on — preparing,
 * ready, served — and a ticket drops off once every dish is served or
 * cancelled. It asks for new tickets every ten seconds.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-kitchen-display',
  standalone: true,
  imports: [FormsModule, IfCanDirective, MessageBoxComponent, TextareaComponent],
  templateUrl: './kitchen-display.page.html',
  styleUrl: './kitchen-display.page.scss',
})
export class KitchenDisplayPage implements OnInit, OnDestroy {
  private readonly http = inject(HttpClient);
  private timer: ReturnType<typeof setInterval> | null = null;

  protected readonly station = signal<MenuStation | null>(null);
  protected readonly tickets = signal<Kot[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly now = signal(new Date());
  protected readonly cancelling = signal<KotLine | null>(null);
  protected cancelReason = '';

  protected readonly stations = STATIONS;
  protected readonly nextKitchenSteps = nextKitchenSteps;
  protected readonly canCancel = canCancel;
  protected readonly cancelNeedsApproval = cancelNeedsApproval;
  protected readonly orderPlace = orderPlace;

  ngOnInit(): void {
    void this.load();
    this.timer = setInterval(() => void this.load(), KITCHEN_POLL_MS);
  }

  ngOnDestroy(): void {
    if (this.timer) clearInterval(this.timer);
  }

  protected chooseStation(station: MenuStation | null): void {
    this.station.set(station);
    void this.load();
  }

  protected async load(): Promise<void> {
    try {
      const query = this.station() ? `?station=${this.station()}` : '';
      this.tickets.set(await firstValueFrom(this.http.get<Kot[]>(`${RESTAURANT_API}/kitchen${query}`)));
      this.now.set(new Date());
      if (this.messages().some((m) => m.tone === 'error')) this.messages.set([]);
    } catch (error) {
      this.fail(error, 'The kitchen tickets could not be loaded. Retrying.');
    }
  }

  protected age(ticket: Kot): string {
    return minutesSince(ticket.sentAt, this.now());
  }

  protected late(ticket: Kot): boolean {
    return isLate(ticket.sentAt, this.now());
  }

  protected stepLabel(step: KitchenStatus): string {
    return step === 'Preparing' ? 'Start' : step === 'Ready' ? 'Ready' : 'Served';
  }

  protected async step(line: KotLine, status: KitchenStatus): Promise<void> {
    await this.run(() => firstValueFrom(this.http.put(`${RESTAURANT_API}/kot-lines/${line.kotLineId}/status`, { status })));
  }

  protected startCancel(line: KotLine): void {
    this.cancelReason = '';
    this.cancelling.set(line);
  }

  protected async cancel(): Promise<void> {
    const line = this.cancelling();
    if (!line) return;
    if (!this.cancelReason.trim()) {
      this.messages.set([{ tone: 'error', text: 'Say why the dish is cancelled.' }]);
      return;
    }
    await this.run(async () => {
      await firstValueFrom(this.http.post(`${RESTAURANT_API}/kot-lines/${line.kotLineId}/cancel`, { reason: this.cancelReason.trim() }));
      this.cancelling.set(null);
    });
  }

  protected async reprint(ticket: Kot): Promise<void> {
    await this.run(async () => {
      const printed = await firstValueFrom(this.http.post<PrintedDocument>(`${RESTAURANT_API}/kots/${ticket.kotId}/print`, {}));
      printHtml(printed.html);
    });
  }

  private async run(work: () => Promise<unknown>): Promise<void> {
    this.busy.set(true);
    try {
      await work();
      await this.load();
    } catch (error) {
      this.fail(error, 'That could not be done.');
    } finally {
      this.busy.set(false);
    }
  }

  private fail(error: unknown, fallback: string): void {
    const message = error instanceof HttpErrorResponse ? (error.error?.message as string | undefined) : undefined;
    this.messages.set([{ tone: 'error', text: message ?? fallback }]);
  }
}
