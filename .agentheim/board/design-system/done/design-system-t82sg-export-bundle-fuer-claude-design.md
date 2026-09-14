---
id: design-system-t82sg
title: Wiederholbarer Export des Design-Systems als Claude-Design-Bundle (Tokens + Kassenbuch-Komponenten aus der /styleguide-Route)
status: done
type: feature
context: design-system
created: 2026-09-11
completed: 2026-09-11
depends_on: [design-system-001]
blocks: [design-system-h58y4]
tags: [frontend, design-system, claude-design, export]
related_adrs: [0024]
related_research: []
prior_art: [design-system-003, design-system-001, design-system-008]
---

## Why
Das Kassenbuch braucht vor dem Bau weiterer Screens (Gesamtsicht) einen UX-Durchgang, und
der soll mit Mockups in Claude Design (claude.ai/design) laufen. Damit die Mockups wie
BudgetBuddy aussehen und nicht wie eine Nachempfindung, braucht Claude Design ein
Design-System-Projekt mit den echten Tokens und Komponenten.

Die Quelle der Wahrheit bleibt das Repo (`src/Client/styles.css`,
`src/Client/DesignSystem/*.fs`, `standards/frontend/styleguide.md`). Claude Design bekommt
eine abgeleitete Kopie, **nur in diese Richtung**. Von Hand nachgebaute Vorschauen würden
genau die Drift erzeugen, die das Styleguide-Gate verhindern soll. Deshalb wird das Bundle
aus dem gerenderten Code erzeugt, und der Export muss wiederholbar sein, damit nach einer
DS-Änderung ein Re-Sync ein Kommando ist.

Dieser Task baut nur den Export. Das Hochladen macht `design-system-h58y4` in einer
Session mit Roman, weil Worker keinen Zugriff auf das `DesignSync`-Werkzeug haben und jeder
Schreibplan von Roman freigegeben wird.

## What
Ein Kommando (`npm run export:design-system`) erzeugt unter `dist/design-system/`
(git-ignoriert, `dist/` steht schon in `.gitignore`) ein Bundle in der Form, die Claude
Design für Design-System-Projekte erwartet. Struktur-Vorbild ist Romans bestehendes
Projekt "Smolder Design System":

```
dist/design-system/
  colors_and_type.css        Tokens: der @theme-Block aus src/Client/styles.css plus die
                             Basis-Variablen, auf die er verweist (--bg-app, --text-*, ...)
  styles.css                 das gebaute App-CSS, damit gerenderte Komponenten stimmen
  preview/
    color-neon.html          Token-Karten: Neon-Farben, Flächen, Text, Typografie
    color-surfaces.html
    type-*.html
    components-card.html     Komponenten-Karten, gerendert aus /#/styleguide
    components-money.html
    components-badge.html
    components-table.html    (Listenzeile)
    components-bottomsheet.html
    components-navigation.html
  assets/
    icon-master.svg, favicon.svg   aus src/Client/public/ (design-system-008)
  README.md                  aus standards/frontend/styleguide.md abgeleitet
  SKILL.md                   Kurzanleitung für Claude Design (s. Notes)
```

1. **Rendern statt nachbauen.** Ein Node-Skript startet die App (Vite) headless mit
   Playwright, öffnet `/#/styleguide` und schneidet je Sektion das gerenderte HTML heraus.
   Damit das Skript die Sektionen findet, bekommen die betroffenen Sektionen in
   `src/Client/Components/Styleguide/View.fs` ein stabiles Kennzeichen (z. B.
   `data-ds-card="components-money"` und eine Gruppe). Jede Vorschau-Datei bindet
   `../colors_and_type.css` und `../styles.css` ein.
2. **Navigation fehlt heute in der /styleguide-Route** (sie rendert Card, Money, Badge,
   Table, Stats, Button, Icons, Loading, Error, PageHeader, Typografie, Voice sowie Demos
   für BottomSheet, Modal, Toast, Form, Input, Swipe). Eine Navigation-Sektion wird ergänzt,
   mit DS-Komponenten.
