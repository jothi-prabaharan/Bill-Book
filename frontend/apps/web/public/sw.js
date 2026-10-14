// Bill-Book Progressive Web App (PWA) Service Worker
// Version: 1.0.2

const CACHE_NAME = 'bill-book-pwa-v4';
const OFFLINE_URL = '/offline.html';

const PRECACHE_ASSETS = [
  OFFLINE_URL,
  '/manifest.webmanifest',
  '/icons/icon-192.png',
  '/icons/icon-512.png',
  '/assets/bill-book-mark.png',
];

// Install: precache offline fallback assets and activate immediately
self.addEventListener('install', (event) => {
  event.waitUntil(
    caches
      .open(CACHE_NAME)
      .then((cache) => cache.addAll(PRECACHE_ASSETS))
      .then(() => self.skipWaiting())
      .catch((err) => {
        // Precache error should not prevent service worker from installing
        console.warn('[PWA SW] Precache warning:', err);
        return self.skipWaiting();
      })
  );
});

// Activate: clean up older cache versions and take control of all clients
self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((cacheNames) =>
        Promise.all(
          cacheNames.map((name) => {
            if (name !== CACHE_NAME) {
              return caches.delete(name);
            }
            return null;
          })
        )
      )
      .then(() => self.clients.claim())
  );
});

// Fetch: Live ERP accounting protection
// STRICT RULE: API requests (/api/*) are never cached — double-entry ledgers require real-time server responses.
// Navigation requests: Network-first, fallback to /offline.html on network failure.
// Static assets: Network-first, fallback to cache if offline.
self.addEventListener('fetch', (event) => {
  const { request } = event;

  // 1. Only handle GET requests
  if (request.method !== 'GET') {
    return;
  }

  const url = new URL(request.url);

  // 2. Ignore non-http(s) schemes (e.g. chrome-extension://)
  if (!url.protocol.startsWith('http')) {
    return;
  }

  // 3. Strictly bypass cache for API routes, auth calls, or cross-origin API
  if (url.pathname.startsWith('/api') || url.pathname.startsWith('/auth')) {
    return;
  }

  // 4. HTML Navigation requests (pages)
  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request).catch(async () => {
        const cache = await caches.open(CACHE_NAME);
        const offlineResponse = await cache.match(OFFLINE_URL);
        if (offlineResponse) {
          return offlineResponse;
        }
        return new Response(
          '<!doctype html><html><head><title>Offline</title></head><body><h1>Offline</h1><p>Please check your connection.</p></body></html>',
          {
            status: 503,
            statusText: 'Service Unavailable',
            headers: { 'Content-Type': 'text/html' },
          }
        );
      })
    );
    return;
  }

  // 5. Static assets (JS chunks, CSS, fonts, images)
  // Network-first with cache fallback
  event.respondWith(
    fetch(request)
      .then((networkResponse) => {
        // Cache valid responses for offline resilience
        if (networkResponse && networkResponse.status === 200 && networkResponse.type === 'basic') {
          const responseToCache = networkResponse.clone();
          caches.open(CACHE_NAME).then((cache) => {
            cache.put(request, responseToCache);
          });
        }
        return networkResponse;
      })
      .catch(async () => {
        const cached = await caches.match(request);
        if (cached) {
          return cached;
        }
        // If an image asset fails while offline, return empty response
        if (request.destination === 'image') {
          return new Response('', { status: 408, statusText: 'Offline Asset Unavailable' });
        }
        throw new Error('Network error and asset not cached');
      })
  );
});

// Client messages (e.g., skipWaiting trigger)
self.addEventListener('message', (event) => {
  if (event.data && event.data.type === 'SKIP_WAITING') {
    self.skipWaiting();
  }
});
