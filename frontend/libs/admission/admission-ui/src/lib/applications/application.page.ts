import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { readApiFailure } from '@bill-book/api-client';
import { IfCanDirective } from '@bill-book/auth';
import {
  AdmissionApiService,
  AdmitRequest,
  Application,
  ApplicationStage,
  ChildGender,
  DocumentKind,
  ParentRelationship,
  STAGE_LABELS,
  SaveApplication,
  canAdmit,
  nextStages,
} from '@bill-book/admission-core';
import { StudentApiService } from '@bill-book/student-core';
import {
  BbSelectOption,
  CheckboxComponent,
  DateInputComponent,
  EmailInputComponent,
  MessageBoxComponent,
  MoneyInputComponent,
  NumberInputComponent,
  PhoneInputComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
} from '@bill-book/ui-components';
import { academicOptions } from '../academic-options';

/**
 * An application (S2, TK-62): the child, the guardian, the documents, the
 * stage, and Admit. Admitting makes the guardian a contact (the same mobile
 * number reuses one) and the child a student, and can enrol them in a section.
 * It can safely be pressed again: nothing is made twice.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-admission-application-page',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    TextInputComponent,
    PhoneInputComponent,
    EmailInputComponent,
    DateInputComponent,
    SelectComponent,
    NumberInputComponent,
    MoneyInputComponent,
    CheckboxComponent,
    MessageBoxComponent,
    IfCanDirective,
  ],
  templateUrl: './application.page.html',
  styleUrl: '../admission-page.scss',
})
export class ApplicationPage implements OnInit {
  private readonly api = inject(AdmissionApiService);
  private readonly studentApi = inject(StudentApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly application = signal<Application | null>(null);
  protected readonly years = signal<BbSelectOption<number>[]>([]);
  protected readonly classes = signal<BbSelectOption<number>[]>([]);
  protected readonly sections = signal<BbSelectOption<number>[]>([]);

  protected readonly stage = computed<ApplicationStage>(() => this.application()?.applicationStage ?? 'Submitted');
  protected readonly isClosed = computed(() => ['Admitted', 'Rejected', 'Withdrawn'].includes(this.stage()));
  protected readonly moves = computed(() => (this.application() ? nextStages(this.stage()) : []));
  protected readonly admittable = computed(() => canAdmit(this.stage()));

  protected form: SaveApplication = ApplicationPage.blank();
  protected score: number | null = null;
  protected admit: AdmitRequest = { admissionDate: new Date().toISOString().slice(0, 10), sectionId: null, rollNo: null };

  protected readonly genders: BbSelectOption<ChildGender>[] = [
    { value: 'Female', label: 'Female' },
    { value: 'Male', label: 'Male' },
    { value: 'Other', label: 'Other' },
    { value: 'NotStated', label: 'Not stated' },
  ];

  protected readonly relationships: BbSelectOption<ParentRelationship>[] = [
    { value: 'Mother', label: 'Mother' },
    { value: 'Father', label: 'Father' },
    { value: 'Guardian', label: 'Guardian' },
    { value: 'Other', label: 'Other' },
  ];

  protected readonly documentKinds: BbSelectOption<DocumentKind>[] = [
    { value: 'BirthCertificate', label: 'Birth certificate' },
    { value: 'TransferCertificate', label: 'Transfer certificate' },
    { value: 'ReportCard', label: 'Report card' },
    { value: 'Photo', label: 'Photo' },
    { value: 'AddressProof', label: 'Address proof' },
    { value: 'Other', label: 'Other' },
  ];

  ngOnInit(): void {
    void this.start();
  }

  protected label(stage: ApplicationStage): string {
    return STAGE_LABELS[stage];
  }

  protected addDocument(): void {
    this.form.documents = [...this.form.documents, { documentKind: 'BirthCertificate', attachmentKey: null, remarks: null, isVerified: false }];
  }

  protected removeDocument(index: number): void {
    this.form.documents = this.form.documents.filter((_, i) => i !== index);
  }

  protected async save(): Promise<void> {
    if (!this.form.childFirstName.trim() || !this.form.guardianName.trim() || !this.form.guardianPhone.trim()) {
      this.messages.set([{ tone: 'error', text: "Give the child's first name, and the guardian's name and mobile number." }]);
      return;
    }

    const id = this.application()?.applicationId ?? null;
    this.busy.set(true);
    try {
      const saved = await this.api.saveApplication(id, this.form);
      this.messages.set([{ tone: 'success', text: 'The application is saved.' }]);
      if (id === null) {
        await this.router.navigate(['/admission/applications', saved.id], { replaceUrl: true });
      }
      await this.load(saved.id);
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  protected async move(to: ApplicationStage): Promise<void> {
    const current = this.application();
    if (!current) {
      return;
    }

    this.busy.set(true);
    try {
      await this.api.move(current.applicationId, to, this.score);
      this.messages.set([{ tone: 'success', text: `The application is now ${STAGE_LABELS[to].toLowerCase()}.` }]);
      await this.load(current.applicationId);
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  protected async doAdmit(): Promise<void> {
    const current = this.application();
    if (!current) {
      return;
    }

    this.busy.set(true);
    try {
      const result = await this.api.admit(current.applicationId, this.admit);
      this.messages.set([{ tone: 'success', text: `Admitted as ${result.admissionNo}.` }]);
      await this.load(current.applicationId);
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  private async start(): Promise<void> {
    try {
      const options = await academicOptions(this.studentApi);
      this.years.set(options.years);
      this.classes.set(options.classes);
      this.form.academicYearId = options.currentYearId ?? 0;
    } catch (error) {
      this.fail(error);
    }

    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (id) {
      await this.load(id);
      return;
    }

    const enquiryId = Number(this.route.snapshot.queryParamMap.get('enquiryId'));
    if (enquiryId) {
      await this.fromEnquiry(enquiryId);
    }
  }

  /** A new application filled in from the enquiry it converts. */
  private async fromEnquiry(enquiryId: number): Promise<void> {
    try {
      const enquiry = (await this.api.enquiries()).find((e) => e.enquiryId === enquiryId);
      if (!enquiry) {
        return;
      }

      const [first, ...rest] = enquiry.childName.trim().split(' ');
      this.form = {
        ...this.form,
        enquiryId,
        childFirstName: first ?? '',
        childLastName: rest.join(' ') || null,
        dateOfBirth: enquiry.dateOfBirth ?? '',
        seekingClassId: enquiry.seekingClassId,
        academicYearId: enquiry.academicYearId,
        guardianName: enquiry.parentName,
        guardianPhone: enquiry.phone,
        guardianEmail: enquiry.email,
      };
    } catch (error) {
      this.fail(error);
    }
  }

  private async load(id: number): Promise<void> {
    try {
      const application = await this.api.application(id);
      this.application.set(application);
      this.score = application.assessmentScore;
      this.form = { ...application, documents: application.documents.map((d) => ({ ...d })) };

      const sections = await this.studentApi.sections(application.academicYearId);
      this.sections.set(
        sections.filter((s) => s.schoolClassId === application.seekingClassId).map((s) => ({ value: s.sectionId, label: `${s.className} ${s.name}` })),
      );
    } catch (error) {
      this.fail(error);
    }
  }

  private fail(error: unknown): void {
    const failure = readApiFailure(error);
    this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
  }

  private static blank(): SaveApplication {
    return {
      enquiryId: null,
      applicationDate: new Date().toISOString().slice(0, 10),
      childFirstName: '',
      childLastName: null,
      dateOfBirth: '',
      childGender: 'NotStated',
      seekingClassId: 0,
      academicYearId: 0,
      guardianName: '',
      guardianPhone: '',
      guardianEmail: null,
      guardianRelationship: 'Mother',
      applicationFee: 0,
      documents: [],
    };
  }
}
