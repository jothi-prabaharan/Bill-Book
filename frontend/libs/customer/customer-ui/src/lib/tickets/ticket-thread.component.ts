import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, OnInit, inject, signal, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CustomerService, Ticket, TicketMessage } from '@bill-book/customer-core';
import { MessageBoxComponent } from '@bill-book/ui-components';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-ticket-thread',
  standalone: true,
  imports: [CommonModule, FormsModule, MessageBoxComponent],
  templateUrl: './ticket-thread.component.html',
  styleUrl: './ticket-thread.component.scss'
})
export class TicketThreadComponent implements OnChanges {
  private readonly customerService = inject(CustomerService);

  @Input({ required: true }) ticket!: Ticket;
  @Output() closed = new EventEmitter<void>();

  readonly _ticket = signal<Ticket | null>(null);
  readonly messages = signal<TicketMessage[]>([]);
  readonly replyBody = signal('');
  readonly loading = signal(false);
  readonly sending = signal(false);
  readonly errorMessage = signal<string | null>(null);

  ngOnChanges(changes: SimpleChanges) {
    if (changes['ticket'] && this.ticket) {
      this._ticket.set(this.ticket);
      void this.loadMessages();
    }
  }

  async loadMessages() {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const msgs = await this.customerService.getTicketMessages(this.ticket.ticketId);
      this.messages.set(msgs);
    } catch (err: any) {
      this.errorMessage.set(err?.error?.message || 'Failed to load thread.');
      console.error('Failed to load messages', err);
    } finally {
      this.loading.set(false);
    }
  }

  async sendMessage() {
    if (!this.replyBody().trim()) return;
    this.sending.set(true);
    this.errorMessage.set(null);
    try {
      await this.customerService.createTicketMessage(this.ticket.ticketId, this.replyBody());
      this.replyBody.set('');
      await this.loadMessages();
    } catch (err: any) {
      this.errorMessage.set(err?.error?.message || 'Failed to send message.');
      console.error('Failed to send message', err);
    } finally {
      this.sending.set(false);
    }
  }
}
