const STATIC_CACHE = 'ecotrails-static-v1';
const API_CACHE = 'ecotrails-api-v1';
const TILE_CACHE = 'ecotrails-tiles-v1';

const STATIC_ASSETS = ['/', '/index.html', '/manifest.webmanifest'];
const API_PATHS = ['/api/trails/export', '/api/trails/summary', '/api/trails/offline-enrichment'];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches
      .open(STATIC_CACHE)
      .then((cache) => cache.addAll(STATIC_ASSETS))
      .catch(() => Promise.resolve()),
  );
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    (async () => {
      const valid = new Set([STATIC_CACHE, API_CACHE, TILE_CACHE]);
      const keys = await caches.keys();
      await Promise.all(keys.filter((key) => !valid.has(key)).map((key) => caches.delete(key)));
      await self.clients.claim();
    })(),
  );
});

function isApiRequest(request) {
  const url = new URL(request.url);
  return API_PATHS.some((path) => url.pathname.startsWith(path));
}

function isTileRequest(request) {
  const url = new URL(request.url);
  return url.hostname.includes('tile.openstreetmap.org') || url.hostname.includes('openrouteservice.org');
}

async function networkFirst(request, cacheName) {
  const cache = await caches.open(cacheName);
  try {
    const response = await fetch(request);
    if (response && response.ok) {
      await cache.put(request, response.clone());
    }
    return response;
  } catch {
    const cached = await cache.match(request);
    if (cached) {
      return cached;
    }

    throw new Error('Network unavailable and no cached response.');
  }
}

async function cacheFirst(request, cacheName) {
  const cache = await caches.open(cacheName);
  const cached = await cache.match(request);
  if (cached) {
    return cached;
  }

  const response = await fetch(request);
  if (response && response.ok) {
    await cache.put(request, response.clone());
  }
  return response;
}

self.addEventListener('fetch', (event) => {
  const { request } = event;
  if (request.method !== 'GET') {
    return;
  }

  if (isApiRequest(request)) {
    event.respondWith(networkFirst(request, API_CACHE));
    return;
  }

  if (isTileRequest(request)) {
    event.respondWith(cacheFirst(request, TILE_CACHE));
    return;
  }

  const url = new URL(request.url);
  if (url.origin === self.location.origin) {
    event.respondWith(cacheFirst(request, STATIC_CACHE));
  }
});