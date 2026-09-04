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

  /**
   * Lets the pending microtasks run.
   *
   * The refresh goes out from a promise (`firstValueFrom`), so it is not on the
   * mock backend the instant the 401 is flushed — it arrives a microtask later.
   * Without this the assertions run before the request exists and read as "no
   * refresh was sent".
   */
  const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 0));

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

  it('refreshes and replays the request on a 401, rather than ending the session', async () => {
    // The whole point of rotation. Before it, a fifteen-minute access token was
    // a fifteen-minute session, because this branch sent the user to /login.
    auth.accessToken.set('stale-token');
    localStorage.setItem('bb.refresh', 'a-refresh-token');

    const done = send();

    httpMock.expectOne('/api/anything').flush(null, { status: 401, statusText: 'Unauthorized' });
    await tick();

    const refresh = httpMock.expectOne('/api/auth/refresh');
    expect(refresh.request.body).toEqual({ refreshToken: 'a-refresh-token' });

    refresh.flush({
      accessToken: 'fresh-token',
      refreshToken: 'a-new-refresh-token',
      accessExpiresInSeconds: 900,
      licenseStatus: 'Active',
      licenseExpiry: null,
      expiryIsBranchLevel: false,
    });
    await tick();

    // Replayed with the new token, not the stale one.
    const replay = httpMock.expectOne('/api/anything');
    expect(replay.request.headers.get('Authorization')).toBe('Bearer fresh-token');
    replay.flush({ ok: true });

    await done;

    expect(navigate).not.toHaveBeenCalled();
    expect(auth.isAuthenticated()).toBe(true);

    // The rotated token replaced the spent one. Keeping the old one would make
    // the next refresh look like reuse and end the session.
    expect(localStorage.getItem('bb.refresh')).toBe('a-new-refresh-token');
  });

  it('signs the user out when the refresh itself is refused', async () => {
    auth.accessToken.set('stale-token');
    localStorage.setItem('bb.refresh', 'a-spent-token');

    const done = send();

    httpMock.expectOne('/api/anything').flush(null, { status: 401, statusText: 'Unauthorized' });
    await tick();

    httpMock
      .expectOne('/api/auth/refresh')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    await done;

    expect(auth.isAuthenticated()).toBe(false);
    expect(navigate).toHaveBeenCalledWith('/login');
  });

  it('does not try to refresh a 401 from an auth endpoint', async () => {
    // /api/auth/refresh answering 401 must not start another refresh, or a
    // spent token loops until the stack gives out.
    auth.accessToken.set('a-token');
    localStorage.setItem('bb.refresh', 'a-refresh-token');

    const done = new Promise((resolve) => {
      http.post('/api/auth/select-organization', {}).subscribe({ next: resolve, error: resolve });
    });

    httpMock
      .expectOne('/api/auth/select-organization')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    await done;

    httpMock.verify();
  });

  it('sends one refresh for a burst of 401s, not one each', async () => {
    // Two refreshes with the same token is precisely what the server reads as
    // reuse — it would end the session this is trying to save.
    auth.accessToken.set('stale-token');
    localStorage.setItem('bb.refresh', 'a-refresh-token');

    const first = send();
    const second = send();

    const failures = httpMock.match('/api/anything');
    expect(failures.length).toBe(2);
    failures.forEach((r) => r.flush(null, { status: 401, statusText: 'Unauthorized' }));
    await tick();

    const refreshes = httpMock.match('/api/auth/refresh');
    expect(refreshes.length).toBe(1);

    refreshes[0].flush({
      accessToken: 'fresh-token',
      refreshToken: 'a-new-refresh-token',
      accessExpiresInSeconds: 900,
      licenseStatus: 'Active',
      licenseExpiry: null,
      expiryIsBranchLevel: false,
    });
    await tick();

    httpMock.match('/api/anything').forEach((r) => r.flush({ ok: true }));

    await Promise.all([first, second]);
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
