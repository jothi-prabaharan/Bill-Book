import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { SessionContextService } from '@bill-book/auth';
import { EditLogArea, EditLogReportComponent } from '@bill-book/ui-components';
import { auditAreasFor } from './audit-areas';

/**
 * Settings › Audit log (TK-118): every change the edit log holds, one
 * service at a time, filtered by date, user and table. The areas follow the
 * branch's business type and add-ons (TK-136).
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
  private readonly session = inject(SessionContextService);

  protected readonly areas = computed<EditLogArea[]>(() =>
    auditAreasFor(this.session.context()?.businessType, this.session.context()?.addOns),
  );
}
