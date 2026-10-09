import * as path from 'node:path';
import { describe, expect, it } from 'vitest';
import { APP_ORIGIN, mimeFor, routeAppRequest } from './app-protocol';

const WEB = path.resolve('/opt/bill-book/resources/app/web');
const SERVER = 'https://books.example.com';

describe('routeAppRequest', () => {
  it('forwards /api calls to the server with their path and query', () => {
    expect(routeAppRequest(`${APP_ORIGIN}/api/sales/invoices?page=2&q=a%20b`, WEB, SERVER)).toEqual({
      kind: 'api',
      target: 'https://books.example.com/api/sales/invoices?page=2&q=a%20b',
    });
  });

  it('says there is no server rather than guessing one', () => {
    expect(routeAppRequest(`${APP_ORIGIN}/api/auth/login`, WEB, null)).toEqual({ kind: 'no-server' });
  });

  it('serves a file with an extension from the web build', () => {
    expect(routeAppRequest(`${APP_ORIGIN}/assets/zxing/zxing_reader.wasm`, WEB, SERVER)).toEqual({
      kind: 'file',
      path: path.join(WEB, 'assets/zxing/zxing_reader.wasm'),
      spa: false,
    });
  });

  it('answers any page address with index.html, so a reload stays on the page', () => {
    for (const page of ['/', '/sales/invoices', '/sales/invoices/42/edit']) {
      expect(routeAppRequest(`${APP_ORIGIN}${page}`, WEB, SERVER)).toEqual({
        kind: 'file',
        path: path.join(WEB, 'index.html'),
        spa: true,
      });
    }
  });

  it('never serves a file from outside the web build', () => {
    // An escaped slash survives the URL parser and is decoded here; the climb is refused.
    expect(routeAppRequest(`${APP_ORIGIN}/..%2F..%2Fdesktop-config.json`, WEB, SERVER)).toEqual({ kind: 'not-found' });
    expect(routeAppRequest(`${APP_ORIGIN}/assets/..%2F..%2F..%2Fmain.js`, WEB, SERVER)).toEqual({ kind: 'not-found' });
  });

  it('keeps dot segments the URL parser resolves inside the web build', () => {
    expect(routeAppRequest(`${APP_ORIGIN}/%2e%2e/%2e%2e/main.js`, WEB, SERVER)).toEqual({
      kind: 'file',
      path: path.join(WEB, 'main.js'),
      spa: false,
    });
  });

  it('refuses another host or scheme', () => {
    expect(routeAppRequest('app://elsewhere/index.html', WEB, SERVER)).toEqual({ kind: 'not-found' });
    expect(routeAppRequest('https://bill-book/index.html', WEB, SERVER)).toEqual({ kind: 'not-found' });
    expect(routeAppRequest('not a url', WEB, SERVER)).toEqual({ kind: 'not-found' });
  });

  it('does not treat a path that merely starts with "api" as the API', () => {
    expect(routeAppRequest(`${APP_ORIGIN}/apiary`, WEB, SERVER)).toMatchObject({ kind: 'file', spa: true });
  });
});

describe('mimeFor', () => {
  it('names WebAssembly as application/wasm, which streaming compilation requires', () => {
    expect(mimeFor('/web/assets/zxing/zxing_writer.wasm')).toBe('application/wasm');
  });

  it('names the bundle types and falls back to bytes', () => {
    expect(mimeFor('main-ABC.js')).toBe('text/javascript; charset=utf-8');
    expect(mimeFor('styles.CSS')).toBe('text/css; charset=utf-8');
    expect(mimeFor('index.html')).toBe('text/html; charset=utf-8');
    expect(mimeFor('data.bin')).toBe('application/octet-stream');
  });
});
