---
id: ledger-s8t3s
title: "Barabhebung im Split-Editor des Kassenbuchs: ein Knopf belegt die erste leere Zeile mit einem Übertrag aufs Bar-Konto (oder hängt eine an), Ausgleich wie im Import über Rest"
status: done
type: feature
context: ledger
created: 2026-10-26
completed:
depends_on: [design-system-001]
blocks: []
tags: [kassenbuch, split, barabhebung, komfort]
related_adrs: [0004, 0006]
related_research: []
prior_art: [ledger-xa7jf, ledger-pafkp, ledger-kxme4]
---

## Why
Eine EC-Zahlung mit Bargeld-Anteil (Einkauf plus Cashback an der Kasse) kommt bei Roman
häufiger vor (Roman, 2026-10-26). Im Kassenbuch muss er sie heute im allgemeinen
Split-Editor Zeile für Zeile aufteilen. Import prüfen hat dafür den Chip "Barabhebung"
(Spike ledger-xa7jf, Lücke 2).

## What
Entscheidung Roman, 2026-10-26: **Variante (a)**, ein Knopf "Barabhebung" im Split-Editor
des Kassenbuchs (`src/Client/Components/Ledger/SplitEditor.fs`, ledger-pafkp) als
Startvorlage. Kein eigener Knopf im Buchungs-Detail.

**Regel (Roman, 2026-10-26):** "Barabhebung" setzt die erste Zeile ohne Ziel auf einen
Übertrag zum Bar-Konto; gibt es keine Zeile ohne Ziel, hängt der Knopf eine neue
Übertrag-Zeile an. Bestehende Zeilen und Beträge bleiben unangetastet. Der Editor öffnet
schon heute mit der bisherigen Kategorie und einer leeren Zeile (`SplitEditor.startState`),
frisch geöffnet ergibt das also Kategorie plus Übertrag. Bar-Konto ist das Quick-Add-Konto
(ADR 0004, Übertrag im Split nach ADR 0006); ist keins eingestellt, kommt die Zeile ohne
Ziel und Roman wählt das Konto selbst.

Die Beträge bleiben leer wie im Import: Roman trägt den Bargeld-Betrag ein und gleicht mit
"Rest" aus (`SplitEditor.fillRest`). Kein automatischer Ausgleich.

Vorbild Import: Chip "Barabhebung" in `src/Client/Components/SyncFlow/Views/TransactionRow.fs`,
Nachricht `StartCashbackSplit` in `src/Client/Components/SyncFlow/State.fs`. Dort bleiben
beide Beträge leer und der Nutzer gleicht mit "Rest" aus.

## Acceptance criteria
- [ ] Der Split-Editor im Kassenbuch zeigt einen Knopf "Barabhebung", am Handy und am PC.
- [ ] Frisch geöffnet ergibt der Knopf zwei Zeilen: die bisherige Kategorie der Buchung und einen Übertrag auf das Quick-Add-Konto.
- [ ] Gibt es eine Zeile ohne Ziel, wird genau diese zum Übertrag; gibt es keine, kommt eine neue Übertrag-Zeile dazu. Andere Zeilen, Ziele und Beträge ändern sich nicht (reiner Test auf der Editor-Logik).
- [ ] Die Beträge bleiben leer; der Ausgleich geht wie bisher über "Rest".
- [ ] Ohne Quick-Add-Konto bleibt das Ziel der Übertrag-Zeile leer und ist wählbar.
- [ ] Gespeichert wird über den bestehenden Split-Schreibweg (ledger-kxme4), ohne neuen Befehl.
- [ ] Playwright am echten Editor: Knopf drücken, Übertrag-Zeile erscheint mit dem Bar-Konto als Ziel.

## Notes
Offene Fragen geklärt am 2026-10-26 (Roman): kein automatischer Ausgleich, Beträge wie im
Import über "Rest"; der Knopf ersetzt nichts, er belegt die erste leere Zeile oder hängt eine
an.

Herkunft: Spike ledger-xa7jf, Outcome-Tabelle Lücke 2.

## Outcome

Der Split-Editor des Kassenbuchs zeigt neben "Zeile hinzufügen" den Knopf "Barabhebung" (am PC im Panel, am Handy im Sheet). Die Logik liegt als reine Funktionen in `Components.Ledger.SplitEditor`: `startCashback` setzt die erste Zeile ohne Ziel auf den Übertrag oder hängt eine Zeile an (auch bei Rest 0,00 €), ändert sonst nichts und lässt die Beträge leer; `cashbackTarget` löst das Quick-Add-Konto (ADR 0004) gegen die wählbaren Übertrag-Ziele auf. Ohne Ziel (kein Quick-Add-Konto, oder es ist nicht wählbar, z. B. das eigene Konto der Buchung) bleibt die Zeile leer und die Konto-Auswahl dieser Zeile geht auf. Das Quick-Add-Konto lädt der Ledger neu aus `getSettings` (`LoadQuickAddAccount` beim Öffnen des Editors, `Model.QuickAddAccountId`); ein Ladefehler lässt das Ziel leer. Die Ansicht (`splitEditorBody`) löst das Ziel aus Modell und `transferTargetsFor` auf und schickt `StartSplitCashback`. Der Knopf sitzt in der neuen DS-Funktion `SplitEditor.lineActions` (Design System), `addLineButton` bleibt für den Styleguide. Gespeichert wird über den bestehenden `setEntrySplits`, kein neuer Befehl.

Dateien: `src/Client/Components/Ledger/SplitEditor.fs`, `Types.fs`, `State.fs`, `View.fs`, `src/Client/DesignSystem/SplitEditor.fs`, Tests in `src/Tests/LedgerSplitEditorClientTests.fs` (11 neue Expecto-Tests), Wächter `scripts/ledger-split-cashback.test.mjs` (4 Playwright-Tests am echten Client: Kategorie plus Übertrag mit Bar-Konto als Ziel, leere Beträge, Ausgleich über Rest und Speichern mit -31,90 € / -50,00 € (-31900 / -50000 Milliunits), zweiter Klick hängt Zeile an, ohne Quick-Add-Konto leeres Ziel und offene Konto-Auswahl, Handy 390 px).

Läufe aus dem Worktree: `dotnet build` 0 Warnungen, 0 Fehler; `dotnet test` 2402 bestanden, 7 übersprungen, 0 Fehler; `npm run build` ok; `npm run guard:classes` ok; Playwright-Wächter seriell (`node --test --test-concurrency=1 scripts/*.test.mjs`) 261 von 261 bestanden. Der parallele Aufruf `npm run test:scripts` hängt auf dieser Maschine (bekannt), die serielle Variante ist grün.

Hinweis Touch-Fläche: "Barabhebung" hat dieselbe Höhe (Small, 36 px) wie der Nachbarknopf "Zeile hinzufügen"; der Wächter prüft die Gleichheit, keine 44 px, weil der bestehende Nachbarknopf sie auch nicht hat.
