---
id: 0029
title: Seitenweise Kassenbuch-Liste, Kontostände-Leiste und Konto-Filter (AllAccounts vs. OneAccount)
scope: ledger
status: accepted
date: 2026-09-12
supersedes: []
superseded_by: []
related_tasks: [ledger-b1xwe]
related_adrs: [0013, 0017]
related_research: []
---

# ADR 0029: Seitenweise Kassenbuch-Liste, Kontostände-Leiste und Konto-Filter

## Context
`ledger-b1xwe` ist das Fundament der sieben Scheiben aus `ledger-s8m3w`: eine
serverseitige, seitenweise Sicht über die Buchungsliste (ein Konto oder alle Konten),
eine Kontostände-Leiste, und ein neues Konto-Filter-Modell, das "Alle Konten" als
eigenen, gleichwertigen Filterwert kennt statt als Sonderfall von "kein Konto
gewählt". Bisher gab es `getLedgerEntriesForAccount` (lädt ein ganzes Konto auf
einmal) und ein `SelectedAccountId: Guid option`, bei dem `None` "noch nichts
geladen" bedeutete. Bei ~14.000 Buchungen ist "ganzes Konto laden" nicht mehr
tragfähig, und "Alle Konten" braucht einen Wert, der sich nicht mit "nichts
gewählt" überschneidet.

## Decision

1. **Zwei neue Server-Sichten neben der bestehenden.** `getLedgerEntriesPage`
   (Konto optional, Seitenzeiger, Seitengröße, liefert Seite + Gesamtzahl + Summe
   über den GANZEN Treffer) und `getLedgerAccountBalances` (alle Konten mit Stand in
   einem Aufruf). `getLedgerEntriesForAccount` bleibt bestehen (ADR-Notiz: "die
   bleibt, bis nichts mehr auf sie zeigt") — der Client nutzt sie nicht mehr, andere
   Aufrufer könnten es tun.

2. **Zwei Zweige statt eines Nullable-Parameter-Tricks in SQL.** Erste Version von
   `loadEntriesPage`/`countEntries`/`loadAmountsForQuery` benutzte
   `WHERE (@accountId IS NULL OR account_id = @accountId)` mit `Option.toObj`. Ein
   Integrationstest (AC1: "Ohne Konto-Filter zeigt alle Konten") schlug reproduzierbar
   fehl: Konto B fehlte in der Gesamtsicht. Statt die Ursache im Nullable-Parameter-
   Binding weiterzuverfolgen, wurde auf zwei explizite SQL-Texte (mit/ohne
   `WHERE account_id = @accountId`) umgestellt — seither grün. Explizit statt clever:
   ein Konto-Filter ist genau zwei Fälle, kein generischer Nullable-Mechanismus.

3. **Die Treffer-Summe wird über ALLE Beträge des Treffers gebildet, nicht nur die
   geladene Seite** (AC1) — in F#, nie per SQL-`SUM` (ADR 0017). Anders als
   `ledgerBalance` (die schon existierende Kontostand-Funktion, die gelöschte
   Buchungen ausschließt) schließt die neue `sumMoney` nichts aus: die
   Treffer-Summe einer Seiten-Abfrage ist heute noch ungefiltert (kein Filter-Chip in
   dieser Scheibe), *nicht* dasselbe wie ein Kontostand. Diese Trennung ist bewusst:
   spätere Filter (`ledger-vgfw3`) und die Summenzeile (README, "Auswahl mit Summe")
   bauen auf `sumMoney` auf, ohne dass `ledgerBalance`s Ausschluss-Semantik
   angefasst werden muss.

4. **Kontostände werden unabhängig von jeder geladenen Seite berechnet.**
   `getLedgerAccountBalances` lädt IMMER alle Beträge aller Buchungen (`loadAmountsForBalances`,
   kein Konto-Parameter) und filtert/gruppiert in F# (`ledgerBalancesByAccount`,
   Shared.Domain). Test "Der Kontostand ist derselbe, ob man ihn aus zwei oder aus
   zwanzig Häppchen zusammensetzt" hält die Invariante fest: der Stand kommt nie aus
   dem geladenen Listen-Ausschnitt (AC2).