3. **Jede Vorschau-Datei beginnt in Zeile 1 mit dem Card-Marker**
   `<!-- @dsCard group="…" -->` (Gruppen z. B. "Colors", "Type", "Components", "Brand").
   Daraus baut Claude Design die Karten im Design-System-Bereich.
4. **Umfang bewusst klein:** Tokens und die Komponenten, die das Kassenbuch braucht (Card,
   Money, Badge, Listenzeile/Table, BottomSheet, Navigation). Weitere Komponenten kommen
   dazu, wenn ein Mockup sie braucht.
5. **Keine echten Daten.** Die /styleguide-Route arbeitet mit Beispieldaten; das Bundle
   enthält nichts aus der Datenbank. Das Skript braucht das Backend nicht.

## Acceptance criteria
- [x] `npm run export:design-system` erzeugt `dist/design-system/` von Grund auf neu (alter
      Inhalt wird ersetzt); zweimal hintereinander ausgeführt ergibt es dasselbe Bundle.
- [x] `colors_and_type.css` enthält alle Farb- und Schrift-Tokens aus dem `@theme`-Block von
      `src/Client/styles.css` mit denselben Werten, einschließlich der Basis-Variablen, auf die
      sie verweisen; keine Variable bleibt unaufgelöst.
- [x] Für Card, Money, Badge, Table, BottomSheet und Navigation liegt je eine
      `preview/components-*.html` vor, deren Markup aus der gerenderten `/#/styleguide`-Route
      stammt (kein von Hand geschriebenes Komponenten-HTML im Skript).
- [x] Die /styleguide-Route hat eine Navigation-Sektion aus DS-Komponenten.
- [x] Jede `preview/*.html` beginnt in Zeile 1 mit `<!-- @dsCard group="…" -->` und sieht,
      als Datei im Browser geöffnet, aus wie die entsprechende Sektion der App
      (Screenshot-Vergleich im Diary festgehalten).
- [x] `README.md` und `SKILL.md` liegen im Bundle; `SKILL.md` nennt visuelle Sprache,
      Farbsemantik, Schriften, Voice und No-Gos aus `standards/frontend/styleguide.md`.
- [x] Das Bundle enthält keine Daten aus der Datenbank; das Skript läuft ohne Backend.
- [x] Keine Inline-Farben in den geänderten Feliz-Views; `dotnet build`, `dotnet test`,
      `npm run build` grün.

## Notes
- **Playwright-Rezept** (aus früheren Sessions): Vite läuft auf Port 5181, Routing ist
  hash-basiert (`/#/styleguide`), der Fable-Erstcompile dauert rund 14 s. Die installierten
  Chromium-Builds passen oft nicht zur `playwright`-Version, dann einmal
  `npx playwright install chromium chromium-headless-shell`. Das Skript muss aus dem
  Projekt-Root laufen (ESM-`import 'playwright'` löst nur dort auf), also z. B.
  `scripts/export-design-system.mjs`. Für präsentationale Routen muss das Backend nicht
  laufen; `/api`-Proxy-Fehler sind dann harmlos.
- **`SKILL.md`-Form** nach dem Smolder-Vorbild: Frontmatter (`name`, `description`,
  `user-invocable: true`), dann "Read the README.md", eine Kurzorientierung (Marke,
  Schriften, wo die Tokens liegen, Signatur-Muster), typische Aufgaben und eine
  "What to avoid"-Liste. Für BudgetBuddy: neon-on-dark, Farbsemantik (Orange = CTA,
  Teal = aktiv, Green = Erfolg, Red = Fehler, Purple/Pink = Akzent), Mono-Font für Beträge,
  kein Inline-Hex, Voice aus Abschnitt 8 des Styleguides.
- Die Smolder-Vorschauen tragen noch keinen `@dsCard`-Marker (älteres Verfahren über
  explizite Registrierung). Laut `DesignSync`-Werkzeug ist der Marker in Zeile 1 heute der
  Weg, das Registrieren entfällt.
- Schriften (Inter, Space Grotesk, JetBrains Mono) müssen in den Vorschauen laden; wie die
  App sie heute einbindet, übernehmen.
