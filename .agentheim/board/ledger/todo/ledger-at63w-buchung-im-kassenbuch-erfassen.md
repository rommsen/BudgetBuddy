---
id: ledger-at63w
title: "Buchung im Kassenbuch erfassen für jedes Konto ohne Import, mit Vorbelegung (Konto, Datum, Zuletzt, Kategorie zum Payee)"
status: todo
type: feature
context: ledger
created: 2026-11-08
completed:
depends_on: [ledger-p3vq8, ledger-qvjvs]
blocks: []
tags: [etappe-2, erfassen, quick-add, ynab]
related_adrs: [0004, 0021, 0066, 0087]
related_research: []
prior_art: [ledger-tdnpk, ledger-xa7jf, ledger-b0b7q]
---

## Why
Fahrplan Etappe 2: "Buchung erfassen im Kassenbuch für jedes Konto, wenige Klicks, kluge Vorbelegung (das
Quick-Add-Versprechen für alle Konten; ING-Tagesgeld)". Heute erfasst BB nur Barbuchungen auf dem Quick-Add-Konto;
für Tagesgeld ING oder Kreditkarte muss Roman nach YNAB. Vorlage: Prototyp Version 17, Sheet "Neue Buchung".

## What
Entschieden (Roman, 2026-11-08):
- **Konten:** jedes offene Budget-Konto ohne Comdirect-Import. Das Import-Konto (`ynab_default_account_id`) ist
  nicht wählbar, weil ein Abgleich zwischen einer von Hand erfassten Buchung und dem späteren Import noch fehlt
  ("Später"; die Duplikat-Erkennung kennt nur die Comdirect-Referenz).
- **Ort:** Knopf "+ Buchung" im Kassenbuch, am Handy schwebend unten rechts mit Sheet, am PC im Panel rechts
  (wie das Detail). Die Quick-Add-Seite bleibt unverändert bis Etappe 3.
- **Felder** wie im Prototyp, von oben: Ausgabe/Einnahme (`Segmented`), Betrag groß und ohne Vorzeichen
  (`inputmode="decimal"`, die Richtung kommt aus Ausgabe/Einnahme), Zuletzt-Chips, Konto (Auswahlfeld, §7.5),
  Kategorie oder Konto (`CategoryPicker` mit `Accounts`, wie ledger-qvjvs), Payee, Datum, Memo. Hauptaktion
  "Buchung anlegen".
- **Vorbelegung:** Konto aus dem Kassenbuch-Filter, sonst das zuletzt beim Erfassen genutzte; Datum heute mit einem
  Tipp auf gestern; Zuletzt-Chips mit den letzten drei erfassten Kombinationen aus Payee, Kategorie, Betrag und
  Konto (ein Tipp füllt alles); beim Payee die Kategorie, die dieser Payee zuletzt hatte, sonst die passende Regel.
- **Schreibweg:** derselbe Befehl wie Quick Add und Bob (`addCashBooking` mit dem gewählten Konto, Kassenbuch zuerst,
  dann Push, ADR 0021), mit fester `import_id` aus ledger-p3vq8. Actor `UserInBb` (Herkunft "Roman in BB"). Ein Konto
  als Ziel bucht einen Übertrag (Push mit `transfer_payee_id` wie in ledger-qvjvs), YNAB legt die Gegenbuchung an.
- **Rückmeldung:** Toast "Buchung angelegt · <Konto>" mit "Rückgängig"; am PC öffnet danach das Detail der neuen
  Buchung im Panel. Ein gescheiterter Push zeigt die Buchung mit "Noch nicht in YNAB" und "Erneut senden".

Nicht Teil: Erfassen auf dem Import-Konto und der Abgleich mit dem Comdirect-Import; "+ Buchung" auf der Übersicht
(Etappe 3); Auflösen der Quick-Add-Seite (Etappe 3).

