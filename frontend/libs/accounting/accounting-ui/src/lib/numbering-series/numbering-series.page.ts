import { ChangeDetectionStrategy } from '@angular/core';
import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DataGridComponent, ColumnDef , TextInputComponent , NumberInputComponent } from '@bill-book/ui-components';

interface NumberingSeries {
  numberingSeriesId: number;
  seriesSystemName: string | null;
  seriesCode: string;
  seriesName: string;
  seriesFor: 'Master' | 'Document';
  prefix: string | null;
  suffix: string | null;
  separator: string | null;
  includeFinancialYear: boolean;
  financialYearFormat: 'FullYearRange' | 'ShortYearRange' | 'Compact' | 'StartYear';
  includeBranchCode: boolean;
  branchCode: string | null;
  numberLength: number;
  startNumber: number;
  nextNumber: number;
  resetFrequency: 'Never' | 'Yearly' | 'Monthly' | 'Daily';
  lastResetOn: string | null;
  allowManualOverride: boolean;
  isDefault: boolean;
  isSystem: boolean;
  isActive: boolean;
  displayOrder: number;
  preview: string;
}

type FormModel = Omit<
  NumberingSeries,
  | 'numberingSeriesId'
  | 'seriesSystemName'
  | 'nextNumber'
  | 'lastResetOn'
  | 'isDefault'
  | 'isSystem'
  | 'displayOrder'
  | 'preview'
>;

/**
 * Settings › Numbering series. Every generated code in the product comes from
 * here — contact codes, item codes and, as those services land, document
 * numbers.
 *
 * Two things the screen is careful about. The preview is rendered live from the
 * same rules the server composes with, because a wrong prefix is only obvious
 * once you see the result. And the counter is moved from its own action rather
 * than as a field on the form, since it is the one edit that can collide with
 * codes already issued.
 */
changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-numbering-series-page',
  standalone: true,
  imports: [DataGridComponent, FormsModule, TextInputComponent, NumberInputComponent],
  templateUrl: './numbering-series.page.html',
  styleUrl: './numbering-series.page.scss',
})
export class NumberingSeriesPage implements OnInit {
  private readonly http = inject(HttpClient);

  columns: ColumnDef[] = [
    { field: 'handle', header: '' },
    { field: 'seriesName', header: 'Name' },
    { field: 'seriesCode', header: 'Code' },
    { field: 'seriesFor', header: 'For' },
    { field: 'preview', header: 'Preview' },
    { field: 'nextNumber', header: 'Next' },
    { field: 'resetFrequency', header: 'Reset' },
    { field: 'isDefault', header: 'Default' },
    { field: 'isActive', header: 'Active' },
    { field: 'actions', header: 'Actions' }
  ];

  private readonly orgId = signal<string>(localStorage.getItem('bb.orgId') ?? '');

  /**
   * The branch's own start month, read from its settings. April until it
   * loads — the preview would otherwise show a year segment for a financial
   * year this branch does not use.
   */
  protected readonly financialYearStartMonth = signal(4);

  protected readonly rows = signal<NumberingSeries[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);
  protected readonly editing = signal<number | null>(null);
  protected readonly formOpen = signal(false);
  protected readonly counterFor = signal<NumberingSeries | null>(null);

  showInactive = false;
  filter: '' | 'Master' | 'Document' = '';
  counterValue = 1;

  form: FormModel = this.blank();

  /** Mirrors NumberFormat.Compose on the server, so the preview cannot drift from reality. */
  protected readonly preview = computed(() => this.compose(this.form, this.form.startNumber));

  ngOnInit(): void {
    void this.load();
    void this.loadFinancialYear();
  }

