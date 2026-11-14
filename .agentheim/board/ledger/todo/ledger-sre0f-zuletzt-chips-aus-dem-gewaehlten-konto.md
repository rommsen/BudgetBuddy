---
id: ledger-sre0f
title: "Zuletzt-Chips in „Neue Buchung“ aus den Buchungen des gewählten Kontos, fünf mit Memo"
status: todo
type: bug
context: ledger
created: 2026-11-14
completed:
depends_on: []
blocks: []
tags: [captured, erfassen, frontend]
related_adrs: [0083]
related_research: []
prior_art: [ledger-ea70s, ledger-at63w]
---

## Why
Romans Blick nach dem Deploy von ledger-ea70s (2026-11-14): Erfassen ohne Quick Add fühlt sich langsamer an, weil
die Zuletzt-Chips die üblichen Bar-Buchungen nicht zeigen (etwa „Anastasia“). Das [human-eye]-Kriterium von
ledger-ea70s („mindestens so schnell wie vorher“) ist damit nicht erfüllt. Quick Add konnte das, „Neue Buchung“
kann es schlechter (Fehler der Verfeinerung von ledger-ea70s: verglichen wurden nur Anzahl und Konto, nicht die
Herkunft der Vorlagen).

## What
Heute: höchstens 3 Chips, nur aus in BB erfassten Buchungen (`CashExpenseAdded` mit Actor `UserInBb` oder
`QuickAdd`, `src/Server/Ledger/Persistence.fs:2050`), über alle Konten gemischt, ohne Memo
(`Shared.Domain.recentBookingTemplates`, `LedgerBookingRecentCount = 3`). Buchungen, die Lena, Bob oder YNAB
anlegen, fehlen. Früher Quick Add: 5 Vorlagen aus YNAB, aus den Buchungen des Quick-Add-Kontos, mit Memo.

Entschieden (Roman, 2026-11-14):
- Die Chips sind die letzten Buchungen des vorbelegten bzw. gewählten Kontos aus dem Kassenbuch, gleich wer sie
  angelegt hat (BB, Bob, Lena, YNAB-Spiegel).
- Wechselt das Konto im Formular, wechseln die Chips.
- 5 Chips.
- Ein Tipp füllt auch das Memo.

## Acceptance criteria
Server und Shared:
- [ ] `getLedgerBookingDefaults` liefert in `Recent` je offenem Kassenbuch-Konto (ohne Import-Konto) bis zu fünf
      Vorlagen aus dessen letzten Buchungen im Kassenbuch, neueste zuerst (Buchungsdatum absteigend, bei gleichem Tag
      die zuletzt verbuchte zuerst), gleich wer sie angelegt hat: in BB erfasst, Bob, YNAB-Spiegel (also auch Lenas
      Buchungen in YNAB). Das Feld `Recent` bleibt eine flache Liste, jede Vorlage trägt ihr `AccountId`.
- [ ] Gelesen wird mit einer Abfrage für alle Konten (etwa `ROW_NUMBER() OVER (PARTITION BY account_id …)` mit einem
      Rückblick von 50 Buchungen je Konto), keine Abfrage je Konto (`.claude/rules/query-safety.md`, kein N+1).
- [ ] Keine Vorlage geben: in YNAB gelöschte Buchungen, aufgeteilte Buchungen, Buchungen ohne Payee, Betrag 0
      (wie heute `bookingTemplateOf` und `recentBookingTemplates`). Überträge bleiben Vorlagen wie heute
      (`BookAsTransfer`, Payee vor dem Übertrag): BB kann das schon, Quick Add konnte es nicht (Regel aus CLAUDE.md
      „gilt BB“). Buchungen ohne Kategorie bleiben Vorlagen ohne Ziel, wie heute.
- [ ] Doppelte fallen weg wie heute (Konto, Payee ohne Rand und Groß-/Kleinschreibung, Ziel, Betrag, Richtung); das
      Memo gehört nicht zum Schlüssel, es gilt das Memo der neuesten Buchung der Kombination.
- [ ] `LedgerBookingTemplate` trägt `Memo: string option` (leer oder nur Leerzeichen wird `None`).
      `LedgerBookingRecentCount` ist 5, Kommentar an `LedgerBookingDefaults.Recent` nachgezogen.
