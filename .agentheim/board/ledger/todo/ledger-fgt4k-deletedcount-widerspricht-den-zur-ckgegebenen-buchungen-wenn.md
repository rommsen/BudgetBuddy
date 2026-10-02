---
id: ledger-fgt4k
title: "DeletedCount widerspricht den zurückgegebenen Buchungen, wenn OnlyNeedsReview und IncludeDeleted zusammen aktiv sind"
status: todo
type: bug
context: ledger
created: 2026-09-25
completed:
depends_on: []
blocks: []
tags: [backend, kassenbuch, lesepfad]
related_adrs: [0032, 0036]
related_research: []
prior_art: [ledger-qns4j, ledger-vgfw3, ledger-p3n8k]
---

## Why
Beim manuellen Verifizieren von `ledger-qns4j` (PC-Statusspalte, AC5) gegen eine
Kopie der echten Entwicklungsdatenbank fiel eine Diskrepanz in
`getLedgerEntriesPage` auf: mit `OnlyNeedsReview = true` und
`IncludeDeleted = true` gleichzeitig für ein Konto lieferte die Antwort
`DeletedCount = 1`, aber keine der zwei zurückgegebenen `Entries` trug
`DeletedInYnab = true`. Laut Doc-Kommentar auf `LedgerEntryPage.DeletedCount`
(`src/Shared/Domain.fs`) soll die Zahl "unabhängig von `IncludeDeleted`" die in
YNAB gelöschten Buchungen im *aktiven Filter* zählen (Konto plus
`OnlyNeedsReview`); bei aktivem `IncludeDeleted` sollten also die gezählten
gelöschten Buchungen auch tatsächlich unter den `Entries` auftauchen.

**Verfeinerung (2026-10-02): echter Fehler, Ursache gefunden, gegen eine Kopie
der Entwicklungsdatenbank bestätigt.** `Ledger.Persistence.countDeletedEntries`
(`src/Server/Ledger/Persistence.fs:867`) baut seinen eigenen Filter so:

```fsharp
let deletedFilter = { filter with IncludeDeleted = true; OnlyNeedsReview = false }
```

Das erzwingt `OnlyNeedsReview = false`, unabhängig davon, was `query.OnlyNeedsReview`
tatsächlich war. `DeletedCount` zählt damit IMMER alle gelöschten Buchungen des
Kontos (plus der fünf Such-Filter), auch wenn `OnlyNeedsReview = true` aktiv ist und
`Entries` (über `loadEntriesPage` → `buildFilterClause`, das `OnlyNeedsReview`
korrekt anwendet) nur die davon zeigt, die zusätzlich `(approved = 0 OR
cleared = 'Uncleared')` erfüllen. Eine gelöschte, aber bereits geprüfte Buchung
(`approved = 1`, `cleared <> 'Uncleared'`) zählt deshalb in `DeletedCount` mit,
obwohl sie bei `OnlyNeedsReview = true` nie unter `Entries` auftauchen kann, auch
nicht mit `IncludeDeleted = true`: genau der beobachtete Widerspruch.

Bestätigt gegen die Kopie der Entwicklungsdatenbank (`dev-copy.db`, rein lesend,
`sqlite3`): ein Konto hat genau eine in YNAB gelöschte Buchung, und diese erfüllt
`approved = 0 OR cleared = 'Uncleared'` NICHT (sie ist bereits geprüft). Mit
`OnlyNeedsReview = true` liefert der heutige Code für dieses Konto `DeletedCount = 1`,
aber `Entries` (auch mit `IncludeDeleted = true`) keine einzige Buchung mit
`DeletedInYnab = true`: exakt Romans Repro, nachgestellt mit realen Daten statt
mit den (hier bewusst nicht wiedergegebenen) ursprünglichen IDs.

