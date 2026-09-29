import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import {
  CheckboxComponent,
  SelectComponent,
} from '@bill-book/ui-components';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { readApiFailure } from '@bill-book/api-client';
import {
  ApiDocumentLine,
  BillableBillLine,
  ProjectItemRef,
  ProjectUnbilled,
  toBillingLines,
} from '@bill-book/sales-core';

interface ProjectRow {
  projectId: number;
  projectCode: string;
  projectName: string;
  status: string;
}

interface AccountRow {
  accountId: number;
  accountCode: string;
  accountName: string;
  accountTypeId: number;
}

/** Income accounts are type 4 in `mst.AccountTypes`. */
const INCOME_TYPE = 4;

/**
 * "Add project items" on the invoice (TK-106): a project's unbilled hours,
 * re-billable expenses and bill lines, and unbilled milestones, ticked and
 * turned into invoice lines. Nothing is claimed here — the items travel with
 * the draft and are claimed when it posts, so two drafts may list the same
 * hours but only one can post them.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-project-items-dialog',
  standalone: true,
  imports: [
    FormsModule,
    DecimalPipe,
    CheckboxComponent,
    SelectComponent,
  ],
  templateUrl: './project-items.dialog.html',
  styleUrl: './project-items.dialog.scss',
})
export class ProjectItemsDialogComponent implements OnInit {
  private readonly http = inject(HttpClient);

  /** The invoice's customer; their projects are offered first. */
  readonly contactId = input<number | null>(null);

  /** Items already on the invoice, so they are not offered again. */
  readonly existing = input<readonly ProjectItemRef[]>([]);

  readonly added = output<{ lines: ApiDocumentLine[]; items: ProjectItemRef[] }>();
  readonly cancelled = output<void>();

  protected readonly projects = signal<ProjectRow[]>([]);
  protected readonly accounts = signal<AccountRow[]>([]);
  protected readonly unbilled = signal<ProjectUnbilled | null>(null);
  protected readonly bills = signal<BillableBillLine[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(false);

  protected projectId: number | null = null;
  protected accountId: number | null = null;

  protected readonly projectOptions = computed(() =>
    this.projects().map((p) => ({ value: p.projectId, label: `${p.projectCode} · ${p.projectName}` })),
  );

  protected readonly accountOptions = computed(() =>
    this.accounts().map((a) => ({ value: a.accountId, label: `${a.accountCode} ${a.accountName}` })),
  );
  protected picked = new Set<string>();

  protected readonly nothingLeft = computed(() => {
    const u = this.unbilled();
    return u !== null && u.time.length + u.expenses.length + u.milestones.length + this.bills().length === 0;
  });

  ngOnInit(): void {
    void this.loadLists();
  }

  protected key(kind: string, id: number): string {
    return `${kind}:${id}`;
  }

  protected isTaken(kind: string, id: number): boolean {
    return this.existing().some((i) => i.kind === kind && i.itemId === id);
  }

  protected toggle(kind: string, id: number, checked: boolean): void {
    const key = this.key(kind, id);
    if (checked) {
      this.picked.add(key);
    } else {
      this.picked.delete(key);
    }
  }

  protected async chooseProject(projectId: number | null): Promise<void> {
    this.projectId = projectId;
    this.picked = new Set();
    this.unbilled.set(null);
    this.bills.set([]);
    if (projectId === null) {
      return;
    }

    this.loading.set(true);
    try {
      const [unbilled, bills] = await Promise.all([
        firstValueFrom(this.http.get<ProjectUnbilled>(`/api/projects/${projectId}/unbilled`)),
        // Bill lines are Purchase's; a user who cannot read purchases simply sees none.
        firstValueFrom(this.http.get<BillableBillLine[]>(`/api/purchase/bills/billable?projectId=${projectId}`)).catch(
          () => [] as BillableBillLine[],
        ),
      ]);
      this.unbilled.set(unbilled);
      this.bills.set(bills);
    } catch (error) {
      this.error.set(readApiFailure(error).text);
    } finally {
      this.loading.set(false);
    }
  }

  protected add(): void {
    const unbilled = this.unbilled();
    if (!unbilled || this.projectId === null) {
      return;
    }

    const has = (kind: string, id: number): boolean => this.picked.has(this.key(kind, id));
    const result = toBillingLines(
      this.projectId,
      {
        time: unbilled.time.filter((t) => has('TimeEntry', t.timeEntryId)),
        expenses: unbilled.expenses.filter((e) => has('Expense', e.spendMoneyDetailId)),
        bills: this.bills().filter((b) => has('BillLine', b.billDetailId)),
        milestones: unbilled.milestones.filter((m) => has('Milestone', m.projectMilestoneId)),
      },
      this.accountId,
    );

    if (result.lines.length === 0) {
      this.error.set('Tick at least one item to add.');
      return;
    }

    this.added.emit(result);
  }

  private async loadLists(): Promise<void> {
    const contact = this.contactId();
    const query = contact ? `?contactId=${contact}` : '';
    try {
      const [projects, accounts] = await Promise.all([
        firstValueFrom(this.http.get<ProjectRow[]>(`/api/projects${query}`)),
        firstValueFrom(this.http.get<AccountRow[]>('/api/accounts')).catch(() => [] as AccountRow[]),
      ]);
      this.projects.set(projects.filter((p) => p.status === 'Active' || p.status === 'OnHold'));
      this.accounts.set(accounts.filter((a) => a.accountTypeId === INCOME_TYPE));
    } catch (error) {
      this.error.set(readApiFailure(error).text);
    }
  }
}
