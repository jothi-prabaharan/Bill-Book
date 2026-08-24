import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DataGridComponent, DataGridCellTemplateDirective, ColumnDef } from '@bill-book/ui-components';
import { CustomerService, Lead } from '@bill-book/customer-core';
import { LeadFormComponent } from './lead-form.component';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-lead-list',
  standalone: true,
  imports: [CommonModule, DataGridComponent, DataGridCellTemplateDirective, LeadFormComponent],
  templateUrl: './lead.list.html',
  styleUrl: './lead.list.scss'
})
export class LeadList implements OnInit {
  private readonly customerService = inject(CustomerService);
  
  readonly leads = signal<Lead[]>([]);
  readonly loading = signal(false);
  readonly showForm = signal(false);

  readonly columns: ColumnDef[] = [
    { field: 'name', title: 'Name', dataType: 'string' },
    { field: 'companyName', title: 'Company', dataType: 'string' },
    { field: 'email', title: 'Email', dataType: 'string' },
    { field: 'phone', title: 'Phone', dataType: 'string' },
    { field: 'source', title: 'Source', dataType: 'string' },
    { field: 'status', title: 'Status', dataType: 'string' },
    { field: 'actions', title: 'Actions', isTemplate: true }
  ];

  ngOnInit() {
    void this.loadLeads();
  }

  async loadLeads() {
    this.loading.set(true);
    try {
      const data = await this.customerService.getLeads();
      this.leads.set(data);
    } catch (err) {
      console.error('Failed to load leads', err);
    } finally {
      this.loading.set(false);
    }
  }

  async convertLead(lead: Lead) {
    try {
      await this.customerService.convertLead(lead.id);
      await this.loadLeads();
    } catch (err) {
      console.error('Failed to convert lead', err);
    }
  }

  onSaved() {
    this.showForm.set(false);
    void this.loadLeads();
  }
}
