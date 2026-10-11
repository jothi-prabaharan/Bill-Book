import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterOutlet } from '@angular/router';
import { AppUpdateService, UpdateModalComponent } from '@bill-book/app-update';
import { ConfirmDialogComponent } from '@bill-book/ui-components';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-root',
  standalone: true,
  imports: [RouterOutlet, UpdateModalComponent, ConfirmDialogComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent {
  private readonly updates = inject(AppUpdateService);

  /** True while the update prompt is up; a forced one also makes the page behind it inert. */
  protected readonly updatePrompt = toSignal(this.updates.visible$, { initialValue: false });
  protected readonly updateForced = toSignal(this.updates.isForced$, { initialValue: false });

  constructor() {
    // The desktop and mobile builds check version.json now and on every resume;
    // in a browser this resolves to the web platform and does nothing (TK-184).
    void this.updates.start(inject(DestroyRef));
  }
}
