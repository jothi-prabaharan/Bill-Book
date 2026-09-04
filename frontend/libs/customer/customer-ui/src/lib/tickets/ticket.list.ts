import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DataGridComponent, DataGridCellTemplateDirective, ColumnDef, MessageBoxComponent } from '@bill-book/ui-components';
import { CustomerService, Ticket } from '@bill-book/customer-core';
import { TicketFormComponent } from './ticket-form.component';
import { TicketThreadComponent } from './ticket-thread.component';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-ticket-list',
  standalone: true,
  imports: [CommonModule, DataGridComponent, DataGridCellTemplateDirective, TicketFormComponent, TicketThreadComponent, MessageBoxComponent],
  templateUrl: './ticket.list.html',
  styleUrl: './ticket.list.scss'
})
export class TicketList implements OnInit {
  private readonly customerService = inject(CustomerService);
  
  readonly tickets = signal<Ticket[]>([]);
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  
  readonly showForm = signal(false);
  readonly selectedTicket = signal<Ticket | null>(null);

  readonly columns: ColumnDef[] = [
    { field: 'ticketId', title: 'ID', dataType: 'number' },
    { field: 'subject', title: 'Subject', dataType: 'string' },
    { field: 'priority', title: 'Priority', dataType: 'string' },
    { field: 'status', title: 'Status', dataType: 'string' },
    { field: 'slaDueAt', title: 'SLA Due', dataType: 'date' },
    { field: 'actions', title: 'Actions', isTemplate: true }
  ];

  ngOnInit() {
    void this.loadTickets();
  }

  async loadTickets() {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const data = await this.customerService.getTickets();
      this.tickets.set(data);
    } catch (err: any) {
      this.errorMessage.set(err?.error?.message || 'Failed to load tickets.');
      console.error('Failed to load tickets', err);
    } finally {
      this.loading.set(false);
    }
  }

  viewThread(ticket: Ticket) {
    this.selectedTicket.set(ticket);
  }

  onSaved() {
    this.showForm.set(false);
    void this.loadTickets();
  }
}
