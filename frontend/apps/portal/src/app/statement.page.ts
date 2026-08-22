import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { HttpClient, HttpHeaders } from '@angular/common/http';

interface StatementTransaction {
  ledgerDate: string;
  transactionNo: string;
  reference: string;
  description: string;
  debit: number;
  credit: number;
  balance: number;
}

interface StatementResponse {
  openingBalance: number;
  transactions: StatementTransaction[];
  closingBalance: number;
}

@Component({
  selector: 'bb-portal-statement',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './statement.page.html',
  styleUrl: './statement.page.scss',
})
export class StatementPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly http = inject(HttpClient);

  readonly statement = signal<StatementResponse | null>(null);
  readonly error = signal<string | null>(null);
  readonly loading = signal<boolean>(true);

  // In a real app, environment.apiUrl would be used, but for simplicity here we just use the gateway URL directly or relative path if hosted together
  private readonly apiUrl = 'http://localhost:5000/api'; // Or use proxy

  ngOnInit() {
    this.route.queryParams.subscribe((params) => {
      const token = params['token'];
      if (!token) {
        this.error.set('No token provided. Please use the secure link sent to you.');
        this.loading.set(false);
        return;
      }
      // Fire and forget: the subscribe callback returns void, and a promise
      // handed back to it is one nobody awaits or catches.
      void this.fetchStatement(token);
    });
  }

  async fetchStatement(token: string) {
    this.loading.set(true);
    this.error.set(null);
    try {
      // Use query params for dates if needed, for now we fetch all
      const headers = new HttpHeaders().set('Authorization', `Bearer ${token}`);
      
      const response = await this.http.get<StatementResponse>(`${this.apiUrl}/portal/statements`, { headers }).toPromise();
      if (response) {
        this.statement.set(response);
      } else {
        this.error.set('Could not load statement.');
      }
    } catch (err: any) {
      if (err.status === 401 || err.status === 403) {
        this.error.set('The link is invalid or has expired.');
      } else {
        this.error.set('An error occurred while loading the statement.');
      }
    } finally {
      this.loading.set(false);
    }
  }
}