## Acceptance criteria
- [ ] Handler-Test: Erfassen auf einem Konto ohne Import schreibt `CashExpenseAdded` mit Actor `UserInBb` und dem
      gewählten Konto und pusht mit der festen `import_id`; Ausgabe negativ, Einnahme positiv im Kassenbuch.
- [ ] Handler-Test: Ablehnung ohne Ereignis und ohne Push für das Import-Konto, ein geschlossenes oder unbekanntes
      Konto, Betrag 0 oder leer, und alles, was Quick Add heute ablehnt (dieselben Regeln, Doktrin Punkt 4), je mit
      konkreter Meldung aus `src/Server/Validation.fs`.
- [ ] Handler-Test: Ziel ist ein Konto: der Push trägt `payee_id` = `transfer_payee_id` und keine `category_id`;
      Ziel ist das eigene Konto: Ablehnung.
- [ ] Reine Funktion oder Abfrage-Test: Vorbelegung des Kontos (Filter, sonst zuletzt genutzt, nie das
      Import-Konto), Datum heute im Berliner Kalender, Zuletzt-Chips (drei, neueste zuerst, ohne Doppelte), Kategorie
      zum Payee (letzte Kategorie dieses Payees, sonst Regel, sonst leer).
- [ ] Client-`update` unter .NET: ein Zuletzt-Chip füllt Payee, Kategorie, Betrag und Konto; "Buchung anlegen" ist
      gesperrt, solange der Aufruf läuft (kein Doppel-Tipp mit zwei Buchungen); Fehler bleiben im Formular sichtbar.
- [ ] Eine Zeile in `src/Tests/RemotingEndpointNegativeTests.fs` und ein Aufruf in `src/Tests/RemotingPipelineTests.fs`,
      falls ein neuer Endpoint oder ein neues Interface entsteht (Doktrin Punkt 10 und 11).
- [ ] Ein Zustand im Layout-Sweep (`scripts/layout-states.mjs`) für das offene Erfassen am Handy und am PC, auch mit
      offener Tastatur; ein `[flow:buchung-erfassen]`-Test mit Eintrag in `FLOWS`.
- [ ] Eingabe bei 412 px mit Touch geprüft: Betrag ohne Minus-Taste eingebbar, die Tastatur bleibt beim Wechsel
      zwischen Betrag, Payee und Memo offen (CLAUDE.md, Skizzen). Screenshot im Outcome.
- [ ] Seite neben dem Prototyp Version 17 verglichen (Sheet "Neue Buchung" am Handy, Panel am PC), Abweichungen nur
      die genannten; Screenshots im Outcome. [human-eye]

## Notes
- Prototyp: https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17), Kassenbuch, "+ Buchung". Der Conductor
  legt Screenshots in den Scratchpad und gibt die Pfade an Worker und Verifier.
- Abweichungen vom Prototyp: Das Konto-Feld bietet das Import-Konto nicht an (Roman, Grund oben). Im Prototyp ist
  das Datum ein Textfeld; in BB ein Datumsfeld mit "Heute" und "Gestern", weil Freitext für ein Datum Drift ist
  (Styleguide §7.5).
- Quick Add hat Vorlagen (ynab-t4n8p) und die Spike-Liste aus ledger-xa7jf; was davon die Zuletzt-Chips abdeckt,
  prüft der Worker und nennt es im Outcome. Die Quick-Add-Seite selbst bleibt unverändert.
- Stellen: `addCashBooking`/`CashBooking` (`src/Server/Ledger/CommandHandlers.fs`), `resolveQuickAddAccount`,
  `ynab_default_account_id` (`src/Server/Ledger/QueryHandlers.fs:434`), `transferTargetsFor`
  (`src/Client/Components/Ledger/View.fs`).
- Fable.Remoting: ein neues Pflichtfeld in einem Record aus `src/Shared/` zieht die Fixtures in `scripts/*.test.mjs`
  im selben Schritt mit.
