import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '@bill-book/auth';

interface NavItem {
  path: string;
  label: string;
  icon: string;
  /**
   * The module this entry leads to, or null for one every role can reach.
   * Drawn only when the user holds `{module}.view`.
   */
  module: string | null;
}

/**
 * Teams-style shell. Desktop (≥768px): far-left icon rail + main area.
 * Mobile: bottom tab bar with the top 4 modules + "More" — the Teams-mobile
 * pattern. Same nav model both ways; only the chrome changes.
 */
@Component({
  selector: 'bb-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  private readonly all: NavItem[] = [
    // Home has no module: it is where everyone lands, including roles with no
    // dashboard permission, and it holds nothing of its own to protect.
    { path: '/dashboard', label: 'Home', icon: '🏠', module: null },
    { path: '/sales', label: 'Sales', icon: '🧾', module: 'sales' },
    { path: '/purchase', label: 'Purchase', icon: '📦', module: 'purchase' },
    { path: '/banking', label: 'Banking', icon: '🏦', module: 'banking' },
    { path: '/contacts', label: 'Contacts', icon: '👥', module: 'contacts' },
    { path: '/inventory', label: 'Inventory', icon: '📋', module: 'inventory' },
    { path: '/accounting', label: 'Accounting', icon: '📒', module: 'accounting' },
    { path: '/reports', label: 'Reports', icon: '📊', module: 'reports' },
    { path: '/settings', label: 'Settings', icon: '⚙️', module: 'settings' },
  ];

  /**
   * What this user can actually open. Offering a screen that answers 403 is a
   * menu lying about the product; the server was already refusing these, so
   * this only changes what is drawn.
   */
  readonly nav = computed(() =>
    this.all.filter((item) => item.module === null || this.auth.canView(item.module)),
  );

  logout(): void {
    this.auth.logout();
    void this.router.navigateByUrl('/login');
  }
}
