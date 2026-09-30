import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { APP_ID } from '@bill-book/auth';
import { EditLogArea, EditLogReportComponent } from '@bill-book/ui-components';
import { auditAreasFor } from './audit-areas';

/**
 * Settings › Audit log (TK-118): every change the edit log holds, one
 * service at a time, filtered by date, user and table. One page for every app;
 * the areas follow the app it is mounted in.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-audit-log-page',
  standalone: true,
  imports: [EditLogReportComponent],
  templateUrl: './audit-log.page.html',
  styleUrl: './audit-log.page.scss',
})
export class AuditLogPage {
  protected readonly areas: EditLogArea[] = auditAreasFor(inject(APP_ID));
}
