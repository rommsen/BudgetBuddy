import { defineConfig } from 'vite';
import fable from 'vite-plugin-fable';
import tailwindcss from '@tailwindcss/vite';
import { VitePWA } from 'vite-plugin-pwa';

export default defineConfig({
  plugins: [
    fable({
      fsproj: './src/Client/Client.fsproj',
      babel: {
        plugins: []
      }
    }),
    tailwindcss(),
    // PWA: installable, shell-only precache. NO data caching by design —
    // BudgetBuddy is a live-data companion (Comdirect -> YNAB); cached financial
    // data would be actively harmful (stale amounts, dedup/ImportId confusion).
    // /api/* (Fable.Remoting) is therefore never cached: it is excluded from the
    // navigate fallback (navigateFallbackDenylist) and not matched by any runtime
    // caching route (there are none). See ADR 0010.
    VitePWA({
      // Silent updates: Single-User, deploy-controlled by Roman. The installed
      // shell refreshes on next load/navigate without a "new version" prompt.
      registerType: 'autoUpdate',
      // Inject a standalone registration <script> into index.html. The Fable entry
      // (App.fs -> compiled JS) is not a convenient host for a virtual import, so we
      // let the plugin own SW registration in the HTML. Disabled in dev (no SW built).
      injectRegister: 'script',
      // App served at the tailnet host root (tailscale serve --https=443 -> :5001);
      // Giraffe serves dist/public at /. SW scope is therefore /, no Vite base.
      scope: '/',
      includeAssets: [
        'favicon.svg',
        'icons/favicon.ico',
        'icons/favicon-16.png',
        'icons/favicon-32.png',
        'icons/apple-touch-icon.png'
      ],
      manifest: {
        name: 'BudgetBuddy',
        short_name: 'BB',
        description: 'YNAB-Companion: Comdirect -> YNAB sync.',
        display: 'standalone',
        start_url: '/',
        scope: '/',
        theme_color: '#08081a',
        background_color: '#08081a',
        icons: [
          { src: 'icons/icon-192.png', sizes: '192x192', type: 'image/png', purpose: 'any' },
          { src: 'icons/icon-512.png', sizes: '512x512', type: 'image/png', purpose: 'any' },
          { src: 'icons/maskable-512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' }
        ]
      },
      workbox: {
        // Precache ONLY the built app shell. offline.html is a public asset and is
        // matched by the html glob below so it gets precached as the navigate floor.
        globPatterns: ['**/*.{js,css,html,ico,png,svg,woff,woff2}'],
        // Navigation floor: if a navigation can be served from neither network nor
        // precache, fall back to the branded offline page (NOT index.html — index
        // needs the server/Tailscale to be useful, offline.html is honest about it).
        navigateFallback: '/offline.html',
        // Never treat /api/* as a navigation -> never serve the SPA/offline shell
        // for an API request; /api stays strictly network-only (uncached).
        navigateFallbackDenylist: [/^\/api\//],
        // No runtime caching routes at all -> no API/data response ever enters the
        // cache. The only cache is the precache (the static shell).
        runtimeCaching: [],
        cleanupOutdatedCaches: true
      }
    })
  ],
  root: './src/Client',
  server: {
    port: 5181,
    proxy: {
      '/api': {
        target: 'http://localhost:5081',
        changeOrigin: true
      }
    }
  },
  build: {
    outDir: '../../dist/public',
    emptyOutDir: true
  }
});
