import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { AuthService } from '@bill-book/auth';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter } from 'rxjs/operators';
import { SHELL_REGISTERS } from '../shell-screens';
import { MenuService } from '../menu.service';
import { ShellPanelService } from '../panel-state.service';
import { ShellNavigationService } from '../shell-navigation.service';

export interface BreadcrumbItem {
  label: string;
  isLink: boolean;
  isLast: boolean;
  path?: string;
}



/**
 * The crumbs for a URL: one per path segment, the last one not a link. The
 * shell and the strip both build them here, so a rule is written once.
 */
export function buildCrumbs(url: string): BreadcrumbItem[] {
  if (url === '/' || url.startsWith('/dashboard')) {
    return [];
  }

  const [path, query] = url.split('?');
  const parts = path.split('/').filter((p) => p);
  const result: BreadcrumbItem[] = [];
  let currentPath = '';

  for (let i = 0; i < parts.length; i++) {
    const part = parts[i];
    currentPath += '/' + part;

    let label = part.replace(/-/g, ' ');
    label = label.charAt(0).toUpperCase() + label.slice(1);

    // Special case expansions and strict UI rule for Accounts
    if (label.toLowerCase() === 'coa') {
      label = 'Chart of Accounts';
    } else if (label.toLowerCase() === 'accounting') {
      label = 'Accounts'; // STRICT: "Accounts"
    }

    const isLast = i === parts.length - 1;

    // A new record in a role (TK-188): /contacts/new?role=vendor reads
    // "New vendor", as the form's own heading does.
    if (isLast && part === 'new') {
      const role = new URLSearchParams(query ?? '').get('role');
      if (role) {
        label = `New ${role === 'jobworker' ? 'job worker' : role.toLowerCase()}`;
      }
    }

    result.push({
      label,
      path: currentPath,
      isLink: !isLast,
      isLast,
    });
  }

  return result;
}

/**
 * The breadcrumb strip (`z-index: 4`). It replaces page titles outright — no
 * module screen renders a heading of its own — and it is where module-level
 * controls live: Export, Import, the board's basis toggle and Customize, and
 * the star that puts this screen in Favourites.
 *
 * Also a projection host: `<ng-content select="[bbShellActions], .acts" />`.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-shell-breadcrumb',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './shell-breadcrumb.component.html',
  styleUrl: './shell-breadcrumb.component.scss',
})
export class ShellBreadcrumbComponent {
  private readonly router = inject(Router);
  private readonly menuService = inject(MenuService);
  protected readonly auth = inject(AuthService);
  protected readonly panel = inject(ShellPanelService);
  private readonly navigation = inject(ShellNavigationService);

  readonly crumbsInput = input<BreadcrumbItem[] | null>(null);
  readonly crumbClick = output<BreadcrumbItem>();

  readonly crumbs = signal<BreadcrumbItem[]>([]);

  readonly effectiveCrumbs = computed(() => this.crumbsInput() ?? this.crumbs());

  /** The current URL, kept as a signal so the strip's controls follow the route. */
  private readonly url = signal('/');

  readonly isHome = computed(() => this.url() === '/' || this.url().startsWith('/dashboard'));

  readonly isRegister = computed(() => SHELL_REGISTERS.includes(this.url().split('?')[0]));

  /**
   * Whether this module has a secondary menu at all. Without one the toggle greys
   * out rather than disappearing, so the strip does not reflow as you move between
   * modules that have a panel and modules that do not.
   */
  readonly hasPanel = computed(() => (this.navigation.panelMenu()?.groups.length ?? 0) > 0);

  /** Whether the secondary menu is showing: the panel on a desktop, the sheet on a phone. */
  readonly panelShown = computed(() => this.hasPanel() && this.navigation.panelShown());

  readonly panelToggleLabel = computed(() => {
    const menu = this.navigation.panelMenu();
    if (!menu) return 'No menu for this screen';
    if (!this.hasPanel()) return `No menu for ${menu.name}`;
    return this.panelShown() ? `Hide ${menu.name} menu` : `Show ${menu.name} menu`;
  });

  // Board controls, driven by the shell.
  readonly base = input(false);
  readonly baseLabel = input('Accrual basis');
  readonly editing = input(false);
  readonly notEditing = computed(() => !this.editing());

  // Favourites star for the current screen.
  readonly starred = input(false);
  readonly starrable = input(false);
  readonly toggleStar = output<void>();

  readonly toggleBase = output<void>();
  readonly startEdit = output<void>();
  readonly resetLayout = output<void>();
  readonly stopEdit = output<void>();







  constructor() {
    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe((event) => {
        this.url.set(event.urlAfterRedirects);
        this.updateCrumbs(event.urlAfterRedirects);

      });

    this.url.set(this.router.url);
    this.updateCrumbs(this.router.url);
  }

  updateCrumbs(url: string): void {
    this.crumbs.set(this.menuService.crumbsFor(url) ?? buildCrumbs(url));
  }

  onCrumbClicked(crumb: BreadcrumbItem): void {
    this.crumbClick.emit(crumb);
  }

  togglePanel(): void {
    this.navigation.togglePanel();
  }

  onToggleStar(): void {
    this.toggleStar.emit();
  }

  onToggleBase(): void {
    this.toggleBase.emit();
  }

  onStartEdit(): void {
    this.startEdit.emit();
  }

  onResetLayout(): void {
    this.resetLayout.emit();
  }

  onStopEdit(): void {
    this.stopEdit.emit();
  }


}


