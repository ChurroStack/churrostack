/// <reference lib="webworker" />
import { clientsClaim } from 'workbox-core';
import { cleanupOutdatedCaches, createHandlerBoundToURL, precacheAndRoute } from 'workbox-precaching';
import { NavigationRoute, registerRoute } from 'workbox-routing';
import { NetworkFirst } from 'workbox-strategies';

declare let self: ServiceWorkerGlobalScope;

// Precarga assets
precacheAndRoute(self.__WB_MANIFEST);

cleanupOutdatedCaches();

// /mcp has no trailing slash (that's how MCP clients call it), unlike the other prefixes here,
// so it needs its own alternative rather than joining the startsWith(.../) list below.
const allowlist: undefined | RegExp[] = [
  /^(?!\/(api|oauth|\.well-known|login|share|swagger)\/|\/mcp(\/|$)).*/
];

// Excluir llamadas a /api/, /oauth/, /share/ y /mcp
registerRoute(
  ({ url }) =>
    !url.pathname.startsWith('/api/') &&
    !url.pathname.startsWith('/oauth/') &&
    !url.pathname.startsWith('/.well-known/') &&
    !url.pathname.startsWith('/login/') &&
    !url.pathname.startsWith('/share/') &&
    !url.pathname.startsWith('/swagger/') &&
    url.pathname !== '/mcp' &&
    !url.pathname.startsWith('/mcp/'),
  new NetworkFirst({
    cacheName: 'default-cache'
  })
);

registerRoute(new NavigationRoute(createHandlerBoundToURL('index.html'), { allowlist }));

self.skipWaiting();
clientsClaim();

self.addEventListener('message', (event) => {
  if (event.data && event.data.type === 'SKIP_WAITING') {
    self.skipWaiting();
  }
});

self.addEventListener('install', () => {
  console.log('Service Worker: Instalación completada');
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  console.log('Service Worker: Activación completada');
  event.waitUntil(
    Promise.all([
      self.clients.claim(),
      // Limpiar caches antiguos
      caches.keys().then((cacheNames) => {
        return Promise.all(
          cacheNames.map((cacheName) => {
            return caches.delete(cacheName);
          })
        );
      })
    ])
  );
});
