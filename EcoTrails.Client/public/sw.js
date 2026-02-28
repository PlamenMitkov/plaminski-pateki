self.addEventListener('install', () => {
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    (async () => {
      const cacheNames = await caches.keys();
      await Promise.all(cacheNames.map((cacheName) => caches.delete(cacheName)));
      await self.registration.unregister();

      const clients = await self.clients.matchAll({ includeUncontrolled: true, type: 'window' });
      clients.forEach((client) => {
        client.navigate(client.url);
      });
    })(),
  );
});