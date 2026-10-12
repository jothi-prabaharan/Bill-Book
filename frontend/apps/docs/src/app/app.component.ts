import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { DOC_GROUPS, DocGroup, DocPage, DocSection } from './docs.manifest';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-docs-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent {
  private readonly router = inject(Router);

  readonly docGroups = DOC_GROUPS;
  readonly searchQuery = signal<string>('');

  // Set of section titles that are currently expanded
  readonly expandedSections = signal<Set<string>>(
    new Set(DOC_GROUPS.flatMap((g) => g.sections.map((s) => s.title)))
  );

  // Filtered doc groups based on search input
  readonly filteredGroups = computed<DocGroup[]>(() => {
    const q = this.searchQuery().trim().toLowerCase();
    if (!q) {
      return this.docGroups;
    }

    return this.docGroups
      .map((group) => {
        const matchingSections = group.sections
          .map((sec) => {
            const matchesSecTitle = sec.title.toLowerCase().includes(q);
            const matchingPages = sec.pages.filter(
              (p) => matchesSecTitle || p.title.toLowerCase().includes(q) || p.slug.toLowerCase().includes(q)
            );
            return {
              ...sec,
              pages: matchingPages,
            };
          })
          .filter((sec) => sec.pages.length > 0);

        return {
          ...group,
          sections: matchingSections,
        };
      })
      .filter((group) => group.sections.length > 0);
  });

  constructor() {
    // Keep active route's parent section expanded
    this.router.events.subscribe((event) => {
      if (event instanceof NavigationEnd) {
        this.expandSectionForUrl(event.urlAfterRedirects || event.url);
      }
    });
  }

  isSectionExpanded(title: string): boolean {
    // If user is searching, always expand matching sections
    if (this.searchQuery().trim().length > 0) {
      return true;
    }
    return this.expandedSections().has(title);
  }

  toggleSection(title: string, event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    const current = new Set(this.expandedSections());
    if (current.has(title)) {
      current.delete(title);
    } else {
      current.add(title);
    }
    this.expandedSections.set(current);
  }

  expandAll(): void {
    const all = new Set(this.docGroups.flatMap((g) => g.sections.map((s) => s.title)));
    this.expandedSections.set(all);
  }

  collapseAll(): void {
    this.expandedSections.set(new Set());
  }

  onSearchInput(event: Event): void {
    const target = event.target as HTMLInputElement;
    this.searchQuery.set(target.value);
  }

  clearSearch(): void {
    this.searchQuery.set('');
  }

  private expandSectionForUrl(url: string): void {
    const cleanSlug = url.replace(/^\//, '').split('?')[0].split('#')[0];
    if (!cleanSlug) return;

    for (const group of this.docGroups) {
      for (const section of group.sections) {
        if (section.pages.some((p) => p.slug === cleanSlug)) {
          const current = new Set(this.expandedSections());
          if (!current.has(section.title)) {
            current.add(section.title);
            this.expandedSections.set(current);
          }
          return;
        }
      }
    }
  }
}
