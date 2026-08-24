import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DataGridComponent, DataGridCellTemplateDirective, ColumnDef } from '@bill-book/ui-components';
import { CustomerService, Ticket } from '@bill-book/customer-core';
import { TicketFormComponent } from './ticket-form.component';
import { TicketThreadComponent } from './ticket-thread.component';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-ticket-list',
  standalone: true,
  imports: [CommonModule, DataGridComponent, DataGridCellTemplateDirective, TicketFormComponent, TicketThreadComponent],
  templateUrl: './ticket.list.html',
  styleUrl: './ticket.list.scss'
})
export class TicketList implements OnInit {
  private readonly customerService = inject(CustomerService);
  
  readonly tickets = signal<Ticket[]>([]);
  readonly loading = signal(false);
  readonly showForm = signal(false);
  readonly selectedTicket = signal<Ticket | null>(null);

  readonly columns: ColumnDef[] = [
    { field: 'subject', title: 'Subject', dataType: 'string' },
    { field: 'priority', title: 'Priority', dataType: 'string' },
    { field: 'status', title: 'Status', dataType: 'string' },
    { field: 'actions', title: 'Actions', isTemplate: true }
  ];

  ngOnInit() {
    void this.loadTickets();
  }

  async loadTickets() {
    this.loading.set(true);
    try {
      const data = await this.customerService.getTickets();
      this.tickets.set(data);
    } catch (err) {
      console.error('Failed to load tickets', err);
    } finally {
      this.loading.set(false);
    }
  }

  onSaved() {
    this.showForm.set(false);
    void this.loadTickets();
  }
}
