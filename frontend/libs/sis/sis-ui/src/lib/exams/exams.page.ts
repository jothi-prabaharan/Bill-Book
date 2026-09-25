import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { readApiFailure } from '@bill-book/api-client';
import { IfCanDirective } from '@bill-book/auth';
import {
  EXAM_STATUS_LABELS,
  Exam,
  ExamStatus,
  ExamSubject,
  MarkRow,
  SaveExam,
  SchoolClass,
  Section,
  SisApiService,
  Subject,
  markProblem,
  nextExamStatuses,
} from '@bill-book/sis-core';
import {
  BbSelectOption,
  CheckboxComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  DateInputComponent,
  MessageBoxComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
} from '@bill-book/ui-components';

/**
 * Students › Exams and marks (S1, TK-61). An exam is planned with its subjects
 * per class, opened for marks, published to parents and locked. Marks are
 * entered per subject and section while the exam is open.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-sis-exams-page',
  standalone: true,
  imports: [
    FormsModule,
    DataGridComponent,
    DataGridCellTemplateDirective,
    TextInputComponent,
    NumberInputComponent,
    DateInputComponent,
    SelectComponent,
    CheckboxComponent,
    MessageBoxComponent,
    IfCanDirective,
  ],
  templateUrl: './exams.page.html',
  styleUrl: '../sis-page.scss',
})
export class ExamsPage implements OnInit {
  private readonly api = inject(SisApiService);

  protected readonly statusLabels = EXAM_STATUS_LABELS;
  protected readonly exams = signal<Exam[]>([]);
  protected readonly subjects = signal<Subject[]>([]);
  protected readonly classes = signal<SchoolClass[]>([]);
  protected readonly sections = signal<Section[]>([]);
  protected readonly yearOptions = signal<BbSelectOption<number>[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly editingId = signal<number | null | undefined>(undefined);

  protected readonly marksExam = signal<Exam | null>(null);
  protected readonly markRows = signal<MarkRow[]>([]);
  protected marksSubjectId: number | null = null;
  protected marksSectionId: number | null = null;

  protected form: SaveExam = ExamsPage.blank();

  protected readonly columns: ColumnDef[] = [
    { field: 'name', header: 'Exam' },
    { field: 'startDate', header: 'Starts' },
    { field: 'endDate', header: 'Ends' },
    { field: 'examStatus', header: 'State' },
    { field: 'actions', header: '' },
  ];

  protected readonly subjectOptions = computed(() => this.subjects().filter((s) => s.isActive).map((s) => ({ value: s.subjectId, label: s.name })));
  protected readonly classOptions = computed(() => this.classes().filter((c) => c.isActive).map((c) => ({ value: c.schoolClassId, label: c.name })));

  /** The chosen exam's subjects, as "Class · Subject". */
  protected readonly examSubjectOptions = computed<BbSelectOption<number>[]>(() =>
    (this.marksExam()?.subjects ?? []).map((s) => ({
      value: s.examSubjectId ?? 0,
      label: `${this.className(s.schoolClassId)} · ${this.subjectName(s.subjectId)}`,
    })),
  );

  ngOnInit(): void {
    void this.load();
  }

  protected label(status: ExamStatus): string {
    return EXAM_STATUS_LABELS[status];
  }

  protected next(exam: Exam): ExamStatus[] {
    return nextExamStatuses(exam.examStatus);
  }

  protected startAdd(): void {
    this.form = ExamsPage.blank();
    const current = this.yearOptions()[0];
    if (current) {
      this.form.academicYearId = current.value;
    }
    this.addSubject();
    this.editingId.set(null);
  }

  protected startEdit(exam: Exam): void {
    this.form = { ...exam, subjects: exam.subjects.map((s) => ({ ...s })) };
    this.editingId.set(exam.examId);
  }

  protected addSubject(): void {
    this.form.subjects = [...this.form.subjects, { subjectId: 0, schoolClassId: 0, maxMarks: 100, passMarks: 35 }];
  }

  protected removeSubject(index: number): void {
    this.form.subjects = this.form.subjects.filter((_, i) => i !== index);
  }

  protected cancel(): void {
    this.editingId.set(undefined);
  }

  protected async save(): Promise<void> {
    if (!this.form.name.trim() || this.form.subjects.length === 0) {
      this.messages.set([{ tone: 'error', text: 'Give the exam a name and at least one subject.' }]);
      return;
    }

    this.busy.set(true);
    try {
      await this.api.saveExam(this.editingId() ?? null, this.form);
      this.messages.set([{ tone: 'success', text: 'The exam is saved.' }]);
      this.editingId.set(undefined);
      await this.load();
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  protected async move(exam: Exam, to: ExamStatus): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.moveExam(exam.examId, to);
      this.messages.set([{ tone: 'success', text: `${exam.name} is now ${EXAM_STATUS_LABELS[to].toLowerCase()}.` }]);
      await this.load();
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  protected openMarks(exam: Exam): void {
    this.marksExam.set(exam);
    this.marksSubjectId = null;
    this.marksSectionId = null;
    this.markRows.set([]);
  }

  /** The sections of the chosen subject's class, in the exam's year. */
  protected marksSections(): BbSelectOption<number>[] {
    const exam = this.marksExam();
    const subject = exam?.subjects.find((s) => s.examSubjectId === this.marksSubjectId);
    return this.sections()
      .filter((s) => s.academicYearId === exam?.academicYearId && s.schoolClassId === subject?.schoolClassId)
      .map((s) => ({ value: s.sectionId, label: `${s.className} ${s.name}` }));
  }

  protected async loadMarks(): Promise<void> {
    const exam = this.marksExam();
    if (!exam || !this.marksSubjectId || !this.marksSectionId) {
      return;
    }

    try {
      this.markRows.set(await this.api.marks(exam.examId, this.marksSubjectId, this.marksSectionId));
    } catch (error) {
      this.fail(error);
    }
  }

  protected async saveMarks(): Promise<void> {
    const exam = this.marksExam();
    const subject = exam?.subjects.find((s) => s.examSubjectId === this.marksSubjectId);
    if (!exam || !subject) {
      return;
    }

    const bad = this.markRows().map((r) => markProblem(r, subject.maxMarks)).find((p) => p !== null);
    if (bad) {
      this.messages.set([{ tone: 'error', text: bad }]);
      return;
    }

    this.busy.set(true);
    try {
      await this.api.saveMarks(exam.examId, subject.examSubjectId ?? 0, this.markRows());
      this.messages.set([{ tone: 'success', text: 'The marks are saved.' }]);
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  protected maxMarks(): number {
    return this.marksExam()?.subjects.find((s) => s.examSubjectId === this.marksSubjectId)?.maxMarks ?? 0;
  }

  private className(id: number): string {
    return this.classes().find((c) => c.schoolClassId === id)?.name ?? '';
  }

  private subjectName(id: number): string {
    return this.subjects().find((s) => s.subjectId === id)?.name ?? '';
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    try {
      const [years, exams, subjects, classes, sections] = await Promise.all([
        this.api.years(),
        this.api.exams(),
        this.api.subjects(),
        this.api.classes(),
        this.api.sections(),
      ]);
      this.yearOptions.set(
        [...years].sort((a, b) => Number(b.isCurrent) - Number(a.isCurrent)).filter((y) => !y.isClosed).map((y) => ({ value: y.academicYearId, label: y.code })),
      );
      this.exams.set(exams);
      this.subjects.set(subjects);
      this.classes.set(classes);
      this.sections.set(sections);

      const open = this.marksExam();
      if (open) {
        this.marksExam.set(exams.find((e) => e.examId === open.examId) ?? null);
      }
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  private fail(error: unknown): void {
    const failure = readApiFailure(error);
    this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
  }

  private static blank(): SaveExam {
    return { academicYearId: 0, name: '', startDate: '', endDate: '', subjects: [] as ExamSubject[] };
  }
}
