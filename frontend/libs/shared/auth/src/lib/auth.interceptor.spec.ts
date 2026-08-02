import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';

/**
 * The interceptor is where a signed-out user actually gets signed out. It reads
 * two failure shapes off the wire and does something irreversible on each — one
 * clears the session, the other navigates away — so getting either wrong is a
 * user who is thrown out of a working session, or one who is left in a dead one.
 */
describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let auth: AuthService;
  let navigate: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);

    navigate = vi.fn().mockResolvedValue(true);
    vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockImplementation(navigate);
  });

  const send = (): Promise<unknown> =>
    new Promise((resolve) => {
      http.get('/api/anything').subscribe({ next: resolve, error: resolve });
    });

  it('attaches the bearer token when there is one', async () => {
    auth.accessToken.set('a-token');

    const done = send();
    const req = httpMock.expectOne('/api/anything');

    expect(req.request.headers.get('Authorization')).toBe('Bearer a-token');
    req.flush({});
    await done;
  });

  it('sends no Authorization header when signed out', async () => {
    auth.accessToken.set(null);

    const done = send();
    const req = httpMock.expectOne('/api/anything');

    // An empty header would be worse than none: some servers treat it as a
    // malformed credential rather than as an anonymous request.
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
    await done;
  });

  it('signs the user out and returns to login on 401', async () => {
    auth.accessToken.set('a-token');
    auth.licenseStatus.set('Active');

    const done = send();
    httpMock.expectOne('/api/anything').flush(null, { status: 401, statusText: 'Unauthorized' });
    await done;

    expect(auth.isAuthenticated()).toBe(false);
    expect(navigate).toHaveBeenCalledWith('/login');
  });

  it('does nothing on a 401 for a user who was never signed in', async () => {
    // The login call itself answers 401 on a wrong password. Treating that as a
    // session expiry would navigate away from the form the user is typing in,
    // losing the error message they need to read.
    auth.accessToken.set(null);

    const done = send();
    httpMock.expectOne('/api/anything').flush(null, { status: 401, statusText: 'Unauthorized' });
    await done;

    expect(navigate).not.toHaveBeenCalled();
  });

  it('routes to the expired page on a licence 403', async () => {
    auth.accessToken.set('a-token');

    const done = send();
    httpMock
      .expectOne('/api/anything')
      .flush({ reason: 'LicenseExpired' }, { status: 403, statusText: 'Forbidden' });
    await done;

    expect(navigate).toHaveBeenCalledWith('/expired');
    // A licence 403 is not a sign-out. The user stays signed in and can renew.
    expect(auth.isAuthenticated()).toBe(true);
  });

  it('leaves an ordinary permission 403 alone', async () => {
    // A 403 for a missing permission is the page's to report. Navigating to the
    // expired page would tell a user their licence has lapsed when it has not.
    auth.accessToken.set('a-token');

    const done = send();
    httpMock
      .expectOne('/api/anything')
      .flush({ reason: 'Forbidden' }, { status: 403, statusText: 'Forbidden' });
    await done;

    expect(navigate).not.toHaveBeenCalled();
    expect(auth.isAuthenticated()).toBe(true);
  });

  it('passes the error on rather than swallowing it', async () => {
    auth.accessToken.set('a-token');

    let caught: unknown = null;
    const done = new Promise<void>((resolve) => {
      http.get('/api/anything').subscribe({
        error: (error) => {
          caught = error;
          resolve();
        },
      });
    });

    httpMock.expectOne('/api/anything').flush(null, { status: 500, statusText: 'Server Error' });
    await done;

    expect(caught).not.toBeNull();
  });
});
