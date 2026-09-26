import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList } from '@angular/cdk/drag-drop';
import { firstValueFrom } from 'rxjs';
import { SessionContextService } from '@bill-book/auth';
import { readApiFailure } from '@bill-book/api-client';
import {
  BbSelectOption,
  CheckboxComponent,
  DateInputComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
} from '@bill-book/ui-components';
import {
  APPROVER_KINDS,
  ApprovalLevel,
  ApprovalWorkflowView,
  REQUEST_KINDS,
  SaveApprovalWorkflow,
  approverKindsFor,
  blankLevel,
  moveLevel,
  requestKindLabel,
  workflowProblem,
} from './approval-workflows.model';

interface RoleRow {
  roleId: number;
  displayName: string;
}

interface UserRow {
  userId: string;
  displayName: string;
}

/**
 * Settings › Approval workflows (TK-103): every app's chains, in one place.
 * A workflow governs one kind of request from a date, and its levels run in
 * the order shown — drag a level, or use its arrows, to reorder. A level with
 * an amount applies only above it.
 *
 * Anyone with `settings.view` can read the page; saving needs `settings.edit`,
 * which the API checks regardless.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-approval-workflows-page',
  standalone: true,
  imports: [
    FormsModule,
    CdkDropList,
    CdkDrag,
    CdkDragHandle,
    TextInputComponent,
    SelectComponent,
    CheckboxComponent,
    DateInputComponent,
    NumberInputComponent,
  ],
  templateUrl: './approval-workflows.page.html',
  styleUrl: './approval-workflows.page.scss',
})
export class ApprovalWorkflowsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionContextService);

  protected readonly approverKinds = APPROVER_KINDS;
  protected readonly requestKindLabel = requestKindLabel;
  protected readonly kindOptions: BbSelectOption<number>[] = REQUEST_KINDS.map((k) => ({
    value: k.value,
    label: k.label,
    group: k.group,
  }));

  protected readonly canEdit = computed(() => this.session.has('settings.edit'));
  protected readonly workflows = signal<ApprovalWorkflowView[]>([]);
  protected readonly roleOptions = signal<BbSelectOption<number>[]>([]);
  protected readonly userOptions = signal<BbSelectOption<string>[]>([]);
  protected readonly editing = signal<SaveApprovalWorkflow | null>(null);
  protected readonly levels = signal<ApprovalLevel[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  private editingId: number | null = null;

  ngOnInit(): void {
    void this.load();
    void this.loadPeople();
  }

  protected approverKindOptions(requestKind: number): BbSelectOption<number>[] {
    return approverKindsFor(requestKind);
  }

  protected levelSummary(workflow: ApprovalWorkflowView): string {
    const count = workflow.levels.length;
    return `${count} level${count === 1 ? '' : 's'}`;
  }

  protected openNew(): void {
    this.editingId = null;
    const kind = 101;
    this.editing.set({
      name: '',
      requestKind: kind,
      departmentId: null,
      gradeId: null,
      workLocationId: null,
      effectiveFrom: new Date().toISOString().slice(0, 10),
      isActive: true,
      levels: [],
    });
    this.levels.set([blankLevel(kind)]);
    this.message.set(null);
  }

  protected open(workflow: ApprovalWorkflowView): void {
    this.editingId = workflow.approvalWorkflowId;
    this.editing.set({ ...workflow });
    this.levels.set(workflow.levels.map((l) => ({ ...l })));
    this.message.set(null);
  }

  protected close(): void {
    this.editing.set(null);
    this.editingId = null;
  }

  protected addLevel(): void {
    const kind = this.editing()?.requestKind ?? 101;
    this.levels.update((levels) => [...levels, blankLevel(kind)]);
  }

  protected removeLevel(index: number): void {
    this.levels.update((levels) => levels.filter((_, i) => i !== index));
  }

  protected move(from: number, to: number): void {
    this.levels.update((levels) => moveLevel(levels, from, to));
  }

  protected dropped(event: CdkDragDrop<ApprovalLevel[]>): void {
    this.move(event.previousIndex, event.currentIndex);
  }

  /** A kind change resets approver kinds a RetailErp document cannot use. */
  protected kindChanged(kind: number | null): void {
    if (kind === null) {
      return;
    }
    const allowed = approverKindsFor(kind).map((k) => k.value);
    this.levels.update((levels) =>
      levels.map((level) => (allowed.includes(level.approverKind) ? level : { ...level, approverKind: allowed[0] })),
    );
  }

  /** Re-publishes the array after ngModel has written into a level in place. */
  protected touched(): void {
    this.levels.update((levels) => [...levels]);
  }

  protected async save(): Promise<void> {
    const draft = this.editing();
    if (!draft) {
      return;
    }

    const workflow: SaveApprovalWorkflow = { ...draft, levels: this.levels() };
    const problem = workflowProblem(workflow);
    if (problem) {
      this.show(problem, true);
      return;
    }

    this.busy.set(true);
    try {
      if (this.editingId === null) {
        await firstValueFrom(this.http.post('/api/approval-workflows', workflow));
      } else {
        await firstValueFrom(this.http.put(`/api/approval-workflows/${this.editingId}`, workflow));
      }
      this.close();
      await this.load();
      this.show('Workflow saved.', false);
    } catch (error) {
      this.show(readApiFailure(error).text, true);
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    try {
      this.workflows.set(await firstValueFrom(this.http.get<ApprovalWorkflowView[]>('/api/approval-workflows')));
    } catch (error) {
      this.show(readApiFailure(error).text, true);
    }
  }

  /** The roles and users a level can name. Either may be refused to a reader; the list then stays empty. */
  private async loadPeople(): Promise<void> {
    const [roles, users] = await Promise.all([
      firstValueFrom(this.http.get<RoleRow[]>('/api/roles')).catch(() => [] as RoleRow[]),
      firstValueFrom(this.http.get<UserRow[]>('/api/users')).catch(() => [] as UserRow[]),
    ]);
    this.roleOptions.set(roles.map((r) => ({ value: r.roleId, label: r.displayName })));
    this.userOptions.set(users.map((u) => ({ value: u.userId, label: u.displayName })));
  }

  private show(text: string, isError: boolean): void {
    this.message.set(text);
    this.messageIsError.set(isError);
  }
}
