import { Component, inject } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '@bill-book/auth';

@Component({
  selector: 'bb-admin-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <header class="bar">
      <div class="brand">Bill-Book Admin</div>
      <nav class="nav">
        <a routerLink="/admin/customers" routerLinkActive="active">Customers</a>
        <a routerLink="/admin/rates" routerLinkActive="active">Rates</a>
      </nav>
      <a class="link" routerLink="/dashboard">Back to dashboard</a>
    </header>
    <main class="content">
      <router-outlet></router-outlet>
    </main>
  `,
  styles: [`
    .bar {
      display: flex;
      align-items: center;
      gap: 1rem;
      padding: 0 1.5rem;
      height: 48px;
      background: #1e293b;
      color: #f8fafc;
    }
    .brand { font-weight: 600; margin-right: 2rem; }
    .nav { display: flex; gap: 1rem; flex: 1; }
    .nav a { color: #cbd5e1; text-decoration: none; }
    .nav a.active { color: #fff; font-weight: 500; }
    .link { color: #cbd5e1; text-decoration: none; }
    .link:hover { color: #fff; text-decoration: underline; }
    .content { padding: 2rem; }
  `]
})
export class AdminShellComponent {
  protected readonly auth = inject(AuthService);
}
