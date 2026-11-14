---
id: ledger-f8d7x
title: "Kassenbuch ordnen: am Handy Filter-Knopf mit Sheet und Chips statt zwei wischender Leisten, am PC Seitenleiste mit Konten, Seitenpanel und Historie wie im Budget"
status: backlog
type: feature
context: ledger
created: 2026-11-11
completed:
depends_on: [design-system-001-styleguide, ledger-ct3cx]
blocks: []
tags: [etappe-3, frontend, kassenbuch]
related_adrs: [0086,0087]
related_research: []
prior_art: []
---

## Why
Das Kassenbuch ist am Handy mit zwei wischenden Leisten überladen, die Kontowahl uneinheitlich, das Seitenpanel ungeordnet (ADR 0086, „ungeführt“).

## What
Handy: Kontowahl als Auswahlfeld mit Saldo, Suche, Filter-Knopf mit Sheet, gesetzte Filter als Chips (Styleguide §7.5). PC: Seitenpanel geordnet, Historie in der Form von §7.11, Mehrfachauswahl mit Zusammenfassung im Panel (§7.7), Rückmeldung über der Auswahl-Leiste (§7.13).

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Schnitt aus Etappe 3 (Fahrplan, Roman 2026-11-11). Groß: beim Verfeinern nach Handy und PC teilen (Roman, 2026-11-11). Nimmt auf: ledger-ayjzq (Sicht „Für mich“). Die Seitenleiste mit Konten kommt mit design-system-k6kfw; bleibt dort eine zweite Kontowahl im Kassenbuch stehen, wird sie hier entfernt.
Vorlage: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17). Baut der Schnitt eine Seite um, fliegen ihre `[flow]`-, `[tastatur]`-, `[fokus]`- und `[scroll]`-Tests raus und werden gegen die Muster aus dem Styleguide neu geschrieben, je Flow einer.

Nach ledger-ct3cx (Kassenbuch behält Stand) bauen: beide ändern die Filter-Chips (`Remove*Filter`, `ClearAllFilters`) und die Historie im Detail (Refinement ledger-ct3cx, 2026-11-12).

## Abgleich mit Prototyp Version 17 (2026-11-14)
Screenshots: Prototyp und BB heute (412 und 1440 px) im Scratchpad der Session, beim Verfeinern neu aufnehmen.
BB kann heute mehr als der Prototyp: Kategorie-Filter mit mehreren Kategorien, Zeitraum als freier Bereich, Sortieren
(Datum, Betrag, Payee, Kategorie, Konto), „Gelöschte zeigen“, Trefferzeile „n Buchungen, Summe“. Das bleibt (Regel
„gilt BB“), am Handy im Filter-Sheet.

Entschieden (Roman, 2026-11-14):
- Die freie Suche (Payee, Memo, Kategorie) ersetzt die Filter Payee und Memo. „Alle zeigen“ am Payee im Detail setzt
  dann die Suche.
- Kopf am PC wie im Prototyp: Konto und Saldo, „Abrufen“, „n durchgehen“, „+ Buchung“; „Jetzt spiegeln“ bleibt daneben.
- Prüfen je Zeile wie im Prototyp: kein Knopf „Geprüft“ an der Zeile und keine Spalte Status; gelbe Kante und „zu
  prüfen“, Taste G, am Handy Wischen.
- Spalte Memo am PC wie im Prototyp erst ab 1500 px.
- Teilen in eine Task Handy und eine Task PC. ledger-ayjzq (Sicht „Für mich“) bleibt eigene Task, braucht ein Gespräch.
