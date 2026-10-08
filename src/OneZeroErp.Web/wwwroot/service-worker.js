const CACHE = 'onezero-public-v3';
const STATIC_ASSETS = [
  '/app.css', '/theme.css', '/theme.js', '/sidebar.css', '/crud.css',
  '/pwa.js', '/offline.html', '/manifest.webmanifest',
  '/icons/icon-192.png', '/icons/icon-512.png'
];
self.addEventListener('install', event => {
  event.waitUntil(caches.open(CACHE).then(cache => cache.addAll(STATIC_ASSETS)));
});
self.addEventListener('message', event => {
  if (event.data?.type === 'ACTIVATE_UPDATE') self.skipWaiting();
});
self.addEventListener('activate', event => event.waitUntil((async () => {
  for (const key of await caches.keys()) {
    if ((key.startsWith('onezero-shell-') || key.startsWith('onezero-public-')) && key !== CACHE) await caches.delete(key);
  }
  await self.clients.claim();
})()));
self.addEventListener('fetch', event => {
  const url = new URL(event.request.url);
  if (event.request.method !== 'GET' || url.origin !== self.location.origin) return;
  if (event.request.mode === 'navigate') {
    // Authenticated pages are never cached, including redirected login pages.
    event.respondWith(fetch(event.request).catch(() => caches.match('/offline.html')));
    return;
  }
  if (url.search || !STATIC_ASSETS.includes(url.pathname)) return;
  event.respondWith(fetch(event.request).then(async response => {
    if (response.ok && !response.redirected && response.type === 'basic') {
      const cache = await caches.open(CACHE);
      await cache.put(event.request, response.clone());
    }
    return response;
  }).catch(() => caches.match(event.request)));
});
