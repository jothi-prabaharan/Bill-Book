import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { API_BASE_URL } from './api-base-url';
import { apiBaseUrlInterceptor } from './api-base-url.interceptor';

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

  const request = (url: string): string => {
    const http = TestBed.inject(HttpClient);
    const mock = TestBed.inject(HttpTestingController);
    http.get(url).subscribe({ error: () => undefined });
    const req = mock.match(() => true)[0];
    req.flush({});
    return req.request.url;
  };

  beforeEach(() => configure('https://api.example.com'));

  it('prefixes an /api/ call with the configured origin', () => {
    expect(request('/api/contacts')).toBe('https://api.example.com/api/contacts');
  });

  it('does not double the slash when the base url has a trailing one', () => {
    // Someone will type it with a trailing slash in an environment file, and
    // https://api.example.com//api/contacts is a different path to most servers.
    configure('https://api.example.com/');

    expect(request('/api/contacts')).toBe('https://api.example.com/api/contacts');
  });

  it('leaves the url alone when no base url is configured', () => {
    // Development serves through the dev-server proxy, where the root-relative
    // path is what the proxy is watching for.
    configure('');

    expect(request('/api/contacts')).toBe('/api/contacts');
  });

  it('leaves an absolute url alone', () => {
    expect(request('https://elsewhere.example.com/thing')).toBe(
      'https://elsewhere.example.com/thing',
    );
  });

  it('leaves a non-api path alone', () => {
    // Asset and template fetches go through the same HttpClient and must not be
    // pointed at the API host.
    expect(request('/assets/logo.svg')).toBe('/assets/logo.svg');
  });
});
