import { Injectable, computed, signal } from '@angular/core';

/** One line in the notifications popover. */
export interface ShellNotification {
  readonly id: string;
  /** Short uppercase category — "Invoice", "Stock", "Reconciliation". */
  readonly kind: string;
  /** Relative time as the person reads it — "2h", "Yesterday". */
  readonly when: string;
  readonly text: string;
  readonly unread: boolean;
  /** Where clicking the row goes, when it goes anywhere. */
  readonly path?: string;
}

/**
 * The notifications the top bar shows.
 *
 * No service publishes a feed yet — the only notification endpoint the backend
 * exposes is outbound email — so the list starts empty and the popover renders
 * its empty state honestly. `set()` is the seam: when a feed lands, it pushes
 * here and nothing in the chrome has to change.
 */
@Injectable({ providedIn: 'root' })
export class ShellNotificationsService {
  private readonly items = signal<readonly ShellNotification[]>([]);

  readonly notifications = computed(() => this.items());

  readonly unreadCount = computed(() => this.items().filter((n) => n.unread).length);

  readonly hasUnread = computed(() => this.unreadCount() > 0);

  set(notifications: readonly ShellNotification[]): void {
    this.items.set(notifications);
  }

  markAllRead(): void {
    this.items.update((list) =>
      list.every((n) => !n.unread) ? list : list.map((n) => ({ ...n, unread: false })),
    );
  }
}
