import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { readApiFailure } from '@bill-book/api-client';
import {
  ActApprovalRequest,
  PendingApprovalItem,
  TimeLeaveApiService,
} from '@bill-book/time-leave-core';
import { MessageBoxComponent, UiMessage } from '@bill-book/ui-components';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-approvals-inbox-page',
  standalone: true,
  imports: [CommonModule, FormsModule, MessageBoxComponent],
  templateUrl: './approvals-inbox.page.html',
  styleUrl: '../time-leave-page.scss',
})
export class ApprovalsInboxPage implements OnInit {
  private readonly api = inject(TimeLeaveApiService);

  protected readonly activeTab = signal<'pending' | 'history'>('pending');
  protected readonly pendingItems = signal<PendingApprovalItem[]>([]);
  protected readonly historyItems = signal<PendingApprovalItem[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly actingStepId = signal<number | null>(null);

  protected selectedStepId: number | null = null;
  protected actionType: 'Approve' | 'Reject' | 'SendBack' = 'Approve';
  protected actionComments = '';

  ngOnInit(): void {
    void this.load();
  }

  protected setTab(tab: 'pending' | 'history'): void {
    this.activeTab.set(tab);
    this.messages.set([]);
    if (tab === 'history' && this.historyItems().length === 0) {
      void this.loadHistory();
    }
  }

  protected async load(): Promise<void> {
    this.busy.set(true);
    this.messages.set([]);
    try {
      const items = await this.api.pendingApprovals();
      this.pendingItems.set(items);
    } catch (err) {
      const failure = readApiFailure(err);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.busy.set(false);
    }
  }

  protected async loadHistory(): Promise<void> {
    try {
      const items = await this.api.approvalHistory();
      this.historyItems.set(items);
    } catch {
      // Best-effort
    }
  }

  protected openActionModal(item: PendingApprovalItem, action: 'Approve' | 'Reject' | 'SendBack'): void {
    this.selectedStepId = item.approvalStepId;
    this.actionType = action;
    this.actionComments = '';
  }

  protected closeActionModal(): void {
    this.selectedStepId = null;
    this.actionComments = '';
  }

  protected async submitAction(): Promise<void> {
    if (!this.selectedStepId) return;

    const stepId = this.selectedStepId;
    this.actingStepId.set(stepId);
    this.messages.set([]);

    try {
      const req: ActApprovalRequest = {
        action: this.actionType,
        comments: this.actionComments.trim() || undefined,
      };
      const res = await this.api.actOnApproval(stepId, req);
      this.messages.set([{ tone: 'success', text: res.message || `${this.actionType} successful.` }]);
      this.closeActionModal();
      await this.load();
      await this.loadHistory();
    } catch (err) {
      const failure = readApiFailure(err);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.actingStepId.set(null);
    }
  }
}
