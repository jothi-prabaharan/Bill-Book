import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * Attaches the bearer token, and refreshes it once when it has expired.
 *
 * **A 401 is not the end of a session any more.** An access token lives fifteen
 * minutes and a refresh token seven days; before rotation existed, the fifteen
 * minutes *was* the session, because a 401 sent the user straight back to the
 * login page. Now the first 401 on a request is answered by rotating the
 * refresh token and replaying the request with the new access token, and only a
 * failed rotation signs the user out.
 *
 * **Once, and never on the auth calls themselves.** A retry loop over
 * `/api/auth/refresh` would answer its own 401 by refreshing again; the
 * `replayed` flag and the route check are what bound it. Concurrent 401s share
 * one rotation — `AuthService.refresh` collapses them — because two requests
 * each spending the same refresh token is exactly what the server reads as
 * token reuse, and it would end the session it was trying to save.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const token = auth.accessToken();
  const request = token ? withBearer(req, token) : req;

  return next(request).pipe(
    catchError((error: unknown) => {
      const status = (error as HttpErrorResponse | null)?.status;

      // Only a signed-in caller has a session worth saving. A 401 for someone
      // who was never signed in — the login form's own refusal, say — must not
      // navigate anywhere: it would take the user off the form before they
      // could read why it failed.
      if (
        status === 401 &&
        auth.isAuthenticated() &&
        !isAuthRoute(req.url) &&
        !req.headers.has(REPLAYED)
      ) {
        return from(auth.refresh()).pipe(
          switchMap((fresh) => {
            if (fresh === null) {
              void router.navigateByUrl('/login');

              return throwError(() => error);
            }

            // Marked so a second 401 on the replay falls through to the sign-out
            // above rather than starting another rotation.
            return next(withBearer(req, fresh).clone({ setHeaders: { [REPLAYED]: '1' } }));
          }),
        );
      }

      if (status === 401 && auth.isAuthenticated()) {
        auth.logout();
        void router.navigateByUrl('/login');
      }

      if (
        status === 403 &&
        (error as HttpErrorResponse | null)?.error?.reason === 'LicenseExpired'
      ) {
        void router.navigateByUrl('/expired');
      }

      return throwError(() => error);
    }),
  );
};

/**
 * A header rather than a property bag: `HttpRequest` context would do as well,
 * but the header survives the `clone` above without a token to pass around, and
 * it is stripped by nothing on the way out because the request never leaves
 * again.
 */
const REPLAYED = 'X-Bb-Retry';

function withBearer(request: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return request.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}

/**
 * The auth endpoints refuse for reasons a refresh cannot fix — wrong password,
 * spent refresh token — so a 401 from one of them is final.
 */
function isAuthRoute(url: string): boolean {
  return url.includes('/api/auth/');
}
