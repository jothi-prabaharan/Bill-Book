import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { readApiFailure } from '@bill-book/api-client';
import { SessionContextService } from '@bill-book/auth';
import {
  EmployeeDetail,
  HrmApiService,
  OrgMasterRow,
  Relationship,
  SaveEmployee,
  blankEmployee,
  employeeProblem,
  fullName,
} from '@bill-book/hrm-core';
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

type Tab = 'personal' | 'job' | 'statutory' | 'contact' | 'family' | 'bank' | 'background' | 'records' | 'history';

const TABS: readonly { tab: Tab; label: string }[] = [
  { tab: 'personal', label: 'Personal' },
  { tab: 'job', label: 'Job' },
  { tab: 'statutory', label: 'Statutory' },
  { tab: 'contact', label: 'Addresses & contacts' },
  { tab: 'family', label: 'Family & nominees' },
  { tab: 'bank', label: 'Bank' },
  { tab: 'background', label: 'Education & past jobs' },
  { tab: 'records', label: 'Documents & assets' },
  { tab: 'history', label: 'History' },
];

const options = <T extends string>(values: readonly T[]): BbSelectOption<T>[] =>
  values.map((value) => ({ value, label: value.replace(/([a-z])([A-Z])/g, '$1 $2') }));

/**
 * One employee, with every child table on its own tab (H1, TK-48). The same
 * page creates (`/hrm/employees/new`) and edits (`/hrm/employees/:id`).
 *
 * PAN, Aadhaar and account numbers arrive masked unless the user holds
 * `payroll.view` or is this employee; saving sends the masked values back
 * unchanged and the server keeps what it holds.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-hrm-employee-page',
  standalone: true,
  imports: [
    FormsModule,
    TextInputComponent,
    SelectComponent,
    DateInputComponent,
    NumberInputComponent,
    CheckboxComponent,
    MessageBoxComponent,
  ],
  templateUrl: './employee.page.html',
  styleUrl: '../hrm-page.scss',
})
export class EmployeePage implements OnInit {
  private readonly api = inject(HrmApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly session = inject(SessionContextService);

  protected readonly tabs = TABS;
  protected readonly tab = signal<Tab>('personal');
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);

  protected readonly employeeId = signal<number | null>(null);
  protected readonly code = signal<string | null>(null);
  protected readonly sensitiveShown = signal(true);
  protected readonly history = signal<EmployeeDetail['history']>([]);
  protected readonly canEdit = computed(() => this.session.has('employee.edit'));

  protected form: SaveEmployee = blankEmployee(new Date().toISOString().slice(0, 10));

  protected readonly departments = signal<BbSelectOption<number>[]>([]);
  protected readonly designations = signal<BbSelectOption<number>[]>([]);
  protected readonly grades = signal<BbSelectOption<number>[]>([]);
  protected readonly locations = signal<BbSelectOption<number>[]>([]);
  protected readonly costCentres = signal<BbSelectOption<number>[]>([]);
  private readonly names = signal(new Map<string, string>());

  protected readonly genders = options(['NotStated', 'Male', 'Female', 'Other'] as const);
  protected readonly maritalStatuses = options(['NotStated', 'Single', 'Married', 'Widowed', 'Divorced'] as const);
  protected readonly employmentTypes = options(['Permanent', 'Probation', 'Contract', 'PartTime', 'Intern', 'Consultant'] as const);
  protected readonly statuses = options(['Onboarding', 'Active', 'OnNotice', 'Exited'] as const);
  protected readonly relationships = options(['Spouse', 'Child', 'Father', 'Mother', 'Sibling', 'Friend', 'Other'] as const);
  protected readonly addressKinds = options(['Current', 'Permanent'] as const);
  protected readonly nominationKinds = options(['Pf', 'Gratuity', 'Insurance'] as const);
  protected readonly documentKinds = options(['Pan', 'Aadhaar', 'Passport', 'Resume', 'OfferLetter', 'Certificate', 'Other'] as const);

  protected readonly familyOptions = computed<BbSelectOption<number>[]>(() =>
    this.familyNames().map((name, index) => ({ value: index, label: name || `Member ${index + 1}` })),
  );
  private readonly familyNames = signal<string[]>([]);

  protected readonly title = computed(() => (this.employeeId() === null ? 'New employee' : this.code() ?? 'Employee'));

  ngOnInit(): void {
    void this.init();
  }

  private async init(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id');
    await this.loadMasters();
    if (id && id !== 'new') {
      await this.loadEmployee(Number(id));
    }
  }

  protected choose(tab: Tab): void {
    this.tab.set(tab);
    this.familyNames.set(this.form.familyMembers.map((f) => f.name));
  }

  protected displayName(): string {
    return fullName(this.form.firstName, this.form.middleName, this.form.lastName);
  }

  protected nameOf(kind: string, id: number | null): string {
    return id === null ? '—' : this.names().get(`${kind}:${id}`) ?? String(id);
  }

  // ---- Child rows --------------------------------------------------------

  protected addAddress(): void {
    this.form.addresses = [...this.form.addresses, { addressKind: this.form.addresses.some((a) => a.addressKind === 'Current') ? 'Permanent' : 'Current', addressLine1: '' }];
  }

  protected addContact(): void {
    this.form.contacts = [...this.form.contacts, { name: '', relationship: 'Spouse' as Relationship, phone: '', isPrimary: this.form.contacts.length === 0 }];
  }

  protected addFamilyMember(): void {
    this.form.familyMembers = [...this.form.familyMembers, { name: '', relationship: 'Spouse', isDependent: false, isEsiCovered: false }];
    this.familyNames.set(this.form.familyMembers.map((f) => f.name));
  }

  protected addNominee(): void {
    this.form.nominees = [...this.form.nominees, { familyMemberIndex: 0, nominationKind: 'Pf', sharePercent: 100 }];
  }

  protected addEducation(): void {
    this.form.education = [...this.form.education, { qualification: '', institution: '', yearOfPassing: new Date().getFullYear() }];
  }

  protected addPreviousEmployment(): void {
    this.form.previousEmployments = [...this.form.previousEmployments, { employer: '', fromDate: '', toDate: '' }];
  }

  protected addBankDetail(): void {
    this.form.bankDetails = [...this.form.bankDetails, { accountHolder: this.displayName(), accountNo: '', ifsc: '', bankName: '', isPrimary: this.form.bankDetails.length === 0 }];
  }

  protected addDocument(): void {
    this.form.documents = [...this.form.documents, { documentKind: 'Pan', attachmentKey: '' }];
  }

  protected addAsset(): void {
    this.form.assetIssues = [...this.form.assetIssues, { assetName: '', issuedDate: new Date().toISOString().slice(0, 10) }];
  }

  protected removeAt<K extends keyof SaveEmployee>(list: K, index: number): void {
    const rows = this.form[list];
    if (Array.isArray(rows)) {
      (this.form[list] as unknown[]) = rows.filter((_, at) => at !== index);
    }
    if (list === 'familyMembers') {
      this.form.nominees = this.form.nominees
        .filter((n) => n.familyMemberIndex !== index)
        .map((n) => ({ ...n, familyMemberIndex: n.familyMemberIndex > index ? n.familyMemberIndex - 1 : n.familyMemberIndex }));
      this.familyNames.set(this.form.familyMembers.map((f) => f.name));
    }
  }

  protected makePrimaryAccount(index: number): void {
    this.form.bankDetails = this.form.bankDetails.map((b, at) => ({ ...b, isPrimary: at === index }));
  }

  // ---- Save --------------------------------------------------------------

  protected async save(): Promise<void> {
    const problem = employeeProblem(this.form);
    if (problem !== null) {
      this.messages.set([{ tone: 'error', text: problem }]);
      return;
    }

    this.busy.set(true);
    try {
      const id = this.employeeId();
      if (id === null) {
        const created = await this.api.createEmployee(this.form);
        this.messages.set([{ tone: 'success', text: `${this.displayName()} is saved as ${created.code}.` }]);
        await this.router.navigate(['/hrm/employees', created.id]);
        await this.loadEmployee(created.id);
      } else {
        await this.api.updateEmployee(id, this.form);
        this.messages.set([{ tone: 'success', text: `${this.displayName()} is saved.` }]);
        await this.loadEmployee(id);
      }
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.busy.set(false);
    }
  }

  private async loadEmployee(id: number): Promise<void> {
    try {
      const detail = await this.api.employee(id);
      const { employeeId, employeeCode, sensitiveShown, history, ...form } = detail;
      this.form = form;
      this.employeeId.set(employeeId);
      this.code.set(employeeCode);
      this.sensitiveShown.set(sensitiveShown);
      this.history.set(history);
      this.familyNames.set(form.familyMembers.map((f) => f.name));
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    }
  }

  private async loadMasters(): Promise<void> {
    try {
      const [departments, designations, grades, locations, costCentres] = await Promise.all([
        this.api.organisation('departments'),
        this.api.organisation('designations'),
        this.api.organisation('grades'),
        this.api.organisation('work-locations'),
        this.api.organisation('cost-centres'),
      ]);
      const toOptions = (rows: OrgMasterRow[]) => rows.filter((r) => r.isActive).map((r) => ({ value: r.id, label: `${r.code} · ${r.name}` }));
      this.departments.set(toOptions(departments));
      this.designations.set(toOptions(designations));
      this.grades.set(toOptions(grades));
      this.locations.set(toOptions(locations));
      this.costCentres.set(toOptions(costCentres));

      const names = new Map<string, string>();
      for (const [kind, rows] of [['department', departments], ['designation', designations], ['grade', grades], ['location', locations]] as const) {
        for (const row of rows) names.set(`${kind}:${row.id}`, row.name);
      }
      this.names.set(names);

      // A new employee starts in the branch's first of each, as seeded.
      if (this.employeeId() === null) {
        this.form.departmentId ||= departments[0]?.id ?? 0;
        this.form.designationId ||= designations[0]?.id ?? 0;
        this.form.gradeId ||= grades[0]?.id ?? 0;
        this.form.workLocationId ||= locations[0]?.id ?? 0;
      }
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    }
  }
}