ADR 0036, Decision 1 behauptet ausdrücklich, dass `countDeletedEntries` dieselbe
`buildFilterClause` inklusive `OnlyNeedsReview` benutzt wie `loadEntriesPage` ("…
die `loadEntriesPage`, `countEntries`, `loadAmountsForQuery`, `countDeletedEntries`
und `loadPayeeSuggestions` alle gemeinsam aufrufen"). Der Code weicht davon ab: der
`OnlyNeedsReview`-Override wurde in `ledger-vgfw3` eingeführt (als `countDeletedEntries`
von "kein Filter" auf "volles `EntryFilter`" umgestellt wurde), ohne dass ADR 0036
oder die Task-Akzeptanzkriterien diese Abweichung je ausdrücklich verlangt oder
begründet hätten: AC5 jener Aufgabe sprach nur von den fünf Such-Filtern
(Kategorie/Zeitraum/Payee/Betrag/Memo), nicht von `OnlyNeedsReview`. Das ist also
kein dokumentiertes Verhalten, sondern eine stillschweigende Abweichung vom eigenen
ADR: ein echter Fehler, kein nur ungenauer Doc-Kommentar.

## What
`countDeletedEntries` so ändern, dass es `OnlyNeedsReview` aus dem übergebenen
`filter` unverändert mitnimmt (nur `IncludeDeleted` wird erzwungen), statt es auf
`false` zu zwingen:

```fsharp
let deletedFilter = { filter with IncludeDeleted = true }
```

Damit zählt `DeletedCount` die gelöschten Buchungen **innerhalb des aktiven
Filters** (Konto, die fünf Such-Filter UND `OnlyNeedsReview`), nicht mehr
unabhängig von `OnlyNeedsReview`. Das deckt sich dann mit ADR 0036 Decision 1 und
mit der Lesart des Doc-Kommentars an `LedgerEntryPage.DeletedCount`, die der Task
ursprünglich zitierte.

Betroffene Stellen:
- `src/Server/Ledger/Persistence.fs:867-883` (`countDeletedEntries`): den
  `OnlyNeedsReview`-Override entfernen (siehe oben); den Doc-Kommentar
  (`:862-866`, "unabhängig von `filter.IncludeDeleted`/`filter.OnlyNeedsReview`")
  auf "unabhängig von `filter.IncludeDeleted`" korrigieren (nicht mehr
  `OnlyNeedsReview`).
- `src/Shared/Domain.fs:953-955` (Doc-Kommentar an `LedgerEntryPage.DeletedCount`):
  unverändert korrekt, keine Code-Änderung nötig; ggf. den Satz "im aktiven Filter"
  um "inklusive `OnlyNeedsReview`" ergänzen, falls das beim Schreiben der
  Akzeptanzkriterien noch Zweifel offenlässt.
- `.agentheim/knowledge/decisions/0036-kassenbuch-filter-lesepfad-betrag-in-fsharp.md`:
  NICHT Teil dieses Tasks (ADR-Nummern sind zugewiesen, 0036 gehört nicht dazu):
  der Fix macht den Code wieder deckungsgleich mit dem bestehenden ADR-Text, es
  braucht also keine neue oder geänderte Decision.

Nicht Teil dieses Tasks: `countNeedsReviewEntries` (der Chip "Zu prüfen (n)")
bleibt unverändert; der ist laut ADR 0036 Decision 6 bewusst ein fester Zähler über
das Konto, unabhängig von `OnlyNeedsReview` UND den fünf Such-Filtern: eine andere,
schon dokumentierte Regel, die dieser Fund nicht berührt.

## Acceptance criteria
- [ ] `countDeletedEntries` übernimmt `filter.OnlyNeedsReview` unverändert (erzwingt
      nur noch `IncludeDeleted = true`), statt es auf `false` zu setzen.
- [ ] Regressionstest (muss vor dem Fix rot sein, danach grün): ein Konto mit genau
      einer in YNAB gelöschten, aber bereits geprüften Buchung (`Approved = true`,
      `Cleared = Cleared` oder `Reconciled`) und mindestens einer ungeprüften,
      nicht gelöschten Buchung. Mit `OnlyNeedsReview = true` und
      `IncludeDeleted = true` liefert `getLedgerEntriesPage`:
      - `DeletedCount = 0` (vorher: `1`, der Fehler),
      - `Entries` enthält keine Buchung mit `DeletedInYnab = true` (unverändert).
      In `src/Tests/LedgerPageTests.fs`, eigenes Konto (nächster freier Buchstabe
      nach den bestehenden `pageTestAccount*`), analog zu den bestehenden
      `OnlyNeedsReview`/`DeletedCount`-Tests ab Zeile ~1704 und ~1750. Zum Seeden der
      gelöschten, geprüften Buchung: `seedReviewedEntry` (Zeile 1385) gefolgt von
      derselben `EntryDeletionObservedInYnab`-Sequenz wie in `seedDeletedEntry`
      (Zeile 1372), notfalls eine kleine neue Hilfsfunktion
      `seedDeletedReviewedEntry`, analog zu `seedDeletedEntryFull`/`seedDeletedEntry`.
- [ ] Intakt-Szenario, nicht neu zu schreiben, nur zu prüfen: der bestehende Test
      "DeletedCount zählt die gelöschten Buchungen unabhängig vom Schalter" (Zeile
      1704) bleibt grün: dort ist die gelöschte Buchung ungeprüft (`seedEntry`,
      `Approved = true, Cleared = Uncleared` macht sie laut `needsReviewSqlClause`
      zu "needs review"), der Fix ändert an diesem Fall nichts.
- [ ] Der Doc-Kommentar an `countDeletedEntries` (`src/Server/Ledger/Persistence.fs`)
      nennt nach dem Fix nur noch `IncludeDeleted`, nicht mehr `OnlyNeedsReview`, als
      das, wovon `DeletedCount` unabhängig zählt.
- [ ] `dotnet build` und `dotnet test` grün.

## Notes
Nicht selbst untersucht oder behoben im Rahmen von `ledger-qns4j`: das liegt im
Server-Lesepfad (`src/Server/`), außerhalb des Umfangs jener (reinen
Client-)Aufgabe, und im selben Bereich, den der parallele Worker
`ledger-xk9wq` im selben Batch anfasst (Server-Ledger-Persistenz/-Domäne).
Die konkreten Konto- und Buchungs-IDs sowie der Kontoname aus dem ursprünglichen
Repro sind hier absichtlich nicht wiedergegeben: sie stammten aus Romans echter
Entwicklungsdatenbank vom 2026-09-25. Die Verfeinerung hat den Fehler stattdessen
gegen eine frische Kopie derselben Entwicklungsdatenbank mit einer rein lesenden
`sqlite3`-Abfrage bestätigt (ein Konto mit genau einer gelöschten, bereits
geprüften Buchung, `approved = 0 OR cleared = 'Uncleared'` dafür falsch); auch diese
Kontodaten werden hier nicht einzeln benannt, nur der Befund.

Historischer Hinweis für den Worker: der `OnlyNeedsReview = false`-Override kam mit
`ledger-vgfw3` (als `countDeletedEntries` auf das volle `EntryFilter` umgestellt
wurde) in den Code, ohne dass eine Akzeptanzkriterium oder ADR 0036 ihn verlangt.
Es ist also keine absichtliche Altlast, die zu respektieren wäre, sondern die
eigentliche Fehlerquelle.