import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { readApiFailure } from '@bill-book/api-client';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  BbSelectOption,
  CheckboxComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  DataGridFooterDirective,
  BranchDateInputComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
} from '@bill-book/ui-components';
import { ProjectListItem, loadProjects, projectOptions } from './project-options';
import {
  TimeEntryRow,
  dayTotals,
  isoDate,
  parseIsoDate,
  roundHours,
  timerHours,
  weekDays,
  weekGrid,
  weekStart,
} from './timesheet-week';

interface ProjectTaskOption {
  projectTaskId: number;
  taskName: string;
  isBillable: boolean;
  isActive: boolean;
}

interface EntryForm {
  projectId: number | null;
  projectTaskId: number | null;
  workDate: string;
  hours: number | null;
  notes: string;
  isBillable: boolean;
}

/** A running timer, kept in this browser so a reload does not lose it. */
interface RunningTimer {
  projectId: number;
  projectTaskId: number;
  startedAt: number;
}

const TIMER_KEY = 'bb.timesheet.timer';

/**
 * Accounting › Timesheet (TK-106). The signed-in user's hours for a week,
 * folded into one row per project task, with an entry form and a timer.
 * An entry that has been billed is locked: the invoice that billed it has to
 * be voided first.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-timesheet-page',
  standalone: true,
  imports: [
    FormsModule,
    SelectComponent,
    NumberInputComponent,
    BranchDateInputComponent,
    TextInputComponent,
    CheckboxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    DataGridFooterDirective,
  ],
  templateUrl: './timesheet.page.html',
  styleUrl: './timesheet.page.scss',
})
export class TimesheetPage implements OnInit, OnDestroy {
  private readonly http = inject(HttpClient);
  protected readonly formats = inject(FormatSettingsService);

  protected readonly monday = signal(weekStart(new Date()));
  protected readonly days = computed(() => weekDays(this.monday()));
  protected readonly entries = signal<TimeEntryRow[]>([]);
  protected readonly rows = computed(() => weekGrid(this.entries(), this.days()));
  protected readonly totals = computed(() => dayTotals(this.rows()));
  protected readonly weekTotal = computed(() => roundHours(this.totals().reduce((a, b) => a + b, 0)));

  /** A column per day of the week shown, between the task and its total. */
  protected readonly weekColumns = computed<ColumnDef[]>(() => [
    { field: 'label', header: 'Project · task' },
    ...this.days().map((day, i) => ({ field: `day${i}`, header: this.dayLabel(day), numeric: true })),
    { field: 'total', header: 'Total', numeric: true },
  ]);
  protected readonly projects = signal<ProjectListItem[]>([]);
  protected readonly projectChoices = computed(() => projectOptions(this.projects(), [this.form.projectId]));
  protected readonly taskChoices = signal<BbSelectOption<number>[]>([]);
  protected readonly editing = signal(false);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);
  protected readonly timer = signal<RunningTimer | null>(null);
  protected readonly now = signal(Date.now());
  protected readonly timerLabel = computed(() => {
    const running = this.timer();
    if (!running) {
      return '';
    }
    const seconds = Math.max(0, Math.floor((this.now() - running.startedAt) / 1000));
    const hh = String(Math.floor(seconds / 3600)).padStart(2, '0');
    const mm = String(Math.floor((seconds % 3600) / 60)).padStart(2, '0');
    const ss = String(seconds % 60).padStart(2, '0');
    return `${hh}:${mm}:${ss}`;
  });

  protected editingId: number | null = null;
  protected form: EntryForm = this.blankForm(isoDate(new Date()));
  private tasks: ProjectTaskOption[] = [];
  private tick: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    this.restoreTimer();
    void this.start();
  }

  private async start(): Promise<void> {
    this.projects.set(await loadProjects(this.http));
    await this.load();
  }

  ngOnDestroy(): void {
    this.stopTicking();
  }

  /** Hours at the column's two decimals, trailing zeros dropped: 1.5, not 1.50. */
  protected hoursText(hours: number): string {
    return String(roundHours(hours));
  }

  protected dayLabel(day: string): string {
    return parseIsoDate(day).toLocaleDateString(undefined, { weekday: 'short', day: 'numeric' });
  }

  protected entriesOn(day: string): TimeEntryRow[] {
    return this.entries().filter((e) => e.workDate === day);
  }

  protected async shiftWeek(weeks: number): Promise<void> {
    const next = new Date(this.monday());
    next.setDate(next.getDate() + weeks * 7);
    this.monday.set(next);
    await this.load();
  }

  protected async thisWeek(): Promise<void> {
    this.monday.set(weekStart(new Date()));
    await this.load();
  }

  protected async load(): Promise<void> {
    const days = this.days();
    this.busy.set(true);
    try {
      this.entries.set(
        await firstValueFrom(this.http.get<TimeEntryRow[]>(`/api/projects/time?from=${days[0]}&to=${days[6]}`)),
      );
    } catch (err) {
      this.fail(readApiFailure(err).text);
    } finally {
      this.busy.set(false);
    }
  }

  protected async newEntry(day?: string): Promise<void> {
    this.editingId = null;
    this.form = this.blankForm(day ?? this.days()[0]);
    this.taskChoices.set([]);
    this.message.set(null);
    this.editing.set(true);
  }

  protected async edit(entry: TimeEntryRow): Promise<void> {
    if (entry.billingStatus !== 'Unbilled') {
      return;
    }
    this.editingId = entry.timeEntryId;
    this.form = {
      projectId: entry.projectId,
      projectTaskId: entry.projectTaskId,
      workDate: entry.workDate,
      hours: entry.hours,
      notes: entry.notes ?? '',
      isBillable: entry.isBillable,
    };
    await this.loadTasks(entry.projectId, entry.projectTaskId);
    this.message.set(null);
    this.editing.set(true);
  }

  protected cancel(): void {
    this.editing.set(false);
  }

  protected async onProject(): Promise<void> {
    this.form.projectTaskId = null;
    await this.loadTasks(this.form.projectId, null);
  }

  protected onTask(): void {
    const task = this.tasks.find((t) => t.projectTaskId === this.form.projectTaskId);
    if (task && this.editingId === null) {
      this.form.isBillable = task.isBillable;
    }
  }

  protected async save(): Promise<void> {
    const f = this.form;
    if (!f.projectId || !f.projectTaskId || !f.workDate || !f.hours || f.hours <= 0 || f.hours > 24) {
      this.fail('Choose a project, a task and a date, and enter more than 0 and at most 24 hours.');
      return;
    }

    const body = {
      projectId: f.projectId,
      projectTaskId: f.projectTaskId,
      workDate: f.workDate,
      hours: roundHours(f.hours),
      notes: f.notes.trim() || null,
      isBillable: f.isBillable,
    };

    this.busy.set(true);
    try {
      if (this.editingId === null) {
        await firstValueFrom(this.http.post('/api/projects/time', body));
      } else {
        await firstValueFrom(this.http.put(`/api/projects/time/${this.editingId}`, body));
      }
      this.editing.set(false);
      this.ok('Hours saved.');
      const saved = weekStart(parseIsoDate(f.workDate));
      if (isoDate(saved) !== isoDate(this.monday())) {
        this.monday.set(saved);
      }
      await this.load();
    } catch (err) {
      this.fail(readApiFailure(err).text);
    } finally {
      this.busy.set(false);
    }
  }

  protected async remove(): Promise<void> {
    if (this.editingId === null) {
      return;
    }
    this.busy.set(true);
    try {
      await firstValueFrom(this.http.delete(`/api/projects/time/${this.editingId}`));
      this.editing.set(false);
      this.ok('Entry deleted.');
      await this.load();
    } catch (err) {
      this.fail(readApiFailure(err).text);
    } finally {
      this.busy.set(false);
    }
  }

  /** Starts the timer on the project and task chosen in the form. */
  protected startTimer(): void {
    if (!this.form.projectId || !this.form.projectTaskId) {
      this.fail('Choose a project and a task to time.');
      return;
    }
    const running: RunningTimer = {
      projectId: this.form.projectId,
      projectTaskId: this.form.projectTaskId,
      startedAt: Date.now(),
    };
    this.timer.set(running);
    this.persistTimer(running);
    this.startTicking();
    this.editing.set(false);
    this.ok('Timer started.');
  }

  /** Stops the timer and opens an entry for today with the time rounded up to the quarter hour. */
  protected async stopTimer(): Promise<void> {
    const running = this.timer();
    if (!running) {
      return;
    }
    this.timer.set(null);
    this.persistTimer(null);
    this.stopTicking();

    this.editingId = null;
    this.form = {
      ...this.blankForm(isoDate(new Date())),
      projectId: running.projectId,
      projectTaskId: running.projectTaskId,
      hours: timerHours(Date.now() - running.startedAt),
    };
    await this.loadTasks(running.projectId, running.projectTaskId);
    this.onTaskDefaults();
    this.message.set(null);
    this.editing.set(true);
  }

  protected timerProject(): string {
    const running = this.timer();
    const project = running ? this.projects().find((p) => p.projectId === running.projectId) : undefined;
    return project ? `${project.projectCode} · ${project.projectName}` : '';
  }

  private onTaskDefaults(): void {
    const task = this.tasks.find((t) => t.projectTaskId === this.form.projectTaskId);
    this.form.isBillable = task?.isBillable ?? true;
  }

  private async loadTasks(projectId: number | null, keep: number | null): Promise<void> {
    this.tasks = [];
    this.taskChoices.set([]);
    if (!projectId) {
      return;
    }
    try {
      const project = await firstValueFrom(
        this.http.get<{ tasks: ProjectTaskOption[] }>(`/api/projects/${projectId}`),
      );
      this.tasks = project.tasks;
      this.taskChoices.set(
        project.tasks
          .filter((t) => t.isActive || t.projectTaskId === keep)
          .map((t) => ({ value: t.projectTaskId, label: t.taskName })),
      );
    } catch {
      this.fail('That project’s tasks could not be loaded.');
    }
  }

  private restoreTimer(): void {
    try {
      const raw = localStorage.getItem(TIMER_KEY);
      const running = raw ? (JSON.parse(raw) as RunningTimer) : null;
      if (running && running.projectId && running.projectTaskId && running.startedAt) {
        this.timer.set(running);
        this.startTicking();
      }
    } catch {
      // No storage in this browser; the timer simply does not survive a reload.
    }
  }

  private persistTimer(running: RunningTimer | null): void {
    try {
      if (running) {
        localStorage.setItem(TIMER_KEY, JSON.stringify(running));
      } else {
        localStorage.removeItem(TIMER_KEY);
      }
    } catch {
      // As above.
    }
  }

  private startTicking(): void {
    this.stopTicking();
    this.now.set(Date.now());
    this.tick = setInterval(() => this.now.set(Date.now()), 1000);
  }

  private stopTicking(): void {
    if (this.tick !== null) {
      clearInterval(this.tick);
      this.tick = null;
    }
  }

  private blankForm(workDate: string): EntryForm {
    return { projectId: null, projectTaskId: null, workDate, hours: null, notes: '', isBillable: true };
  }

  private ok(text: string): void {
    this.message.set(text);
    this.messageIsError.set(false);
  }

  private fail(text: string): void {
    this.message.set(text);
    this.messageIsError.set(true);
  }
}

