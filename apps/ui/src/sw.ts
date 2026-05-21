/// <reference lib="webworker" />
import { clientsClaim } from 'workbox-core';
import { cleanupOutdatedCaches, createHandlerBoundToURL, precacheAndRoute } from 'workbox-precaching';
import { NavigationRoute, registerRoute } from 'workbox-routing';
import { NetworkFirst } from 'workbox-strategies';

declare let self: ServiceWorkerGlobalScope;

// Precarga assets
precacheAndRoute(self.__WB_MANIFEST);

cleanupOutdatedCaches();

const allowlist: undefined | RegExp[] = [/^(?!\/(api|oauth|\.well-known|login|share|swagger)\/).*/];

// Excluir llamadas a /api/, /oauth/ y /share/
registerRoute(
  ({ url }) =>
    !url.pathname.startsWith('/api/') &&
    !url.pathname.startsWith('/oauth/') &&
    !url.pathname.startsWith('/.well-known/') &&
    !url.pathname.startsWith('/login/') &&
    !url.pathname.startsWith('/share/') &&
    !url.pathname.startsWith('/swagger/'),
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
