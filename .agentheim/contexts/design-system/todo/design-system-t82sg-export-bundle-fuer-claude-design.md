---
id: design-system-t82sg
title: Wiederholbarer Export des Design-Systems als Claude-Design-Bundle (Tokens + Kassenbuch-Komponenten aus der /styleguide-Route)
status: todo
type: feature
context: design-system
created: 2026-09-11
completed:
depends_on: [design-system-001]
blocks: [design-system-h58y4]
tags: [frontend, design-system, claude-design, export]
related_adrs: []
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
- [ ] `npm run export:design-system` erzeugt `dist/design-system/` von Grund auf neu (alter
      Inhalt wird ersetzt); zweimal hintereinander ausgeführt ergibt es dasselbe Bundle.
- [ ] `colors_and_type.css` enthält alle Farb- und Schrift-Tokens aus dem `@theme`-Block von
      `src/Client/styles.css` mit denselben Werten, einschließlich der Basis-Variablen, auf die
      sie verweisen; keine Variable bleibt unaufgelöst.
- [ ] Für Card, Money, Badge, Table, BottomSheet und Navigation liegt je eine
      `preview/components-*.html` vor, deren Markup aus der gerenderten `/#/styleguide`-Route
      stammt (kein von Hand geschriebenes Komponenten-HTML im Skript).
- [ ] Die /styleguide-Route hat eine Navigation-Sektion aus DS-Komponenten.
- [ ] Jede `preview/*.html` beginnt in Zeile 1 mit `<!-- @dsCard group="…" -->` und sieht,
      als Datei im Browser geöffnet, aus wie die entsprechende Sektion der App
      (Screenshot-Vergleich im Diary festgehalten).
- [ ] `README.md` und `SKILL.md` liegen im Bundle; `SKILL.md` nennt visuelle Sprache,
      Farbsemantik, Schriften, Voice und No-Gos aus `standards/frontend/styleguide.md`.
- [ ] Das Bundle enthält keine Daten aus der Datenbank; das Skript läuft ohne Backend.
- [ ] Keine Inline-Farben in den geänderten Feliz-Views; `dotnet build`, `dotnet test`,
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
