import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { API_BASE_URL } from './api-base-url';
import { apiBaseUrlInterceptor } from './api-base-url.interceptor';
import { APP_HEADER, PLATFORM_HEADER } from './client-platform';

/**
 * This runs on every request in every deployed environment, and its failure
 * mode is silent: a URL joined wrongly produces a 404 that looks like a missing
 * endpoint rather than a misconfigured base URL.
 */
describe('apiBaseUrlInterceptor', () => {
  const configure = (baseUrl: string): void => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([apiBaseUrlInterceptor])),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: baseUrl },
      ],
    });
  };

  const request = (url: string): { url: string; headers: { app: string | null; platform: string | null } } => {
    const http = TestBed.inject(HttpClient);
    const mock = TestBed.inject(HttpTestingController);
    http.get(url).subscribe({ error: () => undefined });
    const req = mock.match(() => true)[0];
    req.flush({});
    return {
      url: req.request.url,
      headers: {
        app: req.request.headers.get(APP_HEADER),
        platform: req.request.headers.get(PLATFORM_HEADER),
      },
    };
  };

  beforeEach(() => configure('https://api.example.com'));

  it('prefixes an /api/ call with the configured origin', () => {
    expect(request('/api/contacts').url).toBe('https://api.example.com/api/contacts');
  });

  it('attaches X-App and X-Platform headers to /api/ requests', () => {
    const res = request('/api/contacts');
    expect(res.headers.app).toBeTruthy();
    expect(res.headers.platform).toBeTruthy();
  });

  it('does not double the slash when the base url has a trailing one', () => {
    configure('https://api.example.com/');
    expect(request('/api/contacts').url).toBe('https://api.example.com/api/contacts');
  });

  it('leaves the url alone when no base url is configured, but still adds headers', () => {
    configure('');
    const res = request('/api/contacts');
    expect(res.url).toBe('/api/contacts');
    expect(res.headers.app).toBeTruthy();
    expect(res.headers.platform).toBeTruthy();
  });

  it('leaves an absolute non-api url alone and without app headers', () => {
    const res = request('https://elsewhere.example.com/thing');
    expect(res.url).toBe('https://elsewhere.example.com/thing');
    expect(res.headers.app).toBeNull();
    expect(res.headers.platform).toBeNull();
  });

  it('leaves a non-api path alone and without app headers', () => {
    const res = request('/assets/logo.svg');
    expect(res.url).toBe('/assets/logo.svg');
    expect(res.headers.app).toBeNull();
    expect(res.headers.platform).toBeNull();
  });
});