5. **`AccountFilter = AllAccounts | OneAccount of Guid` statt `Guid option`.**
   Der bisherige Typ `SelectedAccountId: Guid option` konnte "Alle Konten" nicht
   abbilden, ohne mit "noch nichts geladen" (leeres Kassenbuch) zu kollidieren — beides
   wäre `None` gewesen. Der neue Summentyp macht beide Fälle unterscheidbar:
   `Model.SelectedFilter: AccountFilter option`, wobei `None` weiterhin "noch nichts
   geladen/leeres Kassenbuch" bedeutet und `Some AllAccounts` ein eigener,
   gleichberechtigter Wert ist (Task-Notes: "'Alle Konten' ist ein eigener Eintrag ...,
   kein Schalter"). Die Vorauswahl beim Laden bleibt ein spezifisches Konto (Server-
   Default), nicht automatisch "Alle Konten" — das Konzept sagt nur, dass "Alle
   Konten" *verfügbar* ist, nicht dass es der Startzustand ist.

6. **Seitenweises Nachladen als reine/unreine Nachrichten-Paar**, dem bestehenden
   Muster der Seite folgend (ADR 0020): `LoadEntries`/`SelectFilter` (rein, setzt
   Seite 0 zurück) → `RunLoadEntriesPage` (unrein, ruft die API) →
   `EntriesPageLoaded` (hängt an, wenn `page > 0`, ersetzt bei `page = 0`) →
   `LoadMoreEntries` (reiner Wächter: kein zweiter Aufruf während `LoadingMore`,
   keiner ohne `HasMoreEntries`). `HasMoreEntries` ist reine Ableitung aus geladener
   Anzahl gegen `TotalCount`, nach jeder Seite neu gesetzt.

7. **Nachladen per `IntersectionObserver`, minimal selbst gebunden.** Fable.Browser.Dom
   (Paketversion in diesem Repo) kennt keinen `IntersectionObserver`-Typ. Statt eines
   vollen Typ-Bindings (Entries, Optionen, Root-Element) reicht ein einzeiliger
   `[<Emit>]`-Wrapper, der Observer, `observe` und `disconnect` in einer JS-Closure
   kapselt und nur `(Element, callback) -> disconnect` nach außen zeigt — analog zu
   `Viewport.fs`s Muster für nicht gebundene Browser-APIs. Kein `root`-Parameter: die
   Seite scrollt im Fenster.

8. **Konto-Kürzel (AC4, Zeile in der Gesamtsicht) ist eine reine
   Anzeige-Ableitung, kein Server-Feld.** `LedgerAccountView` trägt keine
   "Kürzel"-Spalte; `accountAbbreviation` (Client, `View.fs`) nimmt das erste Wort
   des Kontonamens, bis zu vier Zeichen, groß geschrieben ("ING Extra" → "ING",
   "Girokonto Comdirect" → "GIRO"). Das ist eine Verfeinerungs-Annahme (siehe
   Task-Notes: Claude-Annahme, Roman kann beim Durchspielen korrigieren); die
   erfundenen Kürzel im Brief ("CD" für Comdirect) sind nicht algorithmisch
   herleitbar und nicht bindend, da sie selbst nur Beispieldaten sind.

## Consequences

### Positive
- Filter-Tasks (`ledger-vgfw3` u. a.) können auf `getLedgerEntriesPage` und
  `sumMoney` aufbauen, ohne den Lesepfad noch einmal umzubauen.
- Kontostand und Treffer-Summe sind zwei erkennbar getrennte Konzepte im Code
  (`ledgerBalance` vs. `sumMoney`/`ledgerBalancesByAccount`), nicht heimlich
  dasselbe mit unterschiedlichem Namen.
- `AccountFilter` macht die drei Zustände (kein Filter/Konto/Alle Konten) im
  Typsystem sichtbar statt in einer stillschweigenden `None`-Doppelbedeutung.

### Negative
- Zwei Server-Sichten für "Buchungen eines Kontos" (`getLedgerEntriesForAccount`,
  `getLedgerEntriesPage`), bis die alte nichts mehr anspricht — bewusst in Kauf
  genommen (Task-Vorgabe), Aufräumen ist ein späterer Task.
- Das Konto-Kürzel ist eine Annahme ohne Server-Grundlage; wenn Roman andere
  Kürzel erwartet, ist das eine Client-only-Änderung.

## Alternatives considered
- **Nullable-`WHERE`-Parameter für den Konto-Filter**: verworfen nach einem
  reproduzierbaren Testfehler (Punkt 2) zugunsten zweier expliziter Zweige.
- **`SelectedAccountId: Guid option` beibehalten, "Alle Konten" als `Guid.Empty` oder
  Sentinel-Wert**: verworfen — ADR 0020/bestehende Konvention verbietet
  `Guid.Empty`-Platzhalter, und ein Sentinel wäre im Typsystem nicht von einer
  echten Konto-Id unterscheidbar.
