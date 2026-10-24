import { defineConfig } from 'vite';
import fable from 'vite-plugin-fable';
import tailwindcss from '@tailwindcss/vite';
import { VitePWA } from 'vite-plugin-pwa';

// infra-jh7g9: der Backend-Port ist über ASPNETCORE_URLS konfigurierbar
// (src/Server/Program.fs), also muss das Proxy-Ziel derselben Variable
// folgen, sonst redet das Frontend weiter mit dem falschen Port. BACKEND_PORT
// (nicht ASPNETCORE_URLS selbst, das eine ganze URL ist) wird von
// scripts/screenshot-route.mjs gesetzt, wenn es einen freien Port reserviert;
// ohne gesetzte Variable bleibt der bisherige Standard 5081.
const backendPort = process.env.BACKEND_PORT || '5081';

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
      // Neuer Stand ohne Cache-Löschen (infrastructure-bnbdc, ADR 0057): Im Modus
      // 'prompt' bleibt ein neuer Worker im Zustand "waiting", bis der Client ihn
      // freigibt. Wann das geschieht (beim Zurückholen der PWA, nie mitten in einer
      // Eingabe), entscheidet der Elmish-Code in src/Client/Pwa.fs und State.fs.
      registerType: 'prompt',
      // Kein registerSW.js im HTML: der Client registriert den Worker selbst über
      // `virtual:pwa-register` (src/Client/Pwa.fs). Im Dev-Server ist die PWA aus,
      // das virtuelle Modul ist dort ein No-op.
      injectRegister: false,
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
        cleanupOutdatedCaches: true,
        // Die erste Installation übernimmt die Seite sofort. Kein skipWaiting: ein
        // aktualisierter Worker wartet, bis Pwa.applyUpdate ihn freigibt (ADR 0057).
        clientsClaim: true
      }
    })
  ],
  root: './src/Client',
  server: {
    port: 5181,
    proxy: {
      '/api': {
        target: `http://localhost:${backendPort}`,
        changeOrigin: true
      }
    }
  },
  build: {
    outDir: '../../dist/public',
    emptyOutDir: true
  }
});
