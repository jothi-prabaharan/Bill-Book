import { Injectable, inject, signal } from '@angular/core';
import { PlanService, readApiFailure } from '@bill-book/api-client';
import { UpgradeDialogOptions, UpgradeDialogState } from './upgrade-dialog.model';

/**
 * Asking for a higher plan, from wherever a limit is met (TK-209).
 *
 * One prompt for every limit, opened from a button or from a refused request,
 * so the words and the buttons are the same whether the Users screen is full or
 * the thousand-and-first invoice is refused. It does not take payment: the
 * request is recorded and a platform operator changes the plan (D-70).
 */
@Injectable({ providedIn: 'root' })
export class UpgradeDialogService {
  private readonly plan = inject(PlanService);

  readonly state = signal<UpgradeDialogState | null>(null);

  private opening: Promise<void> | null = null;

  /** Shows the prompt and resolves when it is closed. Asking while it is up gives the same promise. */
  open(options: UpgradeDialogOptions = {}): Promise<void> {
    this.opening ??= this.show(options).finally(() => {
      this.opening = null;
    });
    return this.opening;
  }

  /** Sends the request, with an optional note, and keeps the prompt up to say how it went. */
  async request(note: string): Promise<void> {
    const current = this.state();
    if (!current || current.busy || current.requested) {
      return;
    }

    this.patch({ busy: true, outcome: null });
    try {
      const text = await this.plan.requestUpgrade({
        toPlan: 'Elite',
        reason: current.options.limit?.kind,
        note: note.trim() || undefined,
      });
      this.patch({ busy: false, requested: true, outcome: { tone: 'success', text } });
    } catch (error) {
      const failure = readApiFailure(error);
      this.patch({ busy: false, outcome: { tone: 'error', text: failure.text, detail: failure.detail } });
    }
  }

  close(): void {
    this.state()?.resolve();
  }

  private async show(options: UpgradeDialogOptions): Promise<void> {
    const summary = await this.plan.summary().catch(() => null);

    return new Promise<void>((resolve) => {
      this.state.set({
        options,
        summary,
        busy: false,
        outcome: null,
        requested: false,
        resolve: () => {
          this.state.set(null);
          resolve();
        },
      });
    });
  }

  private patch(change: Partial<UpgradeDialogState>): void {
    this.state.update((current) => (current ? { ...current, ...change } : current));
  }
}
