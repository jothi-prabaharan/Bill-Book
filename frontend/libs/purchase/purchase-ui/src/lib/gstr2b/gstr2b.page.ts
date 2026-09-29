import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import {
  GSTR2B_GROUPS,
  Gstr2bImportDetail,
  Gstr2bImportView,
  Gstr2bMatchState,
  Gstr2bService,
} from '@bill-book/purchase-core';

/**
 * GSTR-2B reconciliation (TK-116): import the month's 2B JSON from the GST
 * portal and see, against the purchase register, which supplier invoices
 * match, which differ, which no bill records, and which bills the supplier
 * has not reported — with the credit at risk totalled.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-gstr2b-page',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './gstr2b.page.html',
  styleUrl: './gstr2b.page.scss',
})
export class Gstr2bPage implements OnInit {
  private readonly gstr2b = inject(Gstr2bService);

  protected readonly imports = signal<Gstr2bImportView[]>([]);
  protected readonly selected = signal<Gstr2bImportDetail | null>(null);
  protected readonly group = signal<Gstr2bMatchState>('MissingIn2b');
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);

  protected readonly groups = GSTR2B_GROUPS;

  protected readonly lines = computed(() => (this.selected()?.lines ?? []).filter((l) => l.matchState === this.group()));

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      const imports = await this.gstr2b.list();
      this.imports.set(imports);
      if (imports.length > 0 && this.selected() === null) {
        await this.open(imports[0].gstr2bImportId);
      }
    } catch {
      this.message.set('Could not load the GSTR-2B imports.');
    } finally {
      this.busy.set(false);
    }
  }

  protected async open(id: number): Promise<void> {
    this.busy.set(true);
    this.message.set(null);
    try {
      this.selected.set(await this.gstr2b.get(id));
    } catch {
      this.message.set('Could not open that import.');
    } finally {
      this.busy.set(false);
    }
  }

  protected async upload(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }

    this.busy.set(true);
    this.message.set(null);
    try {
      const detail = await this.gstr2b.import(file, file.name);
      this.selected.set(detail);
      this.group.set('MissingIn2b');
      this.imports.set(await this.gstr2b.list());
    } catch (error: unknown) {
      const body = (error as { error?: { message?: string } } | null)?.error;
      this.message.set(body?.message ?? 'The file could not be imported.');
    } finally {
      this.busy.set(false);
    }
  }

  protected async remove(view: Gstr2bImportView): Promise<void> {
    this.busy.set(true);
    try {
      await this.gstr2b.remove(view.gstr2bImportId);
      if (this.selected()?.gstr2bImportId === view.gstr2bImportId) {
        this.selected.set(null);
      }
      await this.load();
    } catch {
      this.message.set('Could not delete that import.');
      this.busy.set(false);
    }
  }

  protected count(state: Gstr2bMatchState): number {
    const s = this.selected();
    if (!s) {
      return 0;
    }
    switch (state) {
      case 'Matched':
        return s.matchedCount;
      case 'Mismatched':
        return s.mismatchedCount;
      case 'MissingInBooks':
        return s.missingInBooksCount;
      case 'MissingIn2b':
        return s.missingIn2bCount;
    }
  }

  protected period(view: Gstr2bImportView): string {
    return `${view.returnPeriod.slice(0, 2)}/${view.returnPeriod.slice(2)}`;
  }
}
