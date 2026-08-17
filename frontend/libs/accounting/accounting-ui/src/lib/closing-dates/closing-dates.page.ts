import { CardTableComponent } from '@bill-book/ui-components';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface PeriodLock {
  periodLockId: number;
  roleId: number;
  /** Inclusive — nothing posts on or before this date. */
  lockedUpto: string;
  note: string | null;
}

interface Role {
  roleId: number;
  displayName: string;
  description: string | null;
  isSystemRole: boolean;
  isActive: boolean;
  userCount: number;
}

/** What the signed-in user may still post, so the screen can warn about itself. */
interface MyLock {
  lockedUpto: string | null;
  openFrom: string | null;
}

/** A role and its closing date, joined for the screen. */
interface Row {
  role: Role;
  lock: PeriodLock | null;
  /** The date being keyed, before it is saved. */
  editing: string | null;
  note: string;
}

/**
 * Settings › Closing dates. How far back the books are closed, per role.
 *
 * The screen exists because a close is rarely all-or-nothing: a month shuts to
 * everyone who keys documents while staying open to whoever has to make the
 * adjusting entries the close itself produced.
 */
@Component({
  selector: 'bb-closing-dates-page',
  standalone: true,
  imports: [CardTableComponent, FormsModule],
  templateUrl: './closing-dates.page.html',
  styleUrl: './closing-dates.page.scss',
})
export class ClosingDatesPage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly rows = signal<Row[]>([]);
  protected readonly mine = signal<MyLock | null>(null);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);

    try {
      const [roles, locks, mine] = await Promise.all([
        this.get<Role[]>('/api/roles'),
        this.get<PeriodLock[]>('/api/period-locks'),
        this.get<MyLock>('/api/period-locks/mine'),
      ]);

      this.mine.set(mine);

      // Every role is shown, whether or not it has a date. A role missing from
      // the list would read as "not locked" only if you already knew that a
      // missing row means unlocked — showing it says so.
      this.rows.set(
        roles
          .filter((r) => r.isActive)
          .map((role) => {
            const lock = locks.find((l) => l.roleId === role.roleId) ?? null;
            return {
              role,
              lock,
              editing: lock?.lockedUpto ?? null,
              note: lock?.note ?? '',
            };
          }),
      );
    } catch {
      this.fail('Could not load the closing dates.');
    } finally {
      this.busy.set(false);
    }
  }

  /**
   * Moving a date earlier reopens a period that was closed. It is the one change
   * here worth stopping to think about, so it is the one the screen asks about.
   */
  protected reopens(row: Row): boolean {
    return (
      row.lock !== null && row.editing !== null && row.editing < row.lock.lockedUpto
    );
  }

  async save(row: Row): Promise<void> {
    if (row.editing === null || row.editing === '') {
      this.fail('Choose the date to close up to.');
      return;
    }

    this.busy.set(true);
    this.message.set(null);

    try {
      await this.send('PUT', '/api/period-locks', {
        roleId: row.role.roleId,
        lockedUpto: row.editing,
        note: row.note.trim() === '' ? null : row.note.trim(),
      });

      this.notify(
        `${row.role.displayName} is now closed up to and including ${row.editing}.`,
      );

      await this.load();
    } catch (err: unknown) {
      this.fail(this.reason(err, 'Could not save that closing date.'));
    } finally {
      this.busy.set(false);
    }
  }

  async clear(row: Row): Promise<void> {
    this.busy.set(true);
    this.message.set(null);

    try {
      await this.send('DELETE', `/api/period-locks/${row.role.roleId}`, {});
      this.notify(`Every period is open again for ${row.role.displayName}.`);
      await this.load();
    } catch (err: unknown) {
      this.fail(this.reason(err, 'Could not reopen those periods.'));
    } finally {
      this.busy.set(false);
    }
  }

  private reason(err: unknown, fallback: string): string {
    const anyErr = err as { error?: { message?: string } };
    return anyErr?.error?.message ?? fallback;
  }

  private notify(text: string): void {
    this.message.set(text);
    this.messageIsError.set(false);
  }

  private fail(text: string): void {
    this.message.set(text);
    this.messageIsError.set(true);
  }

  private get<T>(url: string): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.get<T>(url).subscribe({ next: resolve, error: reject }),
    );
  }

  private send<T = unknown>(
    method: 'POST' | 'PUT' | 'PATCH' | 'DELETE',
    url: string,
    body: unknown,
  ): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http
        .request<T>(method, url, { body })
        .subscribe({ next: resolve as (value: T) => void, error: reject }),
    );
  }
}
