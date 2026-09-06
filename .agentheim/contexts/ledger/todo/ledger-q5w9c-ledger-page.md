---
id: ledger-q5w9c
title: Kassenbuch-Seite: Buchungen eines Kontos mit Herkunfts-Badge und Historie, "Jetzt spiegeln"
status: todo
type: feature
context: ledger
created: 2026-09-06
depends_on: [ledger-m8r2v, design-system-001]
blocks: [ledger-kt0wq]
tags: [foundation, frontend]
related_adrs: [0013, 0014, 0016, 0005]
related_research: []
prior_art: []
---

## Why
Slice 3 von 3 des Walking Skeletons (`ledger-kt0wq`). Ohne Sicht bleibt das Kassenbuch
unsichtbar ("wenn niemand hineinschaut" ist ein Scheiterkriterium der Vision). Die Seite
beweist, dass Log, Spiegel und Anzeige zusammen funktionieren.

## What
1. **Queries** (semantische Read-Funktionen, Read-Models in `src/Shared/Domain.fs`):
   `getLedgerAccounts`, `getLedgerEntriesForAccount accountId` (Datum, Payee, Betrag,
   Kategorie-Name, Herkunft, gelöscht-Flag, Splits), `getEntryHistory entryId` (Historie
   als Liste: Zeitpunkt, Actor, sprechende Beschreibung des Ereignisses),
   `getMirrorStatus` (letzter Lauf). Endpunkte in `src/Shared/Api.fs` und `src/Server/Api.fs`.
2. **Neue Seite "Kassenbuch"** unter Route `#/ledger` (Hash-Routing wie die anderen
   Seiten), Eintrag in der Navigation, eigene Komponente `src/Client/Components/Ledger/`
   (Types, State, View) nach MVU mit RemoteData.
3. **Anzeige**: Konto-Auswahl (Standard: das Konto, das `ynab_default_account_id`
   entspricht, sonst das erste), Kontostand als Summe der nicht gelöschten Buchungen,
   Buchungsliste (Datum, Payee, Betrag über `Money`, Kategorie, Herkunfts-Badge
   "YNAB-Spiegel"), gelöschte Buchungen bleiben sichtbar mit Badge "in YNAB gelöscht".
   Tippen auf eine Buchung öffnet die Historie (BottomSheet nach ADR 0005) mit allen
   Einträgen und Zeitpunkt.
4. **"Jetzt spiegeln"**: Button im PageHeader ruft `mirrorYnabBudget`, währenddessen
   Hinweis "Spiegel läuft" (Loading), danach Liste neu laden und Zusammenfassung als Toast.
   Letzter Lauf mit Zeitpunkt sichtbar. Kein automatischer Auslöser in diesem Slice.
5. **Nur DS-Komponenten und Tokens** (Styleguide-Gate `design-system-001`); mobil lesbar.
6. **Tests**: Reducer-Tests für den Ledger-State (Laden, Konto wechseln, Spiegel-Lauf
   Start/Ende/Fehler; keine Pfade, die den Fable.Remoting-Proxy berühren), Read-Model-
   Mapping rein (Ereignis → Historie-Beschreibung) mit Tests.

## Acceptance criteria
- [ ] Route `#/ledger` mit Navigationseintrag; Seite zeigt Konten, Buchungen und
      Kontostand des gewählten Kontos.
- [ ] Jede Buchung zeigt Herkunfts-Badge; gelöschte Buchungen sind markiert, nicht
      verschwunden.
- [ ] Historie pro Buchung im BottomSheet, Einträge mit Zeitpunkt und sprechender
      Beschreibung.
- [ ] "Jetzt spiegeln" mit Lauf-Hinweis, Neuladen und Ergebnis-Toast; letzter Lauf sichtbar.
- [ ] Ausschließlich DS-Komponenten und Tokens; keine Inline-Farben.
- [ ] `dotnet build`, `dotnet test`, `npm run build` grün.
- [ ] `ledger/README.md`: Abschnitt "Sichten" mit den Queries.

## Notes
Historie-Beschreibungen sind Sprache des Kassenbuchs ("Kategorie in YNAB geändert:
Lebensmittel → Restaurant"), keine Typnamen. Screenshot-Rezept für die Abnahme: `npm run
dev`, Vite auf 5181, Route `/#/ledger`.