  /** The preview has to compose against the same month the server generates with. */
  private async loadFinancialYear(): Promise<void> {
    try {
      const org = await this.get<{ financialYearStartMonth: number }>(
        `/api/organizations/${this.orgId()}`,
      );

      if (org.financialYearStartMonth >= 1 && org.financialYearStartMonth <= 12) {
        this.financialYearStartMonth.set(org.financialYearStartMonth);
      }
    } catch {
      // The server composes the real number regardless; only the preview here
      // would be a month out, and April is right for most branches.
    }
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      const query = `includeInactive=${this.showInactive}` + (this.filter ? `&seriesFor=${this.filter}` : '');
      this.rows.set(await this.get<NumberingSeries[]>(`/api/numbering-series?${query}`));
    } catch {
      this.fail('Could not load the numbering series.');
    } finally {
      this.busy.set(false);
    }
  }

  openAdd(): void {
    this.editing.set(null);
    this.form = this.blank();
    this.formOpen.set(true);
  }

  openEdit(row: NumberingSeries): void {
    this.editing.set(row.numberingSeriesId);
    this.form = {
      seriesCode: row.seriesCode,
      seriesName: row.seriesName,
      seriesFor: row.seriesFor,
      prefix: row.prefix,
      suffix: row.suffix,
      separator: row.separator,
      includeFinancialYear: row.includeFinancialYear,
      financialYearFormat: row.financialYearFormat,
      includeBranchCode: row.includeBranchCode,
      branchCode: row.branchCode,
      numberLength: row.numberLength,
      startNumber: row.startNumber,
      resetFrequency: row.resetFrequency,
      allowManualOverride: row.allowManualOverride,
      isActive: row.isActive,
    };
    this.formOpen.set(true);
  }

  /** A document series must stay consecutive, so the override option goes away with it. */
  onSeriesForChange(): void {
    if (this.form.seriesFor === 'Document') {
      this.form.allowManualOverride = false;
      this.form.includeFinancialYear = true;
      this.form.resetFrequency = 'Yearly';
    }
  }

  async save(): Promise<void> {
    if (!this.validateForm()) {
      return;
    }

    const body: FormModel = {
      ...this.form,
      seriesCode: this.form.seriesCode.trim(),
      seriesName: this.form.seriesName.trim(),
      prefix: this.cleanOptional(this.form.prefix),
      suffix: this.cleanOptional(this.form.suffix),
      separator: this.cleanOptional(this.form.separator),
      branchCode: this.form.includeBranchCode ? this.cleanOptional(this.form.branchCode) : null,
    };

    this.busy.set(true);
    try {
      const id = this.editing();
      if (id === null) {
        await this.send('POST', '/api/numbering-series', body);
      } else {
        await this.send('PUT', `/api/numbering-series/${id}`, body);
      }
      this.formOpen.set(false);
      this.succeed('Series saved.');
      await this.load();
    } catch (err: unknown) {
      this.fail(this.messageOf(err, 'Could not save that series.'));
    } finally {
      this.busy.set(false);
    }
  }

  openCounter(row: NumberingSeries): void {
    this.counterFor.set(row);
    this.counterValue = row.nextNumber;
  }

  async saveCounter(): Promise<void> {
    const row = this.counterFor();
    if (!row) {
      return;
    }

    this.busy.set(true);
    try {
      const result = await this.send<{ message?: string } | null>(
        'PUT',
        `/api/numbering-series/${row.numberingSeriesId}/next-number`,
        { nextNumber: this.counterValue },
      );
      this.counterFor.set(null);
      // A lowered counter comes back with a warning rather than a plain 204.
      if (result?.message) {
        this.warn(result.message);
      } else {
        this.succeed('Counter updated.');
      }
      await this.load();
    } catch (err: unknown) {
      this.fail(this.messageOf(err, 'Could not update the counter.'));
    } finally {
      this.busy.set(false);
    }
  }

  async makeDefault(row: NumberingSeries): Promise<void> {
    await this.act(`/api/numbering-series/${row.numberingSeriesId}/default`, 'PUT', 'Default changed.');
  }

  async deactivate(row: NumberingSeries): Promise<void> {
    await this.act(
      `/api/numbering-series/${row.numberingSeriesId}`,
      'DELETE',
      'Series deactivated.',
    );
  }

  /**
   * Sends the drop as its neighbours rather than an index, so the server picks
   * the value and two people dragging at once cannot both compute the same one.
   */
  async onDrop(event: CdkDragDrop<NumberingSeries[]>): Promise<void> {
    if (event.previousIndex === event.currentIndex) {
      return;
    }

    const list = [...this.rows()];
    const [moved] = list.splice(event.previousIndex, 1);
    list.splice(event.currentIndex, 0, moved);
    this.rows.set(list);

    const previous = list[event.currentIndex - 1] ?? null;
    const next = list[event.currentIndex + 1] ?? null;

    this.busy.set(true);
    try {
      await this.send('PATCH', '/api/numbering-series/reorder', {
        movedId: moved.numberingSeriesId,
        previousId: previous?.numberingSeriesId ?? null,
        nextId: next?.numberingSeriesId ?? null,
      });
      await this.load();
    } catch {
      this.fail('Could not save the new order.');
      await this.load();
    } finally {
      this.busy.set(false);
    }
  }

  /** Reordering only means something while the list is in its stored order. */
  protected get canReorder(): boolean {
    return !this.showInactive;
  }

  private compose(form: FormModel, sample: number): string {
    const segments: string[] = [];

    if (form.prefix?.trim()) {
      segments.push(form.prefix);
    }

    if (form.includeFinancialYear) {
      segments.push(this.financialYear(form.financialYearFormat));
    }

    if (form.includeBranchCode && form.branchCode?.trim()) {
      segments.push(form.branchCode);
    }

    const length = Math.min(Math.max(form.numberLength || 1, 1), 12);
    segments.push(String(Math.max(sample, 0)).padStart(length, '0'));

    return segments.join(form.separator ?? '') + (form.suffix ?? '');
  }

  private validateForm(): boolean {
    if (!this.hasText(this.form.seriesCode) || !this.hasText(this.form.seriesName)) {
      this.fail('Series code and name are required.');
      return false;
    }

    if (this.form.includeBranchCode && !this.hasText(this.form.branchCode)) {
      this.fail('Branch code is required when Include branch code is selected.');
      return false;
    }

    return true;
  }

  private financialYear(format: FormModel['financialYearFormat']): string {
    const today = new Date();
    const start =
      today.getMonth() + 1 >= this.financialYearStartMonth()
        ? today.getFullYear()
        : today.getFullYear() - 1;
    const end = start + 1;
    const two = (year: number): string => String(year % 100).padStart(2, '0');

    switch (format) {
      case 'FullYearRange':
        return `${start}-${two(end)}`;
      case 'ShortYearRange':
        return `${two(start)}-${two(end)}`;
      case 'StartYear':
        return String(start);
      default:
        return `${two(start)}${two(end)}`;
    }
  }

  private blank(): FormModel {
    return {
      seriesCode: '',
      seriesName: '',
      seriesFor: 'Master',
      prefix: '',
      suffix: '',
      separator: '-',
      includeFinancialYear: false,
      financialYearFormat: 'Compact',
      includeBranchCode: false,
      branchCode: null,
      numberLength: 5,
      startNumber: 1,
      resetFrequency: 'Never',
      allowManualOverride: true,
      isActive: true,
    };
  }

  private async act(url: string, method: 'PUT' | 'DELETE', ok: string): Promise<void> {
    this.busy.set(true);
    try {
      await this.send(method, url, {});
      this.succeed(ok);
      await this.load();
    } catch (err: unknown) {
      this.fail(this.messageOf(err, 'That did not work.'));
    } finally {
      this.busy.set(false);
    }
  }

  private succeed(text: string): void {
    this.message.set(text);
    this.messageIsError.set(false);
  }

  private warn(text: string): void {
    this.message.set(text);
    this.messageIsError.set(true);
  }

  private fail(text: string): void {
    this.message.set(text);
    this.messageIsError.set(true);
  }

  private hasText(value: string | null | undefined): boolean {
    return (value ?? '').trim().length > 0;
  }

  private cleanOptional(value: string | null | undefined): string | null {
    const cleaned = (value ?? '').trim();
    return cleaned.length > 0 ? cleaned : null;
  }

  private messageOf(err: unknown, fallback: string): string {
    const anyErr = err as { error?: { message?: string } };
    return anyErr?.error?.message ?? fallback;
  }

  private get<T>(url: string): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.get<T>(url).subscribe({ next: resolve, error: reject }),
    );
  }

  private send<T = unknown>(
    method: 'POST' | 'PUT' | 'PATCH' | 'DELETE',
    url: string,
    body: unknown,
  ): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http
        .request<T>(method, url, { body })
        .subscribe({ next: resolve as (value: T) => void, error: reject }),
    );
  }
}

