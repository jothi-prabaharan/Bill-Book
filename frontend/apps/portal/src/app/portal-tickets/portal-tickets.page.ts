import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { PortalApi } from '../retail/portal-api.service';
import { PortalTicketItem, ticketStatusLabel } from '../retail/portal.models';

/** The contact's support tickets, and raising a new one (TK-97). */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-portal-tickets',
  standalone: true,
  imports: [DatePipe, FormsModule, RouterLink],
  templateUrl: './portal-tickets.page.html',
  styleUrl: '../retail/retail-portal.scss',
})
export class PortalTicketsPage implements OnInit {
  private readonly api = inject(PortalApi);
  private readonly router = inject(Router);

  protected readonly tickets = signal<PortalTicketItem[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(true);
  protected readonly raising = signal(false);
  protected readonly busy = signal(false);
  protected readonly statusLabel = ticketStatusLabel;

  protected subject = '';
  protected description = '';

  ngOnInit(): void {
    void this.load();
  }

  protected async raise(): Promise<void> {
    if (!this.subject.trim()) {
      this.error.set('Give the ticket a subject.');
      return;
    }
    this.busy.set(true);
    try {
      const { ticketId } = await this.api.raiseTicket(this.subject.trim(), this.description.trim() || null);
      await this.router.navigate(['/tickets', ticketId]);
    } catch {
      this.error.set('The ticket could not be raised. Try again in a moment.');
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    try {
      this.tickets.set(await this.api.tickets());
    } catch {
      this.error.set('Your tickets could not be loaded. Try again in a moment.');
    } finally {
      this.loading.set(false);
    }
  }
}
