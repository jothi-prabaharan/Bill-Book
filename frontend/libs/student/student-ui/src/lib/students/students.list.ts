import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { readApiFailure } from '@bill-book/api-client';
import { IfCanDirective } from '@bill-book/auth';
import { StudentApiService, StudentListItem, StudentStatus } from '@bill-book/student-core';
import {
  BbSelectOption,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
} from '@bill-book/ui-components';

/**
 * Students › Students (S1, TK-61): the current year's students by class and
 * section, or one section's roll. National ids arrive masked from the server.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-student-students-list',
  standalone: true,
  imports: [FormsModule, DataGridComponent, DataGridCellTemplateDirective, TextInputComponent, SelectComponent, MessageBoxComponent, IfCanDirective],
  templateUrl: './students.list.html',
  styleUrl: '../student-page.scss',
})
export class StudentsList implements OnInit {
  private readonly api = inject(StudentApiService);
  private readonly router = inject(Router);

  protected readonly rows = signal<StudentListItem[]>([]);
  protected readonly sections = signal<BbSelectOption<number>[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);

  protected search = '';
  protected status: StudentStatus | null = 'Active';
  protected sectionId: number | null = null;

  protected readonly statusOptions: BbSelectOption<StudentStatus>[] = [
    { value: 'Active', label: 'Active' },
    { value: 'Alumni', label: 'Alumni' },
    { value: 'Withdrawn', label: 'Withdrawn' },
    { value: 'Transferred', label: 'Transferred' },
  ];

  protected readonly columns: ColumnDef[] = [
    { field: 'admissionNo', header: 'Admission no.' },
    { field: 'fullName', header: 'Name' },
    { field: 'className', header: 'Class' },
    { field: 'sectionName', header: 'Section' },
    { field: 'rollNo', header: 'Roll' },
    { field: 'studentStatus', header: 'Status' },
  ];

  ngOnInit(): void {
    void this.start();
  }

  protected async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.rows.set(await this.api.students({ search: this.search, status: this.status, sectionId: this.sectionId }));
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  protected add(): void {
    void this.router.navigate(['/sis/students/new']);
  }

  protected open(row: StudentListItem): void {
    void this.router.navigate(['/sis/students', row.studentId]);
  }

  private async start(): Promise<void> {
    try {
      const years = await this.api.years();
      const current = years.find((y) => y.isCurrent);
      const sections = await this.api.sections(current?.academicYearId ?? null);
      this.sections.set(sections.map((s) => ({ value: s.sectionId, label: `${s.className} ${s.name}`, group: s.academicYearCode })));
    } catch (error) {
      this.fail(error);
    }

    await this.load();
  }

  private fail(error: unknown): void {
    const failure = readApiFailure(error);
    this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
  }
}
