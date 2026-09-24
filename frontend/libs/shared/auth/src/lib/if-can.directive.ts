import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';
import { SessionContextService } from './session-context.service';

/**
 * Shows its content only when the session holds a permission (H0.3, TK-44):
 * `<button *bbIfCan="'payroll.post'">Post</button>`.
 *
 * It reads the same `SessionContextService` the page guard does, so a page and
 * its buttons agree about every permission. Hiding a button is the user
 * interface's half; the server refuses the request regardless.
 */
@Directive({
  selector: '[bbIfCan]',
  standalone: true,
})
export class IfCanDirective {
  private readonly session = inject(SessionContextService);
  private readonly template = inject(TemplateRef<unknown>);
  private readonly view = inject(ViewContainerRef);

  readonly bbIfCan = input.required<string>();

  private shown = false;

  constructor() {
    effect(() => {
      const allowed = this.session.has(this.bbIfCan());
      if (allowed && !this.shown) {
        this.view.createEmbeddedView(this.template);
        this.shown = true;
      } else if (!allowed && this.shown) {
        this.view.clear();
        this.shown = false;
      }
    });
  }
}
