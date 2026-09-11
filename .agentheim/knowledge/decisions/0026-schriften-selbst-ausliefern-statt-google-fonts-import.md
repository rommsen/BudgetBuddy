---
id: 0026
title: Schriften (Inter, JetBrains Mono, Space Grotesk) selbst ausliefern statt Google-Fonts-Import
scope: design-system
status: accepted
date: 2026-09-11
supersedes: [0024]
superseded_by: []
related_tasks: [design-system-f7k2p]
related_adrs: [0024]
related_research: []
---

# ADR 0026: Schriften selbst ausliefern statt Google-Fonts-Import

## Context
ADR 0024 (Decision 3) hielt fest: der Google-Fonts-`@import` in
`src/Client/styles.css` steht hinter dem `@theme`-Block und wird vom
Tailwind-4-Build verworfen ("@import rules must precede all rules"); die
gebaute App rendert deshalb in Browser-Ersatzschriften. ADR 0024 hat dafür nur
eine Übergangslösung für das Export-Bundle beschlossen (expliziter
`<link>` in den Vorschauen), den App-Bug selbst aber bewusst nicht behoben und
als `design-system-f7k2p` erfasst.

Zusätzlich meldete Claude Design beim Erst-Sync (`design-system-h58y4`):
"Missing brand fonts: Inter, JetBrains Mono, Space Grotesk, rendering with
substitutes until the font files are uploaded". Ein Google-Fonts-`<link>` im
Bundle reicht dort nicht, Claude Design erwartet die Schriften als Dateien
(Vorbild-Projekt "Smolder Design System" liefert einen `fonts/`-Ordner mit
`@font-face`).

Beide Funde lösen dieselbe Maßnahme (Entscheidung Roman, 2026-09-11):
BudgetBuddy liefert die drei Schriften künftig selbst aus, sowohl in der App
als auch im Export-Bundle. Kein `@import` mehr vor dem `@theme`-Block zu
verschieben (das hätte den ursprünglichen Fund nur behoben, nicht das
zugrundeliegende Problem: Abhängigkeit von einem externen Font-Host), sondern
`@font-face` mit lokalen Dateien.

## Decision
1. **Fontquelle:** die Variable-Font-Pakete von Fontsource (`@fontsource-variable/*`,
   Version 5.3.0), Subset `latin` (deckt deutsche Umlaute ä/ö/ü/ß ab, Unicode-Range
   U+0000-00FF u. a.), Achse `wght`, Stil `normal` (keine Kursivschnitte, die App
   nutzt keine). Eine Datei pro Schrift deckt den vollen Gewichtsbereich ab
   (Inter 100-900, JetBrains Mono 100-800, Space Grotesk 300-700): kleiner als ein
   Satz einzelner statischer Schnitte und ein Web-Font-Format weniger zu pflegen.
   Bezogen per `curl` von der jsDelivr-CDN-Spiegelung der npm-Pakete (kein
   `npm install`, siehe Diary für die genauen URLs).
2. **Dateien im Repo:** `src/Client/public/fonts/{inter,jetbrains-mono,space-grotesk}-latin-wght-normal.woff2`
   plus `src/Client/public/fonts/LICENSE.md` (SIL Open Font License 1.1, Volltext
   plus Copyright-Zeile je Schrift). `public/` wird von Vite unverändert in den
   Build kopiert (kein Hashing, kein Bundling), damit `@font-face` sie unter einem
   stabilen, root-relativen Pfad (`/fonts/...`) referenzieren kann.
3. **`src/Client/styles.css`:** der Google-Fonts-`@import` entfällt ersatzlos,
   ersetzt durch drei `@font-face`-Regeln (eine je Schrift) mit
   `src: url('/fonts/....woff2') format('woff2-variations')` und
   `font-display: swap` (unverändert aus dem alten Import übernommen).
4. **Export-Bundle (`scripts/export-design-system.mjs`, ADR 0024 Decision 3
   ersetzt):** kopiert die Font-Dateien plus `LICENSE.md` nach
   `dist/design-system/fonts/`, bindet sie per `@font-face` in
   `colors_and_type.css` ein (dieselben drei Regeln wie in `styles.css`, Pfad
   `fonts/...` relativ zur Bundle-Wurzel). Die Google-Fonts-URL-Extraktion aus
   `styles.css` und der `<link>`/`<link rel=preconnect>` in den Vorschau-Dateien
   entfallen: die Vorschauen binden ohnehin schon `colors_and_type.css` ein, das
   jetzt die Fonts selbst mitbringt.
5. **PWA:** keine Änderung an `vite.config.js` nötig,
   `globPatterns: [...,'woff','woff2']` erfasst die neuen Dateien bereits;
   verifiziert per generiertem `sw.js`/Workbox-Manifest.

## Consequences
### Positive
- Die gebaute App lädt Inter, Space Grotesk und JetBrains Mono tatsächlich
  (verifiziert: headless `document.fonts`-Check gegen `vite build`+`preview`,
  keine Anfrage an fonts.googleapis.com/fonts.gstatic.com).
- PWA hat ihre Schriften offline (Precache), die App hängt beim Laden nicht mehr
  von Google ab (schneller First Paint ohne Cross-Origin-Request, kein
  Drittanbieter-Tracking-Risiko über den Font-Host).
- Export-Bundle und App nutzen dieselbe Fontquelle: kein bewusstes Auseinanderlaufen
  mehr (ADR 0024 Decision 3 nahm das noch in Kauf, bis dieser Fix kommt).
- Claude Design bekommt die Schriften als Dateien, wie erwartet.

### Negative
- Drei zusätzliche Binärdateien im Repo (rund 110 KB zusammen, siehe Diary für
  genaue Größen): vertretbar gegenüber sechs bis zehn einzelnen statischen
  Schnitten pro Familie.
- `woff2-variations` als Format-Hinweis setzt einen Browser mit
  Variable-Font-Unterstützung voraus (alle aktuellen Chromium-/Firefox-/
  Safari-Versionen); ältere Browser fallen auf Systemschrift zurück statt auf
  einen statischen Schnitt. Für dieses Projekt (Single-User, moderne Geräte)
  akzeptiert.

## Alternatives considered
- **Nur den `@import` vor den `@theme`-Block verschieben:** hätte den
  ursprünglichen Bug behoben, aber die Abhängigkeit von Google Fonts (externer
  Request, kein Offline-Fallback, keine Dateien für Claude Design) unverändert
  gelassen. Verworfen, weil beide Funde (App-Bug und Claude-Design-Meldung)
  dieselbe Ursache haben: Fonts kommen nicht aus dem eigenen Bundle.
- **Statische Einzelschnitte statt Variable Font:** mehr Dateien, mehr
  `@font-face`-Regeln, insgesamt größer für den benötigten Gewichtsbereich.
  Verworfen zugunsten je einer Variable-Font-Datei pro Familie.
- **Fonts per `npm install` als Paketabhängigkeit (`@fontsource-variable/*`) statt
  Dateien im Repo:** hätte eine Laufzeit-Abhängigkeit von einem Bundler-Import-Pfad
  für Binärdateien erzeugt und wäre im Worktree dieses Tasks ohnehin verboten
  (siehe Task-Setup); die Dateien selbst sind identisch, nur der Bezugsweg
  unterscheidet sich. Verworfen zugunsten von Dateien direkt unter `public/fonts/`.
