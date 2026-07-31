import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '@bill-book/auth';

interface NavItem {
  path: string;
  label: string;
  icon: string;
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
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  moreOpen = false;

  readonly nav: NavItem[] = [
    { path: '/dashboard', label: 'Home', icon: '🏠' },
    { path: '/sales', label: 'Sales', icon: '🧾' },
    { path: '/purchase', label: 'Purchase', icon: '📦' },
    { path: '/banking', label: 'Banking', icon: '🏦' },
    { path: '/contacts', label: 'Contacts', icon: '👥' },
    { path: '/inventory', label: 'Inventory', icon: '📋' },
    { path: '/accounting', label: 'Accounting', icon: '📒' },
    { path: '/reports', label: 'Reports', icon: '📊' },
    { path: '/settings', label: 'Settings', icon: '⚙️' },
  ];

  logout(): void {
    this.auth.logout();
    void this.router.navigateByUrl('/login');
  }
}
