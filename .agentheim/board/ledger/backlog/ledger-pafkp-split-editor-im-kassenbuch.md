---
id: ledger-pafkp
title: "Split-Editor im Kassenbuch: Pfeil an der Kategorie einer Split-Buchung öffnet das Formular-Sheet zum Aufteilen, Ändern und Aufheben der Zeilen mit Rest-Anzeige und Speichern über setEntrySplits"
status: backlog
type: feature
context: ledger
created: 2026-10-24
completed:
depends_on: [design-system-001, ledger-sfab0, ledger-kxme4, design-system-gffps]
blocks: []
tags: [frontend, kassenbuch, split, v2]
related_adrs: [0005, 0006, 0049, 0053, 0055]
related_research: []
prior_art: [ledger-q1hw2]
---

## Why
Die Oberfläche zu `ledger-kxme4`. Referenz ist der Split-Editor des Import-Ablaufs
(`src/Client/Components/SyncFlow/Views/SplitSheet.fs`, ynab-sync); er bleibt unberührt. Der
Handoff Ablauf 7 (`docs/ux/kassenbuch/handoff-ablauf-7-was-nur-bb-kann.md`) behandelt das
Detail lesend und enthält keinen Split-Editor; die Gestaltung ist hier offen.

## What
Wird nach `ledger-kxme4` verfeinert; der Rahmen steht:
1. Der Pfeil an der Kategorie-Zeile im Detail öffnet bei einer Split-Buchung (und optional
   über einen Knopf "Aufteilen" bei einer unaufgeteilten) ein Formular-Sheet mit Speichern
   und Abbrechen. Zeilen mit Kategorie oder Übertrag-Ziel (Picker aus `design-system-gffps` als
   zweite Ebene, ADR 0005), Betrag je Zeile, Rest-Anzeige, Speichern gesperrt bei Rest
   ungleich null (Farbregel wie `splitAmountClass`, ADR 0049).
2. Speichern ruft `setEntrySplits` und nutzt Toast, Fehlschlag-Leiste und `OrderStale` aus
   `ledger-sfab0`.
3. Die Rechen- und Prüffunktionen kommen aus `Shared.Domain` (ADR 0006), nicht aus
   `SyncFlow`.

## Acceptance criteria
- [ ] To be defined after `ledger-kxme4`.

## Notes
Entworfen am 2026-10-24 (Sammel-Modeling) als Scheibe (e, Oberfläche). Ziel-Lage: backlog.
Offen: Gestaltung des Editors am Handy und am PC (Sheet oder Panel-Ersatz). Vorschlag: ein
Entwurf im Claude-Design-Projekt vor der Verfeinerung. Dateien: Ledger-Client
`Types.fs`, `State.fs`, `View.fs`, eine neue Ansichtsdatei für den Editor.
Überschneidung mit `ledger-sfab0` und `-3` in den drei Ledger-Client-Dateien.
