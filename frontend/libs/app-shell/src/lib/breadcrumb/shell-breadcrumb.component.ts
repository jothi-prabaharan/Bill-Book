import { Component, computed, inject, input, output, signal } from '@angular/core';
import { Router, RouterLink, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';

export interface BreadcrumbItem {
  label: string;
  isLink: boolean;
  isLast: boolean;
  path?: string;
}

/**
 * Sticky breadcrumbs (`z-index: 4`) replacing `<h1>` headers.
 * Provides dynamic route path resolution and action projection host (`<ng-content select="[bbShellActions], .acts" />`).
 */
@Component({
  selector: 'bb-shell-breadcrumb',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './shell-breadcrumb.component.html',
  styleUrl: './shell-breadcrumb.component.scss',
})
export class ShellBreadcrumbComponent {
  private readonly router = inject(Router);

  readonly crumbsInput = input<BreadcrumbItem[] | null>(null);
  readonly crumbClick = output<BreadcrumbItem>();

  readonly crumbs = signal<BreadcrumbItem[]>([]);

  readonly effectiveCrumbs = computed(() => this.crumbsInput() ?? this.crumbs());

  readonly isHome = computed(() => {
    const url = this.router.url;
    return url === '/' || url === '/dashboard' || url.startsWith('/dashboard');
  });

  readonly isRegister = computed(() => {
    const url = this.router.url;
    return (
      url.includes('/sales') ||
      url.includes('/purchase') ||
      url.includes('/inventory') ||
      url.includes('/contacts')
    );
  });

  // Dashboard Actions State
  readonly base = signal(false);
  readonly baseLabel = signal('Accrual basis');
  readonly editing = signal(false);
  readonly notEditing = computed(() => !this.editing());

  constructor() {
    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe((event) => {
        this.updateCrumbs(event.urlAfterRedirects);
      });

    // Initialize crumbs
    setTimeout(() => this.updateCrumbs(this.router.url), 0);
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

  // Dashboard specific actions
  toggleBase(): void {
    this.base.update((v) => !v);
    this.baseLabel.set(this.base() ? 'Cash basis' : 'Accrual basis');
  }

  startEdit(): void {
    this.editing.set(true);
  }

  resetLayout(): void {
    // Reset layout hook
  }

  stopEdit(): void {
    this.editing.set(false);
  }

  // Register specific actions
  openExport(): void {
    // Open export hook
  }

  openImport(): void {
    // Open import hook
  }
}
