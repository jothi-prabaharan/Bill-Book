import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'lib-api-clients-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './api-clients.list.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ApiClientsListComponent {
  private readonly http = inject(HttpClient);
  
  readonly clients = signal<any[]>([]);
  readonly newClientName = signal<string>('');
  readonly generatedKey = signal<string | null>(null);

  createClient() {
    if (!this.newClientName()) return;

    this.http.post<any>('/api/master/api-clients', { name: this.newClientName() })
      .subscribe(res => {
        this.generatedKey.set(res.apiKey);
        this.clients.update(c => [...c, res.apiClient]);
        this.newClientName.set('');
      });
  }
}
