import { Injectable, signal } from '@angular/core';
import { ConfirmOptions, ConfirmState } from './confirm-dialog.model';

@Injectable({
  providedIn: 'root',
})
export class ConfirmDialogService {
  readonly state = signal<ConfirmState | null>(null);

  /**
   * Prompts the user with a confirmation modal and returns a Promise resolving to
   * true (confirmed) or false (cancelled).
   */
  confirm(options: string | ConfirmOptions): Promise<boolean> {
    const isString = typeof options === 'string';
    const msg = isString ? options : options.message;
    const isDestructive = /delete|remove|revoke|deactivate|discard|cancel|void/i.test(msg);

    const opts: ConfirmOptions = isString
      ? {
          title: isDestructive ? 'Confirm Action' : 'Confirmation',
          message: options,
          confirmText: isDestructive ? 'Delete' : 'Confirm',
          cancelText: 'Cancel',
          tone: isDestructive ? 'danger' : 'primary',
        }
      : {
          title: options.title ?? (isDestructive ? 'Confirm Action' : 'Confirmation'),
          message: options.message,
          confirmText: options.confirmText ?? options.confirmLabel ?? (options.tone === 'danger' || isDestructive ? 'Delete' : 'Confirm'),
          cancelText: options.cancelText ?? options.cancelLabel ?? 'Cancel',
          tone: options.tone ?? (isDestructive ? 'danger' : 'primary'),
          confirmButtonClass: options.confirmButtonClass,
        };

    return new Promise<boolean>((resolve) => {
      this.state.set({
        options: opts,
        resolve: (confirmed: boolean) => {
          this.state.set(null);
          resolve(confirmed);
        },
      });
    });
  }

  close(confirmed = false): void {
    const current = this.state();
    if (current) {
      current.resolve(confirmed);
    }
  }
}
