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
    /* Positioned and as tall as the window under the bar, as the main shell's content cell is, so a page's
       full-page editor (the global .sheet, which fills its nearest positioned ancestor) fills the space
       under the bar and not the whole window. The padding is inside the height. */
    .content { position: relative; box-sizing: border-box; min-height: calc(100vh - 48px); padding: 2rem; }
  `]
})
export class AdminShellComponent {
  protected readonly auth = inject(AuthService);
}
