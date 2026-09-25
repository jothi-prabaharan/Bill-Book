import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { readApiFailure } from '@bill-book/api-client';
import { IfCanDirective, SessionContextService } from '@bill-book/auth';
import { SisApiService } from '@bill-book/sis-core';
import {
  ATTENDANCE_MARKS,
  AttendanceStatus,
  RegisterView,
  StudentAttendanceApiService,
  allPresent,
  mayEdit,
  withMark,
} from '@bill-book/student-attendance-core';
import {
  AttendanceRegisterComponent,
  BbSelectOption,
  DateInputComponent,
  MessageBoxComponent,
  RegisterEntry,
  SelectComponent,
  UiMessage,
} from '@bill-book/ui-components';

/**
 * Students › Attendance register (S3, TK-63): a section's day, taken by
 * tapping or from the keyboard, then saved and locked. A locked day is shown
 * but not changed, except by someone who may unlock attendance.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-student-attendance-register-page',
  standalone: true,
  imports: [FormsModule, AttendanceRegisterComponent, SelectComponent, DateInputComponent, MessageBoxComponent, IfCanDirective],
  templateUrl: './register.page.html',
  styleUrl: './register.page.scss',
})
export class RegisterPage implements OnInit {
  private readonly api = inject(StudentAttendanceApiService);
  private readonly sis = inject(SisApiService);
  private readonly session = inject(SessionContextService);

  protected readonly marks = ATTENDANCE_MARKS;
  protected readonly sections = signal<BbSelectOption<number>[]>([]);
  protected readonly register = signal<RegisterView | null>(null);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly dirty = signal(false);

  protected sectionId: number | null = null;
  protected date = new Date().toISOString().slice(0, 10);

  protected readonly mayUnlock = computed(() => this.session.has('attendance.unlock'));
  protected readonly editable = computed(() => {
    const view = this.register();
    return !!view && mayEdit(view.isLocked, this.mayUnlock(), this.session.has('attendance.edit'));
  });

  protected readonly entries = computed<RegisterEntry<AttendanceStatus>[]>(() =>
    (this.register()?.rows ?? []).map((r) => ({
      id: r.enrolmentId,
      label: r.studentName ?? r.admissionNo ?? String(r.enrolmentId),
      caption: r.rollNo ? `Roll ${r.rollNo}` : r.admissionNo,
      status: r.attendanceStatus,
    })),
  );

  protected readonly caption = computed(() => {
    const section = this.sections().find((s) => s.value === this.sectionId);
    return `Attendance, ${section?.label ?? 'section'}, ${this.date}`;
  });

  ngOnInit(): void {
    void this.start();
  }

  protected mark(change: { id: number; status: AttendanceStatus }): void {
    const view = this.register();
    if (view) {
      this.register.set({ ...view, rows: withMark(view.rows, change.id, change.status) });
      this.dirty.set(true);
    }
  }

  protected markAllPresent(): void {
    const view = this.register();
    if (view) {
      this.register.set({ ...view, rows: allPresent(view.rows) });
      this.dirty.set(true);
    }
  }

  protected async load(): Promise<void> {
    if (!this.sectionId || !this.date) {
      this.register.set(null);
      return;
    }

    this.busy.set(true);
    try {
      this.register.set(await this.api.register(this.sectionId, this.date));
      this.dirty.set(false);
      this.messages.set([]);
    } catch (error) {
      this.register.set(null);
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  protected async save(): Promise<void> {
    const view = this.register();
    if (!view) {
      return;
    }

    this.busy.set(true);
    try {
      await this.api.save(view.sectionId, view.attendanceDate, view.rows);
      this.messages.set([{ tone: 'success', text: 'The register is saved.' }]);
      await this.load();
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  protected async lock(): Promise<void> {
    await this.act((v) => this.api.lock(v.sectionId, v.attendanceDate), 'The day is locked.');
  }

  protected async unlock(): Promise<void> {
    await this.act((v) => this.api.unlock(v.sectionId, v.attendanceDate), 'The day is unlocked.');
  }

  private async act(call: (view: RegisterView) => Promise<void>, done: string): Promise<void> {
    const view = this.register();
    if (!view) {
      return;
    }

    this.busy.set(true);
    try {
      await call(view);
      await this.load();
      this.messages.set([{ tone: 'success', text: done }]);
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  private async start(): Promise<void> {
    try {
      const years = await this.sis.years();
      const current = years.find((y) => y.isCurrent);
      const sections = await this.sis.sections(current?.academicYearId ?? null);
      this.sections.set(sections.map((s) => ({ value: s.sectionId, label: `${s.className} ${s.name}` })));
    } catch (error) {
      this.fail(error);
    }
  }

  private fail(error: unknown): void {
    const failure = readApiFailure(error);
    this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
  }
}
