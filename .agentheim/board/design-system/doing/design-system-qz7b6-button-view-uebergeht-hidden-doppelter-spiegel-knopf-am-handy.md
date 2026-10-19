---
id: design-system-qz7b6
title: Button.view übergeht hidden aus ClassName, im Kassenbuch erscheinen am Handy beide Spiegel-Knöpfe
status: doing
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