- Fonts, Tokens und Komponenten-Markup kommen aus dem Code; Claude Design ist nie Quelle für
  eine Änderung am Repo.
- **ADR 0024** (`.agentheim/knowledge/decisions/0024-export-bundle-build-nicht-dev-und-nicht-fixe-navigation-preview.md`):
  warum das Skript `vite build`+`preview` statt `vite dev` nutzt (stabile, statische CSS-Datei
  zum Kopieren), warum `Navigation.fs` additive `desktopNavPreview`/`mobileNavPreview`
  bekommt statt eines Containment-Tricks auf den echten (fixen) Navigationskomponenten, und
  warum die Preview-Dateien die Marken-Fonts per explizitem `<link>` laden.
- **Iteration 2** (Verifier-Fund): das gebaute `dist/design-system/styles.css` enthielt weder
  `@import` noch `@font-face`; der Google-Fonts-`@import` in `src/Client/styles.css` steht
  hinter dem `@theme`-Block und wird vom Build verworfen (bestehender App-Bug, nicht durch
  diesen Task verursacht). Preview-Dateien laden die Fonts jetzt per explizitem `<link>`
  (URL per Regex aus `src/Client/styles.css` extrahiert, nicht hart kodiert); der App-Bug
  selbst ist als Bug-Backlog-Item erfasst (`design-system-f7k2p`) und wird in
  diesem Task nicht behoben (Umfang: nur der Export).
- **Iteration 2** (Conductor-Entscheidung): `preview/color-neon.html`, `preview/color-surfaces.html`
  (Gruppe "Colors"), `preview/type-fonts.html` (Gruppe "Type") sowie `preview/brand-icon.html`
  (Gruppe "Brand") ergänzt, generiert aus denselben aufgelösten Token-Werten, die auch
  `colors_and_type.css` schreiben (eine Quelle im Skript, kein zweites von Hand gepflegtes
  Set).
- Kein E2E-/Playwright-Testharness im Repo (`RUN_SMOKE_TESTS` aus `.claude/rules/testing.md`
  existiert nicht, bekannt stale). Verifikation manuell: zweifacher Lauf (Idempotenz-Diff,
  byte-identisch), Screenshot-Vergleich Live-Route vs. exportierte `preview/*.html`
  (Card/Money/Badge/Table/Navigation/BottomSheet: alle visuell deckungsgleich), headless
  `document.fonts`-Check. Nachrüstung der Automatisierung als Backlog-Item
  `design-system-w4n9r` angelegt.
- **Iteration 1** (Stilregel): rund 55 Gedankenstriche als Satzzeichen in neu geschriebener
  Prosa entfernt (ADR 0024, README-Abschnitt, Skript-Vorlagen, F#-Kommentare, Task-Datei,
  Backlog-Item). Punkt/Komma/Doppelpunkt/Klammern statt Gedankenstrich, wie vom Verifier
  verlangt.

## Outcome
`npm run export:design-system` (`scripts/export-design-system.mjs`) baut die App
(`vite build` in ein temporäres Verzeichnis), startet sie headless (`vite preview` auf einem
freien Port), rendert `/#/styleguide` mit Playwright und schreibt daraus
`dist/design-system/`: `colors_and_type.css` (vollständig aufgelöster `@theme`-Block),
`styles.css` (gebautes App-CSS), `preview/components-{card,money,badge,table,bottomsheet,
navigation}.html` (jede mit `<!-- @dsCard group="Components" -->` in Zeile 1, aus der
gerenderten Route geschnitten), `assets/{icon-master,favicon}.svg`, `README.md`, `SKILL.md`.
Preview-Server und temporäres Build-Verzeichnis werden immer aufgeräumt (`finally`), auch bei
Fehlern. Zwei Läufe hintereinander erzeugen ein byte-identisches Bundle.

