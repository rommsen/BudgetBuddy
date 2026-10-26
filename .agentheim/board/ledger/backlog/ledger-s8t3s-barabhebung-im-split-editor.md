---
id: ledger-s8t3s
title: "Barabhebung im Split-Editor des Kassenbuchs: ein Knopf belegt die erste leere Zeile mit einem Übertrag aufs Bar-Konto (oder hängt eine an), Ausgleich wie im Import über Rest"
status: backlog
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
