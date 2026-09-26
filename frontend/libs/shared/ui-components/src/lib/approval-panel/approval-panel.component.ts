import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import {
  ApprovalActionName,
  ApprovalChainView,
  canSubmitForApproval,
  stepStatusLabel,
} from './approval-panel.model';

/**
 * A document's approval chain (TK-100): who approved, when and with what
 * comment, and who it waits on — with "Submit for approval" for a draft and
 * approve, reject and send back for the level's approver. Any service's
 * document can host it: it speaks to `{basePath}/submit` and
 * `{basePath}/approval`, which every service that stores steps serves alike.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-approval-panel',
  standalone: true,
  imports: [DatePipe, FormsModule],
  templateUrl: './approval-panel.component.html',
  styleUrl: './approval-panel.component.scss',
})
export class ApprovalPanelComponent implements OnChanges {
  private readonly http = inject(HttpClient);

  /** The document's own API path, e.g. `/api/purchase/purchase-orders/12`. */
  @Input({ required: true }) basePath!: string;

  /** The document's lifecycle status: only a draft can be submitted. */
  @Input() documentStatus = 'Draft';

  /** Raised after a submit or an action, so the host reloads the document. */
  @Output() readonly changed = new EventEmitter<void>();

  protected readonly chain = signal<ApprovalChainView | null>(null);
  protected readonly message = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly stepStatusLabel = stepStatusLabel;
  protected comments = '';

  ngOnChanges(): void {
    void this.load();
  }

  protected canSubmit(): boolean {
    return canSubmitForApproval(this.documentStatus, this.chain());
  }

  protected async submit(): Promise<void> {
    await this.run(async () => {
      const answer = await firstValueFrom(
        this.http.post<{ approvalStatus: string | null; message?: string }>(`${this.basePath}/submit`, {}),
      );
      this.message.set(answer.message ?? (answer.approvalStatus === 'Approved' ? 'Approved: no level needed to act.' : 'Sent for approval.'));
    });
  }

  protected async act(action: ApprovalActionName): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.http.post(`${this.basePath}/approval`, { action, comments: this.comments || null }));
      this.comments = '';
      this.message.set(action === 'Approve' ? 'Approved.' : action === 'Reject' ? 'Rejected.' : 'Sent back.');
    });
  }

  private async run(work: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    this.message.set(null);
    try {
      await work();
      await this.load();
      this.changed.emit();
    } catch (err: unknown) {
      const text = err instanceof HttpErrorResponse ? (err.error as { message?: string } | null)?.message : undefined;
      this.error.set(text ?? 'That could not be done. Try again in a moment.');
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    if (!this.basePath) return;
    try {
      this.chain.set(await firstValueFrom(this.http.get<ApprovalChainView>(`${this.basePath}/approval`)));
    } catch {
      this.chain.set(null);
    }
  }
}
