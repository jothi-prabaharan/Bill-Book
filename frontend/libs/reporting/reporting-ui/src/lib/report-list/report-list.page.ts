import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ReportCatalogItem, ReportQueryService } from '@bill-book/reporting-core';

/**
 * The reports screen: everything this user may run, grouped by module.
 *
 * **Reports the user lacks the permission for are absent, not greyed out.** The
 * server leaves them out of the catalog, and a list of things you cannot have is
 * a worse screen besides.
 */
@Component({
  selector: 'bb-report-list-page',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './report-list.page.html',
  styleUrl: './report-list.page.scss',
})
export class ReportListPage implements OnInit {
  private readonly reports = inject(ReportQueryService);

  protected readonly catalog = signal<ReportCatalogItem[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);

  /** Grouped by module, each module's reports in their seeded order. */
  protected readonly grouped = computed(() => {
    const modules = new Map<string, ReportCatalogItem[]>();

    for (const report of this.catalog()) {
      const existing = modules.get(report.module) ?? [];
      modules.set(report.module, [...existing, report]);
    }

    return [...modules.entries()].map(([module, reports]) => ({ module, reports }));
  });

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    this.message.set(null);

    try {
      this.catalog.set(await this.reports.catalog());
    } catch {
      this.message.set('Could not load the list of reports.');
    } finally {
      this.busy.set(false);
    }
  }
}
