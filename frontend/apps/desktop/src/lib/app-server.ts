import { net, protocol } from 'electron';
import { readFile } from 'node:fs/promises';
import * as path from 'node:path';
import { APP_SCHEME, mimeFor, routeAppRequest } from './app-protocol';

/** Registered before the app is ready; the scheme then behaves like https. */
export function registerAppScheme(): void {
  protocol.registerSchemesAsPrivileged([
    {
      scheme: APP_SCHEME,
      privileges: { standard: true, secure: true, supportFetchAPI: true, corsEnabled: true, stream: true, codeCache: true },
    },
  ]);
}

function json(status: number, message: string): Response {
  return new Response(JSON.stringify({ message }), {
    status,
    headers: { 'content-type': 'application/json; charset=utf-8' },
  });
}

async function file(filePath: string): Promise<Response> {
  const body = await readFile(filePath);
  const isIndex = path.basename(filePath) === 'index.html';

  return new Response(body, {
    headers: {
      'content-type': mimeFor(filePath),
      // Hashed bundles never change; index.html must be re-read after an update.
      'cache-control': isIndex ? 'no-cache' : 'public, max-age=31536000, immutable',
    },
  });
}

/**
 * Serves the bundled web build and forwards `/api` to the server (TK-184).
 * The forwarded request keeps its method, headers and body; the Origin and
 * Referer of the app scheme are dropped, since the server is now talking to
 * the desktop app rather than to a browser page.
 */
export function serveApp(webRoot: string, serverUrl: () => string | null): void {
  protocol.handle(APP_SCHEME, async (request) => {
    const route = routeAppRequest(request.url, webRoot, serverUrl());

    switch (route.kind) {
      case 'api': {
        const headers = new Headers(request.headers);
        headers.delete('origin');
        headers.delete('referer');
        const hasBody = request.method !== 'GET' && request.method !== 'HEAD';

        try {
          return await net.fetch(route.target, {
            method: request.method,
            headers,
            body: hasBody ? request.body : null,
            redirect: 'manual',
            ...(hasBody ? { duplex: 'half' } : {}),
          } as RequestInit);
        } catch {
          return json(503, 'Bill Book cannot reach the server. Check the connection and try again.');
        }
      }

      case 'no-server':
        return json(503, 'No server is set for this desktop app. Set serverUrl in desktop-config.json.');

      case 'file':
        try {
          return await file(route.path);
        } catch {
          // A page address with a dot in it still opens the app; a missing asset is a 404.
          const navigation = (request.headers.get('accept') ?? '').includes('text/html');
          return navigation ? file(path.join(webRoot, 'index.html')) : new Response(null, { status: 404 });
        }

      default:
        return new Response(null, { status: 404 });
    }
  });
}
