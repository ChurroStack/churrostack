import { defineConfig } from 'vite';
import path from 'path';
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react-swc';
import { oidcSpa } from 'oidc-spa/vite-plugin';
import { VitePWA } from 'vite-plugin-pwa';

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    oidcSpa(),
    VitePWA({
      mode: 'development',
      base: '/',
      registerType: 'autoUpdate',
      srcDir: 'src',
      filename: 'sw.ts',
      strategies: 'injectManifest',
      injectManifest: {
        globPatterns: ['**/*.{js,css,html,ico,png,svg}'],
        maximumFileSizeToCacheInBytes: 25 * 1024 ** 2
      },
      devOptions: {
        enabled: process.env.NODE_ENV == 'development',
        /* when using generateSW the PWA plugin will switch to classic */
        type: 'module',
        navigateFallback: 'index.html'
      },
      // add this to cache all the imports
      workbox: {
        globPatterns: ['**/*'],
        maximumFileSizeToCacheInBytes: 25 * 1024 ** 2,
        cleanupOutdatedCaches: true
      },
      // add this to cache all the
      // static assets in the public folder
      includeAssets: ['**/*'],
      manifest: {
        theme_color: '#fafafa',
        background_color: '#fafafa',
        display: 'standalone',
        scope: '/',
        start_url: '/',
        short_name: 'ChurroStack',
        description: 'Deploy apps like churros. From code to production in minutes. We fry deployments, not your brain.',
        name: 'ChurroStack Platform',
        icons: [
          {
            src: '/churrostack-192x192.png',
            sizes: '192x192',
            type: 'image/png'
          },
          {
            src: '/churrostack-512x512.png',
            sizes: '512x512',
            type: 'image/png'
          }
        ]
      }
    })
  ],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src')
    }
  },
  server: {
    port: process.env.NODE_ENV == 'development' ? 5173 : 8000,
    strictPort: true,
    host: true,
    allowedHosts: process.env.NODE_ENV == 'development' ? ['localhost', 'localhost.mac'] : []
  }
});
