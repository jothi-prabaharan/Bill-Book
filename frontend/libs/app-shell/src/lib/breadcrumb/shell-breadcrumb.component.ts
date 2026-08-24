import { ChangeDetectionStrategy } from '@angular/core';
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
changeDetection: ChangeDetectionStrategy.OnPush,
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

  readonly isHome = signal(false);
  readonly isRegister = signal(false);

  // Dashboard Actions State - Inputs mapped from parent
  readonly base = input(false);
  readonly baseLabel = input('Accrual basis');
  readonly editing = input(false);
  readonly notEditing = computed(() => !this.editing());

  // Outputs for dashboard actions
  readonly toggleBase = output<void>();
  readonly startEdit = output<void>();
  readonly resetLayout = output<void>();
  readonly stopEdit = output<void>();

  // Register specific actions
  readonly openExport = output<void>();
  readonly openImport = output<void>();

  // Methods for test compatibility - delegate to outputs
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

  onOpenExport(): void {
    this.openExport.emit();
  }

  onOpenImport(): void {
    this.openImport.emit();
  }

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
}

