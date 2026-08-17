import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  ReportMetadata,
  ReportQuery,
  ReportQueryService,
  ReportResult,
  REPORT_STATE_PARAM,
  decodeQuery,
  emptyQuery,
  encodeQuery,
} from '@bill-book/reporting-core';
import {
  ColumnChooserDialog,
  FilterBarComponent,
  GroupPanelComponent,
  ReportGridComponent,
} from '@bill-book/ui-components';

/**
 * Every report's screen.
 *
 * **One page, forty-five reports.** A report is data — a key, a column list, a
 * parameter set — so forty-five of them do not mean forty-five pages. One that
 * ever needs something this cannot express gets its own page and still hosts the
 * same grid.
 *
 * **This page owns the query state.** The grid and the panels are given it and
 * emit a changed copy; nothing below holds a second version, so two things can
 * never disagree about which page is showing.
 */
@Component({
  selector: 'bb-report-host-page',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    ReportGridComponent,
    FilterBarComponent,
    ColumnChooserDialog,
    GroupPanelComponent,
  ],
  templateUrl: './report-host.page.html',
  styleUrl: './report-host.page.scss',
})
export class ReportHostPage implements OnInit {
  private readonly reports = inject(ReportQueryService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly metadata = signal<ReportMetadata | null>(null);
  protected readonly result = signal<ReportResult | null>(null);
  protected readonly query = signal<ReportQuery>(emptyQuery());
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly choosingColumns = signal(false);
  protected readonly exporting = signal(false);

  private reportKey = '';

  protected readonly columns = computed(() => this.metadata()?.columns ?? []);

  ngOnInit(): void {
    this.reportKey = this.route.snapshot.paramMap.get('reportKey') ?? '';
    void this.load();
  }

  /**
   * Loads the report's metadata, then runs it.
   *
   * A layout in the URL wins over the report's defaults — that is what makes a
   * pasted link show what the sender was looking at.
   */
  async load(): Promise<void> {
    this.busy.set(true);
    this.message.set(null);

    try {
      const metadata = await this.reports.metadata(this.reportKey);
      this.metadata.set(metadata);

      const shared = decodeQuery(this.route.snapshot.queryParamMap.get(REPORT_STATE_PARAM));
      this.query.set(shared ?? emptyQuery(metadata));

      await this.run();
    } catch {
      this.message.set('Could not open this report.');
      this.busy.set(false);
    }
  }

  async run(): Promise<void> {
    this.busy.set(true);
    this.message.set(null);

    try {
      this.result.set(await this.reports.run(this.reportKey, this.query()));
    } catch (error: unknown) {
      // The server's message is written for the person who asked — a column that
      // does not exist, a value that will not parse — so it is shown rather than
      // replaced with something generic.
      this.message.set(this.readServerMessage(error) ?? 'Could not run this report.');
      this.result.set(null);
    } finally {
      this.busy.set(false);
    }
  }

  /**
   * Any change re-runs the report and rewrites the URL.
   *
   * `replaceUrl` so a filter does not add a history entry per keystroke — the
   * back button should leave the report, not walk back through every column
   * somebody toggled.
   */
  protected async onStateChange(query: ReportQuery): Promise<void> {
    this.query.set(query);

    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { [REPORT_STATE_PARAM]: encodeQuery(query) },
      replaceUrl: true,
    });

    await this.run();
  }

  protected onFiltersChange(filters: ReportQuery['filters']): void {
    // Back to the first page: a filter that shrinks the result to two pages while
    // the reader sits on page nine shows them nothing and looks broken.
    void this.onStateChange({
      ...this.query(),
      filters,
      page: { ...this.query().page, number: 1, includeCount: true },
    });
  }

  protected onGroupByChange(groupBy: string[]): void {
    void this.onStateChange({ ...this.query(), groupBy });
  }

  protected onColumnsChange(columns: string[]): void {
    void this.onStateChange({ ...this.query(), columns });
  }

  protected onParameterChange(name: string, value: string): void {
    void this.onStateChange({
      ...this.query(),
      parameters: { ...this.query().parameters, [name]: value },
      page: { ...this.query().page, number: 1, includeCount: true },
    });
  }

  protected async exportToExcel(): Promise<void> {
    this.exporting.set(true);
    this.message.set(null);

    try {
      const file = await this.reports.export(this.reportKey, this.query());

      this.save(file, `${this.reportKey}.xlsx`);
    } catch (error: unknown) {
      this.message.set(this.readServerMessage(error) ?? 'Could not export this report.');
    } finally {
      this.exporting.set(false);
    }
  }

  /**
   * Hands the file to the browser.
   *
   * This is why the service returns a blob rather than saving it: the download
   * dance is the shell's business, and it lives here in the web app rather than
   * in `reporting-core`, which must stay free of browser globals to run under
   * Ionic and Electron.
   */
  private save(file: Blob, name: string): void {
    const url = URL.createObjectURL(file);
    const link = document.createElement('a');

    link.href = url;
    link.download = name;
    link.click();

    URL.revokeObjectURL(url);
  }

  /** The `{ message }` an API error carries, when it carries one. */
  private readServerMessage(error: unknown): string | null {
    const body = (error as { error?: { message?: string } } | null)?.error;

    return typeof body?.message === 'string' ? body.message : null;
  }
}
