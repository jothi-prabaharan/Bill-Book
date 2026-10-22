import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { planLimitOf } from '@bill-book/api-client';
import { SessionContextService } from '@bill-book/auth';
import { UpgradeDialogService } from '@bill-book/ui-components';
import { catchError, throwError } from 'rxjs';

/**
 * Opens the upgrade prompt when a Sales or Purchase document is refused at the
 * plan's monthly limit (TK-210), so no form needs handling of its own.
 *
 * The refusal still reaches the form: the error is rethrown untouched, and the
 * form shows the server's sentence in its message box as it does for any other
 * refusal. The prompt is the way forward, not a replacement for the words.
 *
 * **A limit on users is left to the Users screen**, which has the seat banner and
 * the upgrade button and closes its own invite dialog first; a second dialog over
 * the first would be in the way.
 *
 * Someone without *Settings: edit* is told to ask an administrator and is not
 * offered the request, which the server would refuse them anyway.
 */
export const planLimitInterceptor: HttpInterceptorFn = (req, next) => {
  const dialog = inject(UpgradeDialogService);
  const session = inject(SessionContextService);

  return next(req).pipe(
    catchError((error: unknown) => {
      const limit = planLimitOf(error);
      if (limit && limit.kind !== 'Users') {
        void dialog.open({ limit, canRequest: session.has('settings.edit') });
      }

      return throwError(() => error);
    }),
  );
};
