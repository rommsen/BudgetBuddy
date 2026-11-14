---
id: 0095
title: Betragsfilter als Höhe ohne Vorzeichen, die Richtung als eigener Umschalter
scope: ledger
status: accepted
date: 2026-11-14
supersedes: []
superseded_by: []
related_tasks: [ledger-d9z3n]
related_adrs: [0017, 0036, 0087]
related_research: []
---

# ADR 0095: Betragsfilter als Höhe ohne Vorzeichen, die Richtung als eigener Umschalter

## Context

Der Betragsfilter im Kassenbuch (`AmountMin`/`AmountMax`, ledger-vgfw3, ADR 0036) verglich den Betrag mit Vorzeichen.
Eine Ausgabe über 12,00 € steht als -12,00 im Kassenbuch: „von 5 bis 50“ traf sie nicht, nur Einnahmen. Wer Ausgaben
suchte, musste „von -50 bis -5“ tippen, mit vertauschter Reihenfolge der Grenzen. Am Handy hat die Zifferntastatur zu
`inputmode="decimal"` je nach Tastatur-App kein Minus (CLAUDE.md), ein Betragsfeld trägt deshalb kein Vorzeichen. Mit
der Handy-Form (ledger-d9z3n) fiel die Lücke auf: die Felder ohne Vorzeichen ließen sich nur noch für Einnahmen nutzen.
Roman hat am 2026-11-14 entschieden.

## Decision

1. `AmountMin`/`AmountMax` grenzen die Höhe des Betrags ohne Vorzeichen ein (`abs amount`), für Ausgaben wie Einnahmen.
   „von 5 bis 50“ trifft -12,00 € und +12,00 €. Beide Grenzen bleiben einzeln offen, ein vertauschtes Intervall bleibt
   leer.
2. Die Richtung ist ein eigenes Feld `AmountDirection: AmountDirection` (`AllDirections | OnlyOutflows | OnlyInflows`)
   auf `LedgerEntryPageQuery`, `LedgerPayeeSuggestionQuery`, `Ledger.Persistence.EntryFilter` und im Client auf
   `Filters`. Ausgabe heißt Betrag unter 0, Einnahme Betrag über 0; ein Betrag von genau 0 ist keines von beiden und
   fällt bei gesetzter Richtung heraus.
3. Die Richtung wirkt auch ohne Betrag. Sie läuft wie der Betrag im F#-Nachfilter (`amountMatches`, ADR 0017: kein
   SQL-Vergleich auf dem Betragstext); `hasAmountFilter` ist wahr, sobald eine Grenze oder eine Richtung gesetzt ist. So
   gilt sie für alle Zählwege (`loadEntriesPage`, `countEntries`, `loadAmountsForQuery`, `countDeletedEntries`) und die
   Payee-Vorschläge.
4. Im Client nehmen die Felder „Betrag von“ und „bis“ kein Vorzeichen an: `withoutAmountSign` streicht „-“, „+“ und
   das typografische Minus beim Tippen, `parseAmountFilterText` liest ein Vorzeichen als unlesbar. Darunter steht der
   Umschalter „Alle | Ausgaben | Einnahmen“ (`Segmented`), im Filter-Sheet unter 1024 px und am PC im Popover „Betrag“.
   Er wirkt sofort, ohne Verzögerung.
5. Eine gesetzte Richtung ist ein Filter (`hasAnyFilter`): unter 1024 px ein eigener Chip „Ausgaben“ bzw. „Einnahmen“
   hinter dem Betrag-Chip, er zählt zu „Filter (n)“, sein „×“ setzt die Richtung auf „Alle“. Am PC trägt der Chip
   „Betrag“ beides, sein „×“ nimmt Betrag und Richtung zurück. „Filter löschen“ setzt sie auf „Alle“.

## Consequences

- Der Vertrag in `src/Shared` hat ein neues Pflichtfeld (Laufzeitfalle Fable.Remoting): ein älterer Client ohne das
  Feld scheitert an der Seitenanfrage, bis er neu lädt. Client und Server gehen zusammen aus, wie beim Wechsel von
  Payee und Memo auf `Search` in derselben Task. Die Fixtures in `scripts/*.test.mjs` bauen keine Seitenanfragen.
- Ein Filter auf genau eine Buchung („-4,50“) geht jetzt als Höhe 4,50 mit Richtung „Ausgaben“. Ohne Richtung trifft
  er auch eine Einnahme über 4,50 €; das ist gewollt.
- Die Richtung allein zwingt den F#-Nachfilter und lädt den Treffer ohne `LIMIT`, wie heute ein Betrag. Bei Romans
  Datenmenge ist das unkritisch (ADR 0036); wird es eng, kann die Richtung als SQL-Bedingung auf das Vorzeichen des
  Betragstextes (`amount LIKE '-%'`) vorgezogen werden, ohne die Regel zu ändern.

## Alternatives considered

- **Vorzeichen in den Feldern behalten**: am Handy ohne Minus auf der Zifferntastatur nicht bedienbar, und „von -50 bis
  -5“ ist für Ausgaben unnatürlich.
- **Höhe ohne Richtung**: einfacher, aber dann ließen sich Ausgaben und Einnahmen gleicher Höhe nicht trennen.
- **Richtung nur als Zusatz zum Betrag** (ohne Betrag wirkungslos): Roman will auch „nur Einnahmen“ ohne Bereich sehen.
