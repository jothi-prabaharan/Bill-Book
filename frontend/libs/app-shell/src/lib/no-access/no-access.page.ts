import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';

/**
 * Why a page was refused (H0.3, TK-44), shown inside the shell rather than a
 * silent bounce to Home.
 *
 * **It names the missing permission, never a role.** Roles are defined by each
 * customer, and the permission is what is actually missing.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-no-access',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './no-access.page.html',
  styleUrl: './no-access.page.scss',
})
export class NoAccessPage {
  private readonly route = inject(ActivatedRoute);
  private readonly query = toSignal(this.route.queryParamMap.pipe(map((q) => ({ need: q.get('need'), reason: q.get('reason') }))), {
    initialValue: { need: null, reason: null },
  });

  protected readonly permission = computed(() => permissionLabel(this.query().need));

  protected readonly message = computed(() => {
    const { need, reason } = this.query();
    if (need) {
      return "You don't have access to this page. Ask your administrator for this permission:";
    }
    switch (reason) {
      case 'businessType':
        return "This page isn't part of this branch's kind of business.";
      case 'addOn':
        return "This page belongs to an add-on this branch hasn't switched on.";
      case 'context':
        return 'Your session for this branch could not be read. Sign in again, or ask your administrator whether you still have a role here.';
      case 'undeclared':
        return 'This page has not been set up with an access rule yet, so it is closed to everyone.';
      default:
        return "You don't have access to this page.";
    }
  });
}

/** `payroll.view` as a person reads it: "Payroll: view". */
export function permissionLabel(code: string | null): string | null {
  if (!code) {
    return null;
  }
  const [module, action] = code.split('.');
  if (!module || !action) {
    return code;
  }
  return `${module.charAt(0).toUpperCase()}${module.slice(1)}: ${action}`;
}
