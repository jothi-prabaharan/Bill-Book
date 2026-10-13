import { ChangeDetectionStrategy, Component, ElementRef, ViewChild, computed, inject, signal } from '@angular/core';
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

  @ViewChild('contentViewport') contentViewport?: ElementRef<HTMLElement>;

  readonly docGroups = DOC_GROUPS;
  readonly searchQuery = signal<string>('');

  // Active expanded group name - only ONE group open at a time (accordion)
  readonly openGroupName = signal<string>('Getting Started');

  // Active expanded section title - only ONE section open at a time within group
  readonly openSectionTitle = signal<string | null>('Overview');

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
    // Keep active route's parent group & section open and scroll page to top
    this.router.events.subscribe((event) => {
      if (event instanceof NavigationEnd) {
        this.expandSectionForUrl(event.urlAfterRedirects || event.url);
        this.scrollToTop();
        setTimeout(() => this.scrollToTop(), 0);
        setTimeout(() => this.scrollToTop(), 60);
      }
    });
  }

  scrollToTop(): void {
    if (typeof window !== 'undefined') {
      window.scrollTo({ top: 0, left: 0, behavior: 'instant' });
    }
    if (this.contentViewport?.nativeElement) {
      this.contentViewport.nativeElement.scrollTo({ top: 0, left: 0, behavior: 'instant' });
      this.contentViewport.nativeElement.scrollTop = 0;
    }
    if (typeof document !== 'undefined') {
      const el = document.querySelector('.content-viewport');
      if (el) {
        el.scrollTo({ top: 0, left: 0, behavior: 'instant' });
        el.scrollTop = 0;
      }
      if (document.documentElement) {
        document.documentElement.scrollTop = 0;
      }
      if (document.body) {
        document.body.scrollTop = 0;
      }
    }
  }

  isGroupExpanded(name: string): boolean {
    // While searching, expand all groups that match
    if (this.searchQuery().trim().length > 0) {
      return true;
    }
    return this.openGroupName() === name;
  }

  toggleGroup(name: string, event?: Event): void {
    if (event) {
      event.preventDefault();
      event.stopPropagation();
    }
    // Accordion: clicking another group closes the current one; clicking open group toggles it
    if (this.openGroupName() === name) {
      this.openGroupName.set('');
    } else {
      this.openGroupName.set(name);
      // Auto-open first section in newly opened group
      const grp = this.docGroups.find((g) => g.name === name);
      if (grp && grp.sections.length > 0) {
        this.openSectionTitle.set(grp.sections[0].title);
      }
    }
  }

  isSectionExpanded(title: string): boolean {
    // While searching, expand all matching sections
    if (this.searchQuery().trim().length > 0) {
      return true;
    }
    return this.openSectionTitle() === title;
  }

  toggleSection(title: string, groupName: string, event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    // Ensure parent group is open
    this.openGroupName.set(groupName);

    // Accordion: clicking another section closes any previously open section
    if (this.openSectionTitle() === title) {
      this.openSectionTitle.set(null);
    } else {
      this.openSectionTitle.set(title);
    }
  }

  collapseAll(): void {
    this.openGroupName.set('');
    this.openSectionTitle.set(null);
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
          this.openGroupName.set(group.name);
          this.openSectionTitle.set(section.title);
          return;
        }
      }
    }
  }
}
