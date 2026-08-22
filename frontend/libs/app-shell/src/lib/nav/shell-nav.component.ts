import { Component, computed, inject, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '@bill-book/auth';

export interface NavItem {
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
 * 56px fixed left rail (z-index: 5, ink ground `--color-ink`).
 * Contains module navigation items, active cutout rule with 4px left accent rule,
 * bottom user profile menu, and responsive mobile bottom tab bar navigation (<860px).
 */
@Component({
  selector: 'bb-shell-nav',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './shell-nav.component.html',
  styleUrl: './shell-nav.component.scss',
})
export class ShellNavComponent {
  protected readonly auth = inject(AuthService);

  readonly userDisplayName = input<string>('Praba');
  readonly userRoleName = input<string>('Owner');

  readonly logout = output<void>();

  readonly allNavItems: NavItem[] = [
    { path: '/dashboard', label: 'Home', icon: 'home', module: null },
    { path: '/contacts', label: 'Contacts', icon: 'contacts', module: 'contacts' },
    { path: '/inventory', label: 'Inventory', icon: 'inventory', module: 'inventory' },
    { path: '/purchase', label: 'Purchase', icon: 'purchase', module: 'purchase' },
    { path: '/sales', label: 'Sales', icon: 'sales', module: 'sales' },
    { path: '/banking', label: 'Banking', icon: 'banking', module: 'banking' },
    { path: '/accounting', label: 'Accounts', icon: 'accounting', module: 'accounting' }, // STRICT UI RULE: Accounts
    { path: '/reports', label: 'Reports', icon: 'reports', module: 'reports' },
    { path: '/settings', label: 'Settings', icon: 'settings', module: 'settings' },
  ];

  /**
   * What this user can actually open based on permissions.
   */
  readonly nav = computed(() =>
    this.allNavItems.filter((item) => item.module === null || this.auth.canView(item.module)),
  );

  readonly primaryNav = computed(() =>
    this.nav().filter((item) => item.path !== '/settings'),
  );

  readonly settingsItem = computed(() =>
    this.nav().find((item) => item.path === '/settings'),
  );

  readonly mobileTopNav = computed(() =>
    this.nav().slice(0, 4),
  );

  readonly mobileMoreNav = computed(() =>
    this.nav().slice(4),
  );

  onLogout(): void {
    this.logout.emit();
  }
}