`src/Client/Components/Styleguide/View.fs` bekam `data-ds-card`-Marker auf den Card-/Money-/
Badge-/Table-Sektionen, eine neue Navigation-Sektion ("20. Navigation") sowie einen
`data-ds-card-trigger`-Marker am BottomSheet-Demo-Button (das Sheet portalt nach
`document.body`, wird also gesondert eingesammelt). `src/Client/DesignSystem/Navigation.fs`
bekam zwei additive, nicht-fixe Preview-Funktionen (`desktopNavPreview`/`mobileNavPreview`);
die echte App-Navigation ist unverändert.

Visuelle Prüfung: Screenshots der Live-Route (`[data-ds-card="…"]`-Elemente) gegen
Screenshots der exportierten `preview/*.html`-Dateien (im Browser geöffnet) zeigen
deckungsgleiches Layout, Farben, Typografie für Card, Money, Badge, Table, Navigation und
BottomSheet.

**Iteration 2** (Verifier-Fund plus Conductor-Entscheidung): das Skript extrahiert die
Google-Fonts-URL per Regex aus `src/Client/styles.css` und bindet sie in jeder
Preview-Datei per explizitem `<link>` ein, unabhängig vom App-seitigen Font-Bug
(`@import` steht dort hinter `@theme` und wird vom Build verworfen; als
`design-system-f7k2p` erfasst, in diesem Task nicht behoben). Ein headless-Check
(`document.fonts`) verifiziert nach jedem Export, dass Inter, Space Grotesk und
JetBrains Mono tatsächlich laden. Zusätzlich erzeugt das Skript vier Token-/Brand-Karten,
generiert aus denselben aufgelösten Werten wie `colors_and_type.css`:
`preview/color-neon.html`, `preview/color-surfaces.html` (Gruppe "Colors"),
`preview/type-fonts.html` (Gruppe "Type"), `preview/brand-icon.html` (Gruppe "Brand").

**Key files:**
- `scripts/export-design-system.mjs`: das Export-Skript (inkl. Font-Link, Token-/Brand-Karten,
  Font-Verifikation).
- `src/Client/Components/Styleguide/View.fs`: `data-ds-card`-Marker, Navigation-Sektion.
- `src/Client/DesignSystem/Navigation.fs`: `desktopNavPreview`/`mobileNavPreview`.
- `package.json`: `export:design-system`-Script.
- `.agentheim/contexts/design-system/README.md`: Abschnitt "Export-Bundle für Claude Design".
- `.agentheim/knowledge/decisions/0024-*.md`: ADR zu Build-Modus, Navigation-Preview und Fonts.
- `.agentheim/contexts/design-system/backlog/design-system-w4n9r-*.md`: Nachrüstung E2E-Test.
- `.agentheim/contexts/design-system/backlog/design-system-f7k2p-*.md`: Bug-Item Font-Import.

## Diary entry

## 2026-09-11 12:45 - Export-Bundle des Design-Systems für Claude Design (design-system-t82sg)

**What I did:**
Ein wiederholbares Export-Kommando gebaut, das Tokens und die Kassenbuch-relevanten
DS-Komponenten aus dem echten, gerenderten Code (`/#/styleguide`) als Claude-Design-Bundle
nach `dist/design-system/` (git-ignoriert) schreibt. Dazu eine Navigation-Sektion in der
Styleguide-Galerie ergänzt (fehlte bisher), zwei nicht-fixe Preview-Varianten der
Navigation-Komponente gebaut, sowie (Iteration 2) Token-/Type-/Brand-Vorschaukarten und
expliziten Font-Ladepfad für die Bundle-Vorschauen ergänzt.

**Files Added:**
- `scripts/export-design-system.mjs`: Node-Skript: `vite build` (temp outDir), `vite
  preview` (freier Port), Playwright rendert `/#/styleguide`, schneidet `[data-ds-card]`
  heraus, schreibt `colors_and_type.css`/`styles.css`/`preview/*.html`/`assets/`/
  `README.md`/`SKILL.md`, prüft danach headless, dass die Fonts laden.
- `.agentheim/knowledge/decisions/0024-export-bundle-build-nicht-dev-und-nicht-fixe-navigation-preview.md`
- `.agentheim/contexts/design-system/backlog/design-system-w4n9r-e2e-test-fuer-styleguide-export.md`
- `.agentheim/contexts/design-system/backlog/design-system-f7k2p-google-fonts-import-hinter-theme-block-wird-verworfen.md`
  (Iteration 2: Bug-Backlog-Item für den App-seitigen Font-Import-Fehler)