- [ ] `LastUsedAccountId` bleibt wie heute aus den in BB erfassten Buchungen.
- [ ] Tests auf Handler- bzw. Abfrage-Ebene mit In-Memory-SQLite (Doktrin Punkt 1 und 3): eine Bob-Buchung und eine
      gespiegelte Buchung erscheinen als Vorlage ihres Kontos (kaputt-Szenario, heute fehlen sie); zwei Konten mit je
      sieben Buchungen ergeben je fünf, neueste zuerst; gelöschte und aufgeteilte erscheinen nicht; das Import-Konto
      liefert keine. Reine Tests für Doppelte mit verschiedenem Memo (das neueste gilt) und für die Fünfer-Grenze.

Client:
- [ ] „Neue Buchung“ zeigt unter „Zuletzt“ nur die Vorlagen des gewählten Kontos, in der Reihenfolge vom Server,
      höchstens fünf. Ohne gewähltes Konto keine Chips. Die Auswahl ist eine reine Funktion (etwa
      `Capture.templatesFor accountId defaults`), die View ruft sie nur auf; Test unter .NET.
- [ ] Wechselt das Konto im Formular (Konto-Auswahl), zeigt „Zuletzt“ sofort die Vorlagen des neuen Kontos, ohne
      neue Anfrage. Test unter .NET über `update` mit dem Wechsel des Kontos.
- [ ] Ein Tipp auf einen Chip füllt Richtung, Betrag, Ziel, Payee und Memo; das Memo wird genau das der Vorlage, ohne
      Memo leer (ein zweiter Tipp auf einen anderen Chip lässt kein altes Memo stehen). Das Datum bleibt. Kommentar
      an `applyTemplate` nachgezogen. Test unter .NET.
- [ ] Der Text des Chips bleibt „Payee · Betrag“ (`Capture.templateLabel`), das Memo steht nicht im Chip (Quick Add
      zeigte es auch nicht; fünf Chips sollen bei 412 px in zwei Zeilen passen).
- [ ] Die Playwright-Fixture `scripts/ledger-capture-fixture.mjs` trägt `Memo` an jeder Vorlage (neues Pflichtfeld,
      Laufzeitfalle Fable.Remoting aus CLAUDE.md) und Vorlagen auf mindestens zwei Konten; Browser-Tests, die einen
      Chip eines anderen Kontos erwarteten, sind nachgezogen.
- [ ] Ledger-README: Bullet zu den Zuletzt-Chips (Herkunft, je Konto, fünf, Memo) nachgezogen, alter Wortlaut „drei“
      und „nur in BB erfasst“ ersetzt.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Erfassen einer üblichen Bar-Buchung (etwa „Anastasia“) geht mit einem Tipp auf den Chip und „Buchung anlegen“
      mindestens so schnell wie früher mit Quick Add. [human-eye]

## Abweichungen
Quick Add zeigte nur Buchungen mit Kategorie und keine Überträge; hier bleiben Überträge und Buchungen ohne Kategorie
Vorlagen, weil „Neue Buchung“ das heute schon kann. Quick Add las 90 Tage aus YNAB; hier gilt der Rückblick von 50
Buchungen je Konto aus dem Kassenbuch, das den YNAB-Stand spiegelt.

## Notes
Verfeinert 2026-11-14 nach Romans Entscheidungen (oben unter „What“). Abgleich mit Quick Add vor ledger-ea70s
(Version 9645bb72~1): `recentQuickAddTemplates 5` aus `YnabClient.getAccountTransactions` (90 Tage) des
Quick-Add-Kontos, Schlüssel Payee, Richtung, Betrag, Kategorie, Memo der neuesten Buchung, Chip „Payee · Betrag €“.
Heute: `loadCapturedEntryIds` (nur `CashExpenseAdded` mit Actor `UserInBb`/`QuickAdd`), drei Vorlagen über alle Konten,
ohne Memo. Die Vorbelegung lädt bei jedem Öffnen von „Neue Buchung“ neu (`RunLoadCaptureDefaults`), eine eben erfasste
Buchung steht beim nächsten Öffnen also schon als Chip da.
Eine eigene Abfrage je Kontowechsel wurde verworfen: die Vorlagen aller Konten sind wenige Zeilen, der Wechsel soll am
Handy ohne Warten gehen.
Bob ist nicht betroffen (die Chips sind nur im Formular).
Kein Bug: Mit Filter auf dem Comdirect-Girokonto kommt Bar, weil das Import-Konto zum Erfassen gesperrt ist (ledger-at63w); Roman bestätigt, dass Bar dort richtig ist.
