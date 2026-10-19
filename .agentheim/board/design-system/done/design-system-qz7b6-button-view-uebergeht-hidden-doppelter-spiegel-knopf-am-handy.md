---
id: design-system-qz7b6
title: Button.view übergeht hidden aus ClassName, im Kassenbuch erscheinen am Handy beide Spiegel-Knöpfe
status: done
type: bug
context: design-system
created: 2026-10-19
completed:
depends_on: []
blocks: []
tags: [frontend, bug, kassenbuch, visuell, ruhige-listen]
related_adrs: [0049, 0031]
related_research: []
prior_art: [design-system-q7rkm, ledger-n4vte, design-system-fq8dn]
---

## Why
Seit `ledger-n4vte` zeigt das Kassenbuch im Seitenkopf zwei Spiegel-Knöpfe: am Handy
(390 × 844) stehen der Symbol-Knopf und "Jetzt spiegeln" nebeneinander, obwohl am Handy
nur das Symbol und am PC nur der Textknopf erscheinen soll. Gefunden in der
Bestandsaufnahme gegen ADR 0049 am 2026-10-19 (Screenshot
`/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/ce62b2b7-5777-4228-8b31-afa8d3e4749a/scratchpad/audit/shots/handy-390x844-ledger.png`).
Weder der Worker noch zwei Verifier haben es gesehen, weil kein Test die Sichtbarkeit
prüft und der Handy-Screenshot als Ganzseitenaufnahme den Kopf klein zeigte.

## What
Ursache: `Button.view` (`src/Client/DesignSystem/Button.fs`, `view`) setzt fest
`inline-flex` an den Anfang der Klassenliste und hängt `ClassName` hinten an. Der
Textknopf in `src/Client/Components/Ledger/View.fs` (um Zeile 1301) übergibt
`ClassName = Some "hidden md:inline-flex"`. In Tailwind 4 entscheidet die Reihenfolge im
erzeugten CSS, nicht die im `class`-Attribut; `inline-flex` gewinnt gegen `hidden`. Der
Symbol-Knopf mit `md:hidden` funktioniert, weil `md:hidden` als Media-Regel später kommt.

Den Fehler im Design-System beheben, sodass ein Aufrufer einen Button per `ClassName`
(oder einer eigenen, ausdrücklichen Eigenschaft) responsiv ausblenden kann. Wie, ist dem
Worker überlassen: z. B. eine Sichtbarkeits-Eigenschaft an `ButtonProps`, oder `view`
lässt `inline-flex` weg, wenn der Aufrufer eine eigene Anzeige-Klasse mitgibt, oder ein
Wrapper. Danach das Kassenbuch so anpassen, dass es den Weg nutzt. Andere Aufrufer mit
`hidden` oder `md:hidden` in `ClassName` per `grep` prüfen und mit erfassen.

## Acceptance criteria
- [ ] Regressionstest im Browser (Muster `scripts/ledger-status-column.test.mjs`, läuft
      über `npm run test:scripts`): auf `#/ledger` ist bei 390 × 844 genau ein
      Spiegel-Knopf sichtbar (der Symbol-Knopf mit zugänglichem Namen "Jetzt spiegeln"
      oder gleichwertig), bei 1440 × 900 genau einer (der Textknopf). Der Test ist vor
      dem Fix rot und danach grün; der Testkommentar nennt die Ursache.
- [ ] Ein .NET-Test sichert die Klassenlogik von `Button.view` ab (z. B. eine reine
      Klassenfunktion): mit einer Anzeige-Klasse vom Aufrufer enthält das Ergebnis kein
      konkurrierendes `inline-flex` ohne Breakpoint; ohne sie bleibt `inline-flex` wie bisher.
- [ ] Alle anderen Stellen, die einen `Button` per `ClassName` responsiv aus- oder
      einblenden, sind geprüft und funktionieren; die Liste steht im Outcome.
- [ ] Bestehende Buttons sehen unverändert aus (keine Änderung an Varianten, Größen,
      Abständen).
- [ ] Neue Screenshots `#/ledger` in 390 × 844 und 1440 × 900, **nur sichtbarer Bereich**
      (keine Ganzseitenaufnahme), unter `docs/ux/kassenbuch/screenshots/`
      (`handy-390x844-ruhige-listen.png`, `pc-1440x900-ruhige-listen.png` ersetzen).
