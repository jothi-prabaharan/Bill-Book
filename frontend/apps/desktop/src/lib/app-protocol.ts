import * as path from 'node:path';

/**
 * The bundled web app is served on `app://bill-book/` rather than `file://`
 * (TK-184). A standard, secure scheme gives the page a real origin — local
 * storage, cookies, the camera and Angular's path routing all behave as on a
 * website — and lets `/api` be forwarded to the server from the main process,
 * so no CORS origin has to be added for the desktop.
 */
export const APP_SCHEME = 'app';
export const APP_HOST = 'bill-book';
export const APP_ORIGIN = `${APP_SCHEME}://${APP_HOST}`;

export type AppRoute =
  | { readonly kind: 'api'; readonly target: string }
  | { readonly kind: 'no-server' }
  | { readonly kind: 'file'; readonly path: string; readonly spa: boolean }
  | { readonly kind: 'not-found' };

/**
 * What a request on the app scheme maps to:
 * - `/api/...` → the same path and query on the configured server;
 * - a path with a file extension → that file under `webRoot`;
 * - any other path → `index.html`, so a reload on `/sales/invoices` works.
 * Anything that would leave `webRoot` is not found.
 */
export function routeAppRequest(rawUrl: string, webRoot: string, serverUrl: string | null): AppRoute {
  let url: URL;

  try {
    url = new URL(rawUrl);
  } catch {
    return { kind: 'not-found' };
  }

  if (url.protocol !== `${APP_SCHEME}:` || url.host !== APP_HOST) {
    return { kind: 'not-found' };
  }

  let pathname: string;

  try {
    pathname = decodeURIComponent(url.pathname);
  } catch {
    return { kind: 'not-found' };
  }

  if (pathname === '/api' || pathname.startsWith('/api/')) {
    return serverUrl ? { kind: 'api', target: `${serverUrl}${url.pathname}${url.search}` } : { kind: 'no-server' };
  }

  const root = path.resolve(webRoot);
  const last = pathname.split('/').pop() ?? '';
  const spa = !last.includes('.');
  const resolved = path.resolve(root, `.${spa ? '/index.html' : pathname}`);
  const relative = path.relative(root, resolved);

  if (relative === '' || relative.startsWith('..') || path.isAbsolute(relative)) {
    return { kind: 'not-found' };
  }

  return { kind: 'file', path: resolved, spa };
}

const MIME: Readonly<Record<string, string>> = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.webmanifest': 'application/manifest+json; charset=utf-8',
  '.map': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.gif': 'image/gif',
  '.webp': 'image/webp',
  '.ico': 'image/x-icon',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.ttf': 'font/ttf',
  '.wasm': 'application/wasm',
  '.mp3': 'audio/mpeg',
  '.wav': 'audio/wav',
  '.txt': 'text/plain; charset=utf-8',
};

/** The content type for a file; WebAssembly in particular must be `application/wasm` to stream-compile. */
export function mimeFor(file: string): string {
  return MIME[path.extname(file).toLowerCase()] ?? 'application/octet-stream';
}
