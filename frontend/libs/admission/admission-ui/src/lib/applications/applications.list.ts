import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { readApiFailure } from '@bill-book/api-client';
import { IfCanDirective } from '@bill-book/auth';
import { AdmissionApiService, Application, ApplicationStage, STAGE_LABELS } from '@bill-book/admission-core';
import {
  BbSelectOption,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  SelectComponent,
  UiMessage,
} from '@bill-book/ui-components';

/** Students › Applications (S2, TK-62): every application by stage, newest first. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-admission-applications-list',
  standalone: true,
  imports: [FormsModule, DataGridComponent, DataGridCellTemplateDirective, SelectComponent, MessageBoxComponent, IfCanDirective],
  templateUrl: './applications.list.html',
  styleUrl: '../admission-page.scss',
})
export class ApplicationsList implements OnInit {
  private readonly api = inject(AdmissionApiService);
  private readonly router = inject(Router);

  protected readonly rows = signal<Application[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected stage: ApplicationStage | null = null;

  protected readonly stageOptions: BbSelectOption<ApplicationStage>[] = (Object.keys(STAGE_LABELS) as ApplicationStage[]).map((s) => ({
    value: s,
    label: STAGE_LABELS[s],
  }));

  protected readonly columns: ColumnDef[] = [
    { field: 'applicationNo', header: 'Application' },
    { field: 'applicationDate', header: 'Date' },
    { field: 'childFirstName', header: 'Child' },
    { field: 'guardianName', header: 'Guardian' },
    { field: 'applicationStage', header: 'Stage' },
    { field: 'admissionNo', header: 'Admission no.' },
  ];

  ngOnInit(): void {
    void this.load();
  }

  protected label(stage: ApplicationStage): string {
    return STAGE_LABELS[stage];
  }

  protected add(): void {
    void this.router.navigate(['/admission/applications/new']);
  }

  protected open(row: Application): void {
    void this.router.navigate(['/admission/applications', row.applicationId]);
  }

  protected async load(): Promise<void> {
    try {
      this.rows.set(await this.api.applications(this.stage));
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    }
  }
}
