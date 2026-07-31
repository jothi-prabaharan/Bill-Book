import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/** Attaches the bearer token and reacts to auth/licence failures. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const token = auth.accessToken();
  const request = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(request).pipe(
    catchError((error) => {
      if (error.status === 401 && auth.isAuthenticated()) {
        auth.logout();
        void router.navigateByUrl('/login');
      }
      if (error.status === 403 && error.error?.reason === 'LicenseExpired') {
        void router.navigateByUrl('/expired');
      }
      return throwError(() => error);
    }),
  );
};