**Files Modified:**
- `src/Client/Components/Styleguide/View.fs`: `data-ds-card`-Marker auf Card-/Money-/
  Badge-/Table-Sektion, neue Navigation-Sektion ("20. Navigation"), `data-ds-card-trigger`
  am BottomSheet-Demo-Button, Voice-Sektion auf "21." umnummeriert. Iteration 2: Prosa in
  neuen Kommentaren auf Punkt/Doppelpunkt statt Gedankenstrich umgestellt.
- `src/Client/DesignSystem/Navigation.fs`: `desktopNavPreview`/`mobileNavPreview` ergänzt
  (additiv, echte `desktopNav`/`mobileNav` unverändert). Iteration 2: Kommentare
  dashfrei umformuliert.
- `package.json`: `export:design-system`-Script ergänzt.
- `.agentheim/contexts/design-system/README.md`: Abschnitt "Export-Bundle für Claude
  Design" ergänzt (Iteration 2: Fonts, Token-/Brand-Karten, Bug-Verweis ergänzt, alle
  Gedankenstriche entfernt).

**Files Deleted:**
- keine

**Rationale:**
Das Kassenbuch braucht vor weiteren Screens einen UX-Durchgang mit Mockups in Claude Design.
Damit die Mockups wie BudgetBuddy aussehen statt wie eine Nachempfindung, muss Claude Design
echte Tokens/Komponenten bekommen, nicht von Hand nachgebaute. Gerendertes Markup statt
Handarbeit verhindert genau die Drift, die das Styleguide-Gate sonst verhindern soll.
Iteration 2 kam aus der Verifier-Prüfung: eine Stilregel (keine Gedankenstriche in neuer
Prosa), ein echter Bundle-Mangel (Fonts luden nicht) und eine Conductor-Entscheidung
(Token-Karten ergänzen, weil der "What"-Baum sie nennt).

**Outcomes:**
- Build: dotnet build erfolgreich (Client, Server, Shared, Tests, 0 Fehler, 0 Warnungen)
- Tests: dotnet test erfolgreich, 717 bestanden, 7 übersprungen (Comdirect/YNAB-
  Integrationstests, brauchen `.env`, vorbestehend, nicht Teil dieses Tasks), 0 fehlgeschlagen
- npm run build: erfolgreich
- `npm run export:design-system` zweimal hintereinander: byte-identisches Bundle
  (`diff -rq` zwischen beiden Läufen: keine Unterschiede), inklusive der neuen
  Token-/Brand-Karten
- Screenshot-Vergleich Live-Route vs. exportierte `preview/*.html`: Card, Money, Badge,
  Table, Navigation, BottomSheet visuell deckungsgleich (Layout, Farben, Typografie, Glow;
  Screenshots nicht committet)
- Headless `document.fonts`-Check gegen `preview/type-fonts.html`: Inter, Space Grotesk,
  JetBrains Mono geladen (vorher: 0 geladene Fonts, Ersatzschrift)
- Kein automatisierter E2E-Test dafür im Repo (kein Playwright-Harness in `dotnet test`):
  Nachrüstung als Backlog-Item `design-system-w4n9r` angelegt, manuelle Verifikation s. o.
- App-seitiger Font-Bug als Backlog-Item `design-system-f7k2p` erfasst, in diesem Task
  bewusst nicht behoben (Umfang: nur der Export)
- Issues: keine

## Verifier note (iteration 1)

