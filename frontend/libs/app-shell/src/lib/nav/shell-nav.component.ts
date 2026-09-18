import { ChangeDetectionStrategy } from '@angular/core';
import { Component, computed, inject, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '@bill-book/auth';
import { MenuService } from '../menu.service';
import { FALLBACK_RAIL, ShellRailItem } from '../shell-screens';

/**
 * One entry in the rail. Kept as an exported name because the shell and its specs
 * refer to it; the shape lives with the fallback it is declared alongside.
 */
export type NavItem = ShellRailItem;

/**
 * 56px fixed left rail (z-index: 5, ink ground `--color-ink`).
 * Contains module navigation items, active cutout rule with 4px left accent rule,
 * bottom user profile menu, and responsive mobile bottom tab bar navigation (<860px).
 *
 * The rail is drawn from `GET /api/menu`: the server owns which modules exist,
 * their labels, their Lucide icons and where they lead. `FALLBACK_RAIL` covers the
 * gap before that call answers, and the case where it never does.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-shell-nav',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './shell-nav.component.html',
  styleUrl: './shell-nav.component.scss',
})
export class ShellNavComponent {
  protected readonly auth = inject(AuthService);
  private readonly menuService = inject(MenuService);

  readonly userDisplayName = input<string>('Praba');
  readonly userRoleName = input<string>('Owner');

  readonly logout = output<void>();

  /** What the rail shows until the server's tree arrives. */
  readonly allNavItems: readonly NavItem[] = FALLBACK_RAIL;

  /**
   * What this user can actually open.
   *
   * The server has already filtered its own tree to this role, so `canView`
   * changes nothing there — it still guards the fallback, which knows nothing
   * about who is signed in.
   */
  readonly nav = computed(() => {
    const fromServer = this.menuService.rail();
    const source = fromServer.length > 0 ? fromServer : this.allNavItems;
    return source.filter((item) => item.module === null || this.auth.canView(item.module));
  });

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
