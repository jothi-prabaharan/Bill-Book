import { CardTableComponent } from '@bill-book/ui-components';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface HsnSacCode {
  hsnSacCodeId: number;
  code: string;
  codeType: string;
  description: string;
  chapterCode: string | null;
  defaultGstRate: number | null;
  digitLength: number;
  isActive: boolean;
}

interface CodePage {
  total: number;
  skip: number;
  rows: HsnSacCode[];
}

interface Coverage {
  hsnChapters: number;
  hsnCodes: number;
  sacGroups: number;
  sacCodes: number;
  inactive: number;
}

const PAGE_SIZE = 50;

/**
 * Settings › HSN & SAC codes.
 *
 * **Read-only, and that is the design rather than an unfinished page.** These
 * codes are notified by the CBIC, identical for every business in the country,
 * so they live in the master database — one table shared by every customer on
 * the platform. A customer editing a row here would be editing it for all of
 * them. The detailed list is loaded by the operator from the official file;
 * what a customer needs from this screen is to find a code and see the rate it
 * usually attracts.
 *
 * The coverage strip at the top exists because the failure it describes is
 * otherwise unreadable: only the 97 chapters and 32 SAC groups ship as seed
 * data, and until the CBIC file is imported a search for a real 8-digit code
 * returns nothing at all. Without a count on screen that looks like a broken
 * search rather than an empty table.
 */
@Component({
  selector: 'bb-hsn-sac-page',
  standalone: true,
  imports: [CardTableComponent, FormsModule],
  templateUrl: './hsn-sac.page.html',
  styleUrl: './hsn-sac.page.scss',
})
export class HsnSacPage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly rows = signal<HsnSacCode[]>([]);
  protected readonly total = signal(0);
  protected readonly skip = signal(0);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly coverage = signal<Coverage | null>(null);

  protected readonly pageSize = PAGE_SIZE;

  /** True while the detailed nomenclature has not been imported. */
  protected readonly awaitingImport = computed(() => {
    const c = this.coverage();
    return c !== null && c.hsnCodes === 0;
  });

  protected readonly showingFrom = computed(() =>
    this.total() === 0 ? 0 : this.skip() + 1,
  );
  protected readonly showingTo = computed(() =>
    Math.min(this.skip() + this.rows().length, this.total()),
  );
  protected readonly hasPrevious = computed(() => this.skip() > 0);
  protected readonly hasNext = computed(() => this.skip() + this.rows().length < this.total());

  search = '';
  codeType = '';
  includeChapters = false;
  includeInactive = false;

  ngOnInit(): void {
    void this.load();
    void this.loadCoverage();
  }

  /** Any filter change restarts at the first page — page 4 of a new search is meaningless. */
  async applyFilters(): Promise<void> {
    this.skip.set(0);
    await this.load();
  }

  async previous(): Promise<void> {
    this.skip.set(Math.max(0, this.skip() - PAGE_SIZE));
    await this.load();
  }

  async next(): Promise<void> {
    this.skip.set(this.skip() + PAGE_SIZE);
    await this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);

    const query = new URLSearchParams({
      includeChapters: String(this.includeChapters),
      includeInactive: String(this.includeInactive),
      skip: String(this.skip()),
      take: String(PAGE_SIZE),
    });

    if (this.search.trim()) {
      query.set('search', this.search.trim());
    }
    if (this.codeType) {
      query.set('codeType', this.codeType);
    }

    try {
      const page = await this.get<CodePage>(`/api/master/hsn-sac?${query.toString()}`);
      this.rows.set(page.rows);
      this.total.set(page.total);
    } catch {
      this.error.set('Could not load codes.');
      this.rows.set([]);
      this.total.set(0);
    } finally {
      this.busy.set(false);
    }
  }

  private async loadCoverage(): Promise<void> {
    try {
      this.coverage.set(await this.get<Coverage>('/api/master/hsn-sac/summary'));
    } catch {
      // A missing count is not worth an error banner over the list itself —
      // the strip simply does not render.
      this.coverage.set(null);
    }
  }

  private get<T>(url: string): Promise<T> {
    return new Promise((resolve, reject) => {
      this.http.get<T>(url).subscribe({ next: resolve, error: reject });
    });
  }
}