- [ ] `dotnet build` (keine neue Warnung), `dotnet test`, `npm run build` und
      `npm run test:scripts` sind grün.

## Notes
- Arbeitsbranch ist `feature/kassenbuch`; der Branch `main` ist veraltet.
- Bug-Protokoll (CLAUDE.md): Test zuerst rot, dann Fix.
- Tailwind-Klassen nur als wörtliche Zeichenketten (Klassen-Wächter in `dotnet test`).

## Outcome

Ursache bestätigt: `Button.view` setzte fest `inline-flex` an den Anfang der Klassenliste, der Textknopf im Kassenbuch übergab `hidden md:inline-flex`. Tailwind 4 ordnet Utilities im erzeugten CSS, nicht nach dem class-Attribut, daher gewann `inline-flex` gegen `hidden`.

Fix (`src/Client/DesignSystem/Button.fs`): neue öffentliche reine Funktion `callerSetsDisplay` prüft, ob `ClassName` eine Anzeige-Klasse ohne Breakpoint enthält (`hidden`, `block`, `inline-block`, `inline`, `flex`, `inline-flex`, `grid`, `inline-grid`). In `view` entfällt dann das feste `inline-flex`, sonst bleibt es wie bisher. Keine neue Eigenschaft an `ButtonProps`, daher kein README_DELTA. Der Klassen-Wächter verlangt wörtliche Zweige in der Bindung, deshalb steht `if callerSetsDisplay extraClass then "" else "inline-flex"` direkt in `view`. Der Aufrufer im Kassenbuch (`src/Client/Components/Ledger/View.fs`) bleibt unverändert, er nutzt jetzt den reparierten Weg.

Tests:
- `scripts/ledger-mirror-buttons.test.mjs` (läuft in `npm run test:scripts`): bei 390 x 844 genau ein sichtbarer Spiegel-Knopf (Symbol, Titel "Jetzt spiegeln"), bei 1440 x 900 genau einer (Text, "Jetzt spiegeln" oder "Spiegel läuft…", je nachdem ob der gemockte Lauf schon läuft). Vor dem Fix war der Handy-Test rot (zwei sichtbare Knöpfe), danach grün; der Kommentar nennt die Ursache.
- `src/Tests/RuhigeListenTests.fs`: drei Tests für `Button.callerSetsDisplay` (ohne Anzeige-Klasse bleibt inline-flex, `hidden md:inline-flex` und `block`/`flex` verdrängen es).

Andere Aufrufer: `grep` nach `ClassName = Some` mit hidden/flex/block in `src/Client` findet nur die zwei Kassenbuch-Knöpfe (`md:hidden` am Symbol-Knopf, `hidden md:inline-flex` am Textknopf). Der Symbol-Knopf bleibt bei `inline-flex` (nur Breakpoint-Klasse), alle anderen Buttons haben keine Anzeige-Klasse in ClassName und sehen unverändert aus.

Screenshots `docs/ux/kassenbuch/screenshots/handy-390x844-ruhige-listen.png` und `pc-1440x900-ruhige-listen.png` ersetzt, nur sichtbarer Bereich (`fullPage: false`), gerendert gegen die gemockten Ledger-Antworten aus `scripts/ledger-filter-bar.mjs` (erfundene Werte, keine Datenbank). Handy zeigt nur das Symbol, PC nur "Jetzt spiegeln". Gesehen und geprüft.

Prüfläufe: `dotnet build` ohne neue Warnung (nur das bekannte FS0025 in `Ledger/State.fs:333`), `dotnet test` 1200 grün (7 übersprungen), `npm run build` ok, `npm run test:scripts` 120 von 120 grün.

Tagebuch (für `diary/development.md`): Button.view verdrängt `inline-flex`, wenn der Aufrufer in ClassName eine Anzeige-Klasse ohne Breakpoint setzt (`callerSetsDisplay`). Behebt die doppelten Spiegel-Knöpfe im Kassenbuch am Handy. Neu: `scripts/ledger-mirror-buttons.test.mjs`, drei Expecto-Tests in `RuhigeListenTests.fs`; geändert: `Button.fs`, zwei Screenshots. Build und alle Tests grün.
