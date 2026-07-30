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
  template: `
    <div class="shell">
      <nav class="rail" aria-label="Modules">
        @for (item of nav; track item.path) {
          <a [routerLink]="item.path" routerLinkActive="active" [title]="item.label">
            <span class="icon">{{ item.icon }}</span>
            <span class="label">{{ item.label }}</span>
          </a>
        }
        <div class="spacer"></div>
        <button type="button" class="rail-btn" title="Sign out" (click)="logout()">⎋</button>
      </nav>

      <main class="content">
        <router-outlet />
      </main>

      <nav class="tabs" aria-label="Modules">
        @for (item of nav.slice(0, 4); track item.path) {
          <a [routerLink]="item.path" routerLinkActive="active">
            <span class="icon">{{ item.icon }}</span>
            <span class="label">{{ item.label }}</span>
          </a>
        }
        <button type="button" (click)="moreOpen = !moreOpen">
          <span class="icon">⋯</span>
          <span class="label">More</span>
        </button>
      </nav>

      @if (moreOpen) {
        <div class="more-sheet" (click)="moreOpen = false">
          <div class="sheet-body">
            @for (item of nav.slice(4); track item.path) {
              <a [routerLink]="item.path" (click)="moreOpen = false">
                <span class="icon">{{ item.icon }}</span> {{ item.label }}
              </a>
            }
            <button type="button" (click)="logout()">⎋ Sign out</button>
          </div>
        </div>
      }
    </div>
  `,
  styles: `
    .shell { display: flex; height: 100dvh; }
    .content { flex: 1; overflow: auto; padding: 1rem; }

    .rail {
      display: flex; flex-direction: column; width: 64px; flex-shrink: 0;
      background: var(--bb-rail-bg, #1f2430); color: #fff; padding: .5rem 0;
    }
    .rail a, .rail .rail-btn {
      display: flex; flex-direction: column; align-items: center; gap: 2px;
      padding: .6rem 0; color: inherit; text-decoration: none;
      background: none; border: 0; cursor: pointer; font: inherit;
    }
    .rail .icon { font-size: 1.25rem; }
    .rail .label { font-size: .55rem; opacity: .75; }
    .rail a.active { background: rgba(255, 255, 255, .12); border-left: 3px solid #7aa2ff; }
    .rail .spacer { flex: 1; }

    .tabs { display: none; }
    .more-sheet {
      position: fixed; inset: 0; background: rgba(0, 0, 0, .4); z-index: 20;
    }
    .sheet-body {
      position: absolute; bottom: 0; left: 0; right: 0; background: #fff;
      border-radius: 12px 12px 0 0; padding: 1rem; display: grid; gap: .75rem;
    }
    .sheet-body a, .sheet-body button { text-align: left; font-size: 1rem; }

    @media (max-width: 767px) {
      .rail { display: none; }
      .content { padding-bottom: 64px; }
      .tabs {
        position: fixed; bottom: 0; left: 0; right: 0; height: 56px;
        display: flex; background: var(--bb-rail-bg, #1f2430); color: #fff; z-index: 10;
      }
      .tabs a, .tabs button {
        flex: 1; display: flex; flex-direction: column; align-items: center;
        justify-content: center; gap: 2px; color: inherit; text-decoration: none;
        background: none; border: 0; font: inherit; cursor: pointer;
      }
      .tabs .icon { font-size: 1.2rem; }
      .tabs .label { font-size: .6rem; }
      .tabs a.active { color: #7aa2ff; }
    }
  `,
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
