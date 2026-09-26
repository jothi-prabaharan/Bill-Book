import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { readApiFailure } from '@bill-book/api-client';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  INBOX_SOURCES,
  InboxItem,
  actionUrl,
  documentRoute,
  itemKey,
  kindLabel,
  mergeInbox,
} from './approvals-inbox.model';

type Move = 'Approve' | 'Reject' | 'SendBack';

/**
 * Approvals › Waiting for me (TK-103): every document waiting on the signed-in
 * user, whichever service holds it, with approve, send back and reject
 * inline. Each move goes to the document's own approval route, so the chain
 * and its checks are the same here as on the document.
 *
 * A source the user cannot read is left out without a word; one that fails
 * for another reason is named, so a missing document is never a silent one.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-approvals-inbox-page',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './approvals-inbox.page.html',
  styleUrl: './approvals-inbox.page.scss',
})
export class ApprovalsInboxPage implements OnInit {
  private readonly http = inject(HttpClient);
  protected readonly formats = inject(FormatSettingsService);

  protected readonly kindLabel = kindLabel;
  protected readonly documentRoute = documentRoute;
  protected readonly itemKey = itemKey;

  protected readonly items = signal<InboxItem[]>([]);
  protected readonly loading = signal(true);
  protected readonly unavailable = signal<string[]>([]);
  protected readonly busyKey = signal<string | null>(null);
  protected readonly errors = signal<Record<string, string>>({});
  protected readonly message = signal<string | null>(null);

  /** The comment typed against each item, keyed by `itemKey`. */
  protected comments: Record<string, string> = {};

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    const answers = await Promise.allSettled(
      INBOX_SOURCES.map((source) => firstValueFrom(this.http.get<InboxItem[]>(source.url))),
    );

    const lists: InboxItem[][] = [];
    const failed: string[] = [];
    answers.forEach((answer, index) => {
      if (answer.status === 'fulfilled') {
        lists.push(answer.value ?? []);
      } else if (!isRefusal(answer.reason)) {
        failed.push(INBOX_SOURCES[index].name);
      }
    });

    this.items.set(mergeInbox(lists));
    this.unavailable.set(failed);
    this.loading.set(false);
  }

  protected async act(item: InboxItem, move: Move): Promise<void> {
    const key = itemKey(item);
    const comments = (this.comments[key] ?? '').trim();

    if (move !== 'Approve' && !comments) {
      this.setError(key, move === 'Reject' ? 'Add a comment to say why it is rejected.' : 'Add a comment to say what to change.');
      return;
    }

    this.busyKey.set(key);
    this.setError(key, null);
    try {
      await firstValueFrom(this.http.post(actionUrl(item), { action: move, comments: comments || null }));
      delete this.comments[key];
      this.items.update((items) => items.filter((i) => itemKey(i) !== key));
      const verb = move === 'Approve' ? 'Approved' : move === 'Reject' ? 'Rejected' : 'Sent back';
      this.message.set(`${verb}: ${kindLabel(item.requestKind).toLowerCase()} ${item.documentNo}.`);
    } catch (error) {
      this.setError(key, readApiFailure(error).text);
    } finally {
      this.busyKey.set(null);
    }
  }

  private setError(key: string, text: string | null): void {
    this.errors.update((errors) => {
      const next = { ...errors };
      if (text) {
        next[key] = text;
      } else {
        delete next[key];
      }
      return next;
    });
  }
}

/** A 403 or 404: the user holds no permission for that module, or the service has no inbox. Not a failure. */
function isRefusal(error: unknown): boolean {
  return error instanceof HttpErrorResponse && (error.status === 403 || error.status === 404);
}
