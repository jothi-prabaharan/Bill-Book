import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { readApiFailure } from '@bill-book/api-client';
import { IfCanDirective } from '@bill-book/auth';
import { AdmissionApiService, Enquiry, EnquirySource, EnquiryStatus, SaveEnquiry } from '@bill-book/admission-core';
import { StudentApiService } from '@bill-book/student-core';
import {
  BbSelectOption,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  DateInputComponent,
  EmailInputComponent,
  MessageBoxComponent,
  PhoneInputComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
} from '@bill-book/ui-components';
import { academicOptions } from '../academic-options';

/**
 * Students › Enquiries (S2, TK-62): parents asking about a seat, with a
 * follow-up date. An enquiry becomes an application with "Make application",
 * or is marked lost.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-admission-enquiries-page',
  standalone: true,
  imports: [
    FormsModule,
    DataGridComponent,
    DataGridCellTemplateDirective,
    TextInputComponent,
    PhoneInputComponent,
    EmailInputComponent,
    DateInputComponent,
    SelectComponent,
    MessageBoxComponent,
    IfCanDirective,
  ],
  templateUrl: './enquiries.page.html',
  styleUrl: '../admission-page.scss',
})
export class EnquiriesPage implements OnInit {
  private readonly api = inject(AdmissionApiService);
  private readonly studentApi = inject(StudentApiService);
  private readonly router = inject(Router);

  protected readonly rows = signal<Enquiry[]>([]);
  protected readonly years = signal<BbSelectOption<number>[]>([]);
  protected readonly classes = signal<BbSelectOption<number>[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly editingId = signal<number | null | undefined>(undefined);

  protected status: EnquiryStatus | null = 'Open';
  protected form: SaveEnquiry = this.blank(null);

  protected readonly statusOptions: BbSelectOption<EnquiryStatus>[] = [
    { value: 'Open', label: 'Open' },
    { value: 'FollowUp', label: 'Follow up' },
    { value: 'Converted', label: 'Converted' },
    { value: 'Lost', label: 'Lost' },
  ];

  protected readonly editableStatuses = this.statusOptions.filter((s) => s.value !== 'Converted');

  protected readonly sources: BbSelectOption<EnquirySource>[] = [
    { value: 'WalkIn', label: 'Walk-in' },
    { value: 'Website', label: 'Website' },
    { value: 'Referral', label: 'Referral' },
    { value: 'Advertisement', label: 'Advertisement' },
    { value: 'Other', label: 'Other' },
  ];

  protected readonly columns: ColumnDef[] = [
    { field: 'enquiryDate', header: 'Date' },
    { field: 'childName', header: 'Child' },
    { field: 'seekingClassId', header: 'Class' },
    { field: 'parentName', header: 'Parent' },
    { field: 'phone', header: 'Phone' },
    { field: 'followUpDate', header: 'Follow up' },
    { field: 'enquiryStatus', header: 'Status' },
    { field: 'actions', header: '' },
  ];

  ngOnInit(): void {
    void this.start();
  }

  protected className(id: number): string {
    return this.classes().find((c) => c.value === id)?.label ?? '—';
  }

  protected startAdd(): void {
    this.form = this.blank(this.years()[0]?.value ?? null);
    this.editingId.set(null);
  }

  protected startEdit(row: Enquiry): void {
    this.form = { ...row };
    this.editingId.set(row.enquiryId);
  }

  protected cancel(): void {
    this.editingId.set(undefined);
  }

  protected makeApplication(row: Enquiry): void {
    void this.router.navigate(['/admission/applications/new'], { queryParams: { enquiryId: row.enquiryId } });
  }

  protected async save(): Promise<void> {
    if (!this.form.childName.trim() || !this.form.parentName.trim() || !this.form.phone.trim()) {
      this.messages.set([{ tone: 'error', text: "Give the child's name, the parent's name and a phone number." }]);
      return;
    }

    this.busy.set(true);
    try {
      await this.api.saveEnquiry(this.editingId() ?? null, this.form);
      this.messages.set([{ tone: 'success', text: 'The enquiry is saved.' }]);
      this.editingId.set(undefined);
      await this.load();
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  protected async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.rows.set(await this.api.enquiries(this.status));
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
    } catch (error) {
      this.fail(error);
    }

    await this.load();
  }

  private fail(error: unknown): void {
    const failure = readApiFailure(error);
    this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
  }

  private blank(yearId: number | null): SaveEnquiry {
    return {
      enquiryDate: new Date().toISOString().slice(0, 10),
      childName: '',
      dateOfBirth: null,
      seekingClassId: 0,
      academicYearId: yearId ?? 0,
      parentName: '',
      phone: '',
      email: null,
      enquirySource: 'WalkIn',
      enquiryStatus: 'Open',
      followUpDate: null,
    };
  }
}
