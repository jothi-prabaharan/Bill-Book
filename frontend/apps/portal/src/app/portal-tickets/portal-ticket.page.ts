import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PortalApi } from '../retail/portal-api.service';
import { PortalTicketDetail, ticketStatusLabel } from '../retail/portal.models';

/** One ticket's conversation, and a reply (TK-97). Replying to a resolved ticket reopens it. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-portal-ticket',
  standalone: true,
  imports: [DatePipe, FormsModule, RouterLink],
  templateUrl: './portal-ticket.page.html',
  styleUrl: '../retail/retail-portal.scss',
})
export class PortalTicketPage implements OnInit {
  private readonly api = inject(PortalApi);
  private readonly route = inject(ActivatedRoute);

  protected readonly detail = signal<PortalTicketDetail | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly statusLabel = ticketStatusLabel;

  protected body = '';

  private get id(): number {
    return Number(this.route.snapshot.paramMap.get('id'));
  }

  ngOnInit(): void {
    void this.load();
  }

  protected async reply(): Promise<void> {
    if (!this.body.trim()) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.api.replyToTicket(this.id, this.body.trim());
      this.body = '';
      await this.load();
    } catch (err: unknown) {
      const message = err instanceof HttpErrorResponse ? (err.error as { message?: string } | null)?.message : undefined;
      this.error.set(message ?? 'Your reply could not be sent. Try again in a moment.');
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    try {
      this.detail.set(await this.api.ticket(this.id));
    } catch {
      this.error.set('That ticket could not be found.');
    }
  }
}
