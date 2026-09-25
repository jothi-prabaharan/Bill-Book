import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { readApiFailure } from '@bill-book/api-client';
import { IfCanDirective } from '@bill-book/auth';
import { AcademicYear, SchoolClass, Section, SisApiService, Subject, SubjectKind } from '@bill-book/sis-core';
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

type Tab = 'years' | 'classes' | 'sections' | 'subjects';

/**
 * Students › Academic setup (S1, TK-61): school years, classes, sections and
 * subjects. One year is current; making another current takes it off the old
 * one. A new branch starts with the classes LKG to XII.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-sis-academic-setup-page',
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
  templateUrl: './academic-setup.page.html',
  styleUrl: '../sis-page.scss',
})
export class AcademicSetupPage implements OnInit {
  private readonly api = inject(SisApiService);

  protected readonly tabs: readonly { tab: Tab; label: string }[] = [
    { tab: 'years', label: 'School years' },
    { tab: 'classes', label: 'Classes' },
    { tab: 'sections', label: 'Sections' },
    { tab: 'subjects', label: 'Subjects' },
  ];

  protected readonly tab = signal<Tab>('years');
  protected readonly years = signal<AcademicYear[]>([]);
  protected readonly classes = signal<SchoolClass[]>([]);
  protected readonly sections = signal<Section[]>([]);
  protected readonly subjects = signal<Subject[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly editingId = signal<number | null | undefined>(undefined);

  protected yearForm: Omit<AcademicYear, 'academicYearId'> = { code: '', startDate: '', endDate: '', isCurrent: false, isClosed: false };
  protected classForm: Omit<SchoolClass, 'schoolClassId'> = { code: '', name: '', sortOrder: 1, isActive: true };
  protected sectionForm: Partial<Section> = {};
  protected subjectForm: Omit<Subject, 'subjectId'> = { code: '', name: '', subjectKind: 'Core', isActive: true };

  protected readonly subjectKinds: BbSelectOption<SubjectKind>[] = [
    { value: 'Core', label: 'Core' },
    { value: 'Language', label: 'Language' },
    { value: 'Elective', label: 'Elective' },
    { value: 'CoCurricular', label: 'Co-curricular' },
  ];

  protected readonly yearColumns: ColumnDef[] = [
    { field: 'code', header: 'Year' },
    { field: 'startDate', header: 'Starts' },
    { field: 'endDate', header: 'Ends' },
    { field: 'isCurrent', header: 'State' },
    { field: 'actions', header: '' },
  ];
  protected readonly classColumns: ColumnDef[] = [
    { field: 'code', header: 'Code' },
    { field: 'name', header: 'Name' },
    { field: 'sortOrder', header: 'Order' },
    { field: 'isActive', header: 'Status' },
    { field: 'actions', header: '' },
  ];
  protected readonly sectionColumns: ColumnDef[] = [
    { field: 'academicYearCode', header: 'Year' },
    { field: 'className', header: 'Class' },
    { field: 'name', header: 'Section' },
    { field: 'enrolled', header: 'Students' },
    { field: 'capacity', header: 'Capacity' },
    { field: 'actions', header: '' },
  ];
  protected readonly subjectColumns: ColumnDef[] = [
    { field: 'code', header: 'Code' },
    { field: 'name', header: 'Name' },
    { field: 'subjectKind', header: 'Kind' },
    { field: 'isActive', header: 'Status' },
    { field: 'actions', header: '' },
  ];

  protected yearOptions(): BbSelectOption<number>[] {
    return this.years().map((y) => ({ value: y.academicYearId, label: y.code, disabled: y.isClosed }));
  }

  protected classOptions(): BbSelectOption<number>[] {
    return this.classes().filter((c) => c.isActive).map((c) => ({ value: c.schoolClassId, label: c.name }));
  }

  ngOnInit(): void {
    void this.load();
  }

  protected choose(tab: Tab): void {
    this.tab.set(tab);
    this.editingId.set(undefined);
  }

  protected startAdd(): void {
    this.yearForm = { code: '', startDate: '', endDate: '', isCurrent: false, isClosed: false };
    this.classForm = { code: '', name: '', sortOrder: this.classes().length + 1, isActive: true };
    this.sectionForm = { academicYearId: this.years().find((y) => y.isCurrent)?.academicYearId, name: '' };
    this.subjectForm = { code: '', name: '', subjectKind: 'Core', isActive: true };
    this.editingId.set(null);
  }

  protected editYear(row: AcademicYear): void {
    this.yearForm = { ...row };
    this.editingId.set(row.academicYearId);
  }

  protected editClass(row: SchoolClass): void {
    this.classForm = { ...row };
    this.editingId.set(row.schoolClassId);
  }

  protected editSection(row: Section): void {
    this.sectionForm = { ...row };
    this.editingId.set(row.sectionId);
  }

  protected editSubject(row: Subject): void {
    this.subjectForm = { ...row };
    this.editingId.set(row.subjectId);
  }

  protected cancel(): void {
    this.editingId.set(undefined);
  }

  protected async save(): Promise<void> {
    const id = this.editingId() ?? null;
    this.busy.set(true);
    try {
      switch (this.tab()) {
        case 'years':
          await this.api.saveYear(id, this.yearForm);
          break;
        case 'classes':
          await this.api.saveClass(id, this.classForm);
          break;
        case 'sections':
          await this.api.saveSection(id, this.sectionForm);
          break;
        default:
          await this.api.saveSubject(id, this.subjectForm);
      }

      this.messages.set([{ tone: 'success', text: 'Saved.' }]);
      this.editingId.set(undefined);
      await this.load();
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    try {
      const [years, classes, sections, subjects] = await Promise.all([
        this.api.years(),
        this.api.classes(),
        this.api.sections(),
        this.api.subjects(),
      ]);
      this.years.set(years);
      this.classes.set(classes);
      this.sections.set(sections);
      this.subjects.set(subjects);
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
}