**REASONS:**
- Stilregel dieses Laufs verletzt: rund 55 Gedankenstriche (— bzw. –) als Satzzeichen in neu geschriebener Prosa. Fundstellen: ADR 0024 Zeilen 23, 33, 47, 51, 59, 73; neuer README-Abschnitt (`.agentheim/contexts/design-system/README.md` Zeilen 97, 101, 103, 108, 110, 114, 123); generierter Bundle-Text in `scripts/export-design-system.mjs` (Vorlagen README_MD und SKILL_MD, Zeilen 290, 303, 324, 333, 352, 386, 388, 390, 392; Preview-`<title>` Zeile 231; Kopf von colors_and_type.css Zeile 171); Code-Kommentare in `src/Client/Components/Styleguide/View.fs` (die vier "data-ds-card s. cardSection — Export-Marker"-Kommentare, BottomSheet-Trigger-Kommentar, Doc-Kommentar von navigationSection) und `src/Client/DesignSystem/Navigation.fs` (Doc-Kommentare von `desktopNavPreview` und `mobileNavPreview`); Notes, Outcome und Diary entry der Task-Datei (18 Zeilen); Backlog-Datei `design-system-w4n9r-...md` (2). README.md und SKILL.md des Bundles sind der Text, den Claude Design liest.
- Die Notes verlangen: "Schriften (Inter, Space Grotesk, JetBrains Mono) müssen in den Vorschauen laden." Sie laden nicht. Das gebaute `dist/design-system/styles.css` enthält 0 `@import`- und 0 `@font-face`-Regeln. Der Google-Fonts-`@import url(...)` steht in `src/Client/styles.css` (Zeile 50) hinter `@theme`, der Build verwirft ihn. Headless geöffnet ist `document.fonts` in `preview/components-money.html` leer; `font-mono`-Labels in `preview/components-navigation.html` rendern in einer Ersatzschrift. ADR 0024 Zeile 47 behauptet "Fonts/Farben/Layout stimmen mit der App überein"; das stimmt nur, weil die gebaute App denselben (älteren) Fehler hat. Weder im Bundle behoben noch als Backlog-Item erfasst.
- Nicht blockierend (Entscheidung des Conductors): Der `## What`-Baum nennt Token-Karten (`preview/color-neon.html`, `color-surfaces.html`, `type-*.html`), Punkt 3 die Gruppen "Colors", "Type", "Brand". Sie fehlen, ohne Backlog-Item; das Bundle hat nur "Components"-Karten.
- Übrige Checks bestanden: Build 0 Warnungen, 717 Tests grün, `npm run build` grün; Export zweimal ausgeführt, alte Datei entfernt, `diff -r` byte-identisch; alle sechs `preview/components-*.html` beginnen mit `<!-- @dsCard group="Components" -->` und rendern von `file://`; `colors_and_type.css` mit vollem `@theme` plus allen 10 Basis-Variablen; `desktopNav`/`mobileNav` unverändert; TDD-Verzicht vertretbar; SKILL.md behandelt Voice nur knapp.

**SUGGESTED_FIX:** Alle Gedankenstriche in der vom Worker geschriebenen Prosa durch Punkt, Komma, Doppelpunkt oder Klammern ersetzen (ADR, README-Abschnitt, Vorlagen und generierte Texte im Skript, F#-Kommentare, Task-Datei, Backlog-Item). Die Schriften im Bundle laden (Google-Fonts-URL aus `src/Client/styles.css` übernehmen, nicht hart kodieren: als `<link>` im Kopf jeder Vorschau oder als `@import` am Anfang von `colors_and_type.css`) oder den App-Fehler als Backlog-Item erfassen und die Font-Aussage in ADR 0024 korrigieren.

**ITERATION_HINT:** likely-fixable

**Conductor-Entscheidung zu den offenen Punkten:** (1) Schriften: im Bundle laden (URL aus styles.css) UND den App-Fehler (`@import` hinter `@theme`, im Build verworfen) als Bug-Backlog-Item im design-system erfassen; die App selbst in diesem Task nicht ändern. ADR 0024 entsprechend korrigieren. (2) Token-Karten: ergänzen, weil der `## What`-Baum sie nennt: mindestens `preview/color-neon.html`, `preview/color-surfaces.html` (Gruppe "Colors") und `preview/type-fonts.html` (Gruppe "Type"), erzeugt aus den Tokens in colors_and_type.css, nicht von Hand gepflegt; App-Icon als Karte in Gruppe "Brand", wenn es einfach geht.
