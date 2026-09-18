import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter } from 'rxjs/operators';
import { SHELL_REGISTERS } from '../shell-screens';
import { MenuService } from '../menu.service';
import { ShellPanelService } from '../panel-state.service';

export interface BreadcrumbItem {
  label: string;
  isLink: boolean;
  isLast: boolean;
  path?: string;
}

/** One entry in the Export format menu. */
export interface ExportFormat {
  readonly label: string;
  readonly ext: string;
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
  protected readonly panel = inject(ShellPanelService);

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
  readonly hasPanel = computed(
    () => (this.menuService.menuForPath(this.url())?.groups.length ?? 0) > 0,
  );

  readonly panelShown = computed(() => this.hasPanel() && !this.panel.collapsed());

  readonly panelToggleLabel = computed(() => {
    const menu = this.menuService.menuForPath(this.url());
    if (!menu || !this.hasPanel()) return 'No menu for this screen';
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

  /** Emits the chosen format's extension — `pdf`, `xlsx`, `csv`. */
  readonly openExport = output<string>();
  readonly openImport = output<void>();

  readonly exportFormats: readonly ExportFormat[] = [
    { label: 'Portable document', ext: 'pdf' },
    { label: 'Excel workbook', ext: 'xlsx' },
    { label: 'Comma separated', ext: 'csv' },
  ];

  readonly exportMenuOpen = signal(false);

  constructor() {
    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe((event) => {
        this.url.set(event.urlAfterRedirects);
        this.updateCrumbs(event.urlAfterRedirects);
        this.exportMenuOpen.set(false);
      });

    this.url.set(this.router.url);
    this.updateCrumbs(this.router.url);
  }

  updateCrumbs(url: string): void {
    if (url === '/' || url.startsWith('/dashboard')) {
      this.crumbs.set([]);
      return;
    }

    const parts = url.split('?')[0].split('/').filter((p) => p);
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
      result.push({
        label,
        path: currentPath,
        isLink: !isLast,
        isLast,
      });
    }

    this.crumbs.set(result);
  }

  onCrumbClicked(crumb: BreadcrumbItem): void {
    this.crumbClick.emit(crumb);
  }

  togglePanel(): void {
    this.panel.toggle();
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

  toggleExportMenu(): void {
    this.exportMenuOpen.update((v) => !v);
  }

  pickExport(format: ExportFormat): void {
    this.exportMenuOpen.set(false);
    this.openExport.emit(format.ext);
  }

  onOpenImport(): void {
    this.openImport.emit();
  }
}
