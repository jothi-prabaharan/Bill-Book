import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { readApiFailure } from '@bill-book/api-client';
import { IfCanDirective } from '@bill-book/auth';
import {
  EnrolRequest,
  EnrolmentView,
  Gender,
  GuardianRelationship,
  SaveStudent,
  StudentApiService,
  StudentGuardian,
  StudentStatus,
  studentProblem,
} from '@bill-book/student-core';
import {
  BbSelectOption,
  CheckboxComponent,
  DateInputComponent,
  MessageBoxComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
} from '@bill-book/ui-components';

/**
 * A student's record (S1, TK-61): personal details, one or two guardians
 * chosen from the guardian contacts, and enrolments. A new student can be
 * enrolled in the same save; the admission number comes from the ADM series.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-student-student-page',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    TextInputComponent,
    DateInputComponent,
    SelectComponent,
    NumberInputComponent,
    CheckboxComponent,
    MessageBoxComponent,
    IfCanDirective,
  ],
  templateUrl: './student.page.html',
  styleUrl: '../student-page.scss',
})
export class StudentPage implements OnInit {
  private readonly api = inject(StudentApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly studentId = signal<number | null>(null);
  protected readonly admissionNo = signal<string | null>(null);
  protected readonly enrolments = signal<EnrolmentView[]>([]);
  protected readonly guardianOptions = signal<BbSelectOption<number>[]>([]);
  protected readonly yearOptions = signal<BbSelectOption<number>[]>([]);
  protected readonly sectionOptions = signal<BbSelectOption<number>[]>([]);

  protected form: SaveStudent = StudentPage.blank();
  protected enrol: EnrolRequest = { academicYearId: 0, sectionId: 0, rollNo: null };
  protected enrolNow = true;

  protected readonly genders: BbSelectOption<Gender>[] = [
    { value: 'Male', label: 'Male' },
    { value: 'Female', label: 'Female' },
    { value: 'Other', label: 'Other' },
    { value: 'NotStated', label: 'Not stated' },
  ];

  protected readonly statuses: BbSelectOption<StudentStatus>[] = [
    { value: 'Active', label: 'Active' },
    { value: 'Alumni', label: 'Alumni' },
    { value: 'Withdrawn', label: 'Withdrawn' },
    { value: 'Transferred', label: 'Transferred' },
  ];

  protected readonly relationships: BbSelectOption<GuardianRelationship>[] = [
    { value: 'Mother', label: 'Mother' },
    { value: 'Father', label: 'Father' },
    { value: 'Guardian', label: 'Guardian' },
    { value: 'Other', label: 'Other' },
  ];

  ngOnInit(): void {
    void this.start();
  }

  protected addGuardian(): void {
    this.form.guardians = [
      ...this.form.guardians,
      { contactId: 0, relationship: 'Guardian', isPrimary: this.form.guardians.length === 0, hasPortalAccess: false },
    ];
  }

  protected removeGuardian(index: number): void {
    this.form.guardians = this.form.guardians.filter((_, i) => i !== index);
  }

  protected makePrimary(guardian: StudentGuardian): void {
    for (const g of this.form.guardians) {
      g.isPrimary = g === guardian;
    }
  }

  protected async yearChosen(yearId: number | null): Promise<void> {
    this.enrol.sectionId = 0;
    this.sectionOptions.set([]);
    if (!yearId) {
      return;
    }

    try {
      const sections = await this.api.sections(yearId);
      this.sectionOptions.set(sections.map((s) => ({ value: s.sectionId, label: `${s.className} ${s.name}` })));
    } catch (error) {
      this.fail(error);
    }
  }

  protected async save(): Promise<void> {
    const problem = studentProblem(this.form);
    if (problem) {
      this.messages.set([{ tone: 'error', text: problem }]);
      return;
    }

    const isNew = this.studentId() === null;
    const body: SaveStudent = { ...this.form, enrol: isNew && this.enrolNow && this.enrol.sectionId ? this.enrol : null };

    this.busy.set(true);
    try {
      const saved = await this.api.saveStudent(this.studentId(), body);
      this.messages.set([{ tone: 'success', text: 'The student is saved.' }]);
      if (isNew) {
        await this.router.navigate(['/sis/students', saved.id], { replaceUrl: true });
        await this.load(saved.id);
      }
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  protected async enrolExisting(): Promise<void> {
    const id = this.studentId();
    if (id === null || !this.enrol.sectionId) {
      this.messages.set([{ tone: 'error', text: 'Choose a school year and a section.' }]);
      return;
    }

    this.busy.set(true);
    try {
      await this.api.enrol({ ...this.enrol, studentId: id });
      this.messages.set([{ tone: 'success', text: 'The student is enrolled.' }]);
      await this.load(id);
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  private async start(): Promise<void> {
    try {
      const [guardians, years] = await Promise.all([this.api.guardians(), this.api.years()]);
      this.guardianOptions.set(guardians.map((g) => ({ value: g.contactId, label: `${g.displayName} (${g.contactCode})` })));
      this.yearOptions.set(years.filter((y) => !y.isClosed).map((y) => ({ value: y.academicYearId, label: y.code })));
      const current = years.find((y) => y.isCurrent && !y.isClosed);
      if (current) {
        this.enrol.academicYearId = current.academicYearId;
        await this.yearChosen(current.academicYearId);
      }
    } catch (error) {
      this.fail(error);
    }

    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (id) {
      await this.load(id);
    } else {
      this.addGuardian();
    }
  }

  private async load(id: number): Promise<void> {
    this.busy.set(true);
    try {
      const view = await this.api.student(id);
      this.studentId.set(view.studentId);
      this.admissionNo.set(view.admissionNo);
      this.enrolments.set(view.enrolments);
      this.form = {
        firstName: view.firstName,
        lastName: view.lastName,
        dateOfBirth: view.dateOfBirth,
        gender: view.gender,
        admissionDate: view.admissionDate,
        studentStatus: view.studentStatus,
        leavingDate: view.leavingDate,
        bloodGroup: view.bloodGroup,
        nationalId: view.nationalId,
        guardians: view.guardians.map((g) => ({ ...g })),
      };
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

  private static blank(): SaveStudent {
    return {
      firstName: '',
      lastName: null,
      dateOfBirth: '',
      gender: 'NotStated',
      admissionDate: new Date().toISOString().slice(0, 10),
      studentStatus: 'Active',
      leavingDate: null,
      bloodGroup: null,
      nationalId: null,
      guardians: [],
    };
  }
}
