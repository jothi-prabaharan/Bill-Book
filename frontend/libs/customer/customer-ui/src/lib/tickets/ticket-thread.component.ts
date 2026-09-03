import { ChangeDetectionStrategy, Component, Input, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CustomerService, Ticket, TicketMessage } from '@bill-book/customer-core';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-ticket-thread',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './ticket-thread.component.html',
  styleUrl: './ticket-thread.component.scss'
})
export class TicketThreadComponent implements OnInit {
  private readonly customerService = inject(CustomerService);
  
  @Input({ required: true }) ticket!: Ticket;

  readonly messages = signal<TicketMessage[]>([]);
  readonly loading = signal(false);
  readonly newMessage = signal('');
  readonly sending = signal(false);

  ngOnInit() {
    void this.loadMessages();
  }

  async loadMessages() {
    this.loading.set(true);
    try {
      const data = await this.customerService.getTicketMessages(this.ticket.ticketId);
      this.messages.set(data);
    } catch (err) {
      console.error('Failed to load messages', err);
    } finally {
      this.loading.set(false);
    }
  }

  async sendMessage() {
    const text = this.newMessage().trim();
    if (!text) return;

    this.sending.set(true);
    try {
      await this.customerService.createTicketMessage(this.ticket.ticketId, text);
      this.newMessage.set('');
      await this.loadMessages();
    } catch (err) {
      console.error('Failed to send message', err);
    } finally {
      this.sending.set(false);
    }
  }
}
