---
id: ledger-k3wqn
title: "Payee- und Memo-Filter finden keine Umlaut-Schreibweisen: COLLATE NOCASE faltet nur ASCII"
status: done
type: bug
context: ledger
created: 2026-09-20
completed:
depends_on: []
blocks: [ledger-xk9wq, ledger-h4qtx]
tags: [backend, kassenbuch, filter, suche]
related_adrs: [0036, 0037]
related_research: []
prior_art: [ledger-vgfw3]
---

## Why

`ledger-vgfw3` (ADR 0036) hat Payee- und Memo-Filter im Kassenbuch-Lesepfad eingeführt und
beide ohne Beachtung der Groß-/Kleinschreibung gebaut:

- `payee = @payee COLLATE NOCASE`
- `memo LIKE @memo COLLATE NOCASE`

SQLites `NOCASE` faltet aber **nur ASCII A-Z**. Gegen sqlite3 nachgemessen (2026-09-20):

```
INSERT INTO t VALUES ('MÜLLER GMBH'), ('Müller GmbH');

SELECT payee FROM t WHERE payee = 'müller gmbh' COLLATE NOCASE;
  -> liefert NUR 'Müller GmbH'
  -> liefert NICHT 'MÜLLER GMBH'

SELECT memo FROM t WHERE memo LIKE '%grüne%' COLLATE NOCASE;
  -> liefert NUR 'Grüne Erde'
  -> liefert NICHT 'GRÜNE ERDE Rechnung'
```

Zur Gegenprobe: rein ASCII funktioniert (`'cafe lotte'` findet `'Cafe Lotte'`).

**Warum das hier wirklich weh tut.** Comdirect- und YNAB-Verwendungszwecke stehen häufig in
Großbuchstaben: `MÜLLER`, `BÄCKEREI`, `GRÜNE`, `STRAßE`. Der Memo-Filter ist das Feld, in das
Roman frei tippt. Tippt er `müller` gegen ein gespeichertes `MÜLLER`, kommt **kein Treffer**
zurück, ohne Fehler und ohne Hinweis. Das ist die schlechteste Form eines Fehlers: er sieht
aus wie "es gibt nichts".

**Warum kein Test das gefunden hat.** Alle acht Filter-Tests aus `ledger-vgfw3` säen reines
ASCII (`"cafe lotte"` / `"Cafe Lotte"` in AC6, das transliterierte `"Baecker"` in AC7). Der
Fall kann dort strukturell nicht auftreten. Gefunden im QA-Review am Ende des Work-Laufs vom
2026-09-19, nicht von der Verifikation der Aufgabe selbst.

**Entschieden (Roman, 2026-09-20):** Der Fehler wird repariert, die Einschränkung wird nicht
festgeschrieben. Das Wie steht in **ADR 0037**: eine anwendungsdefinierte SQL-Funktion
`bb_lower` im Lesepfad, keine Normalform-Spalte, kein Backfill. Die Belege dafür (kein ICU im
Bundle, `LIKE` ignoriert Kollationen, kein Index auf `payee`, Invariant-Modus im Container)
stehen im Context-Abschnitt von ADR 0037 und sind nicht neu zu erheben.

**Nicht zu verwechseln mit `ledger-h4qtx`.** Jenes Item behandelt die Payee-*Vorschläge*, die
groß-/kleinschreibungs-empfindlich gruppieren. Diese Aufgabe fasst `loadPayeeSuggestions`
nicht an.

## What

ADR 0037 umsetzen: die Faltung in eine anwendungsdefinierte SQL-Funktion legen und beide
Textfilter darüber vergleichen. Ein Weg, drei Dateien, kein Schema-Eingriff.

**1. `src/Server/Persistence.fs`**, zwischen `activeConnectionString` (Z. 103) und
`getConnection` (Z. 109):

- `let internal searchFold (s: string) : string` mit `ToLowerInvariant`, `null` wird
  durchgereicht (damit `bb_lower(NULL)` SQL-`NULL` bleibt und Buchungen ohne Payee die
  Gleichheit wie bisher nicht treffen). **`String.Normalize` darf nicht vorkommen**: es
  zerlegt im Globalization-Invariant-Modus des Containers nicht, der Test wäre lokal grün und
  in Produktion wirkungslos (ADR 0037 Context 4).
- `withSearchFunctions`, das `conn.CreateFunction<string, string>("bb_lower", (fun s ->
  searchFold s), true)` aufruft. Das dritte Argument ist `isDeterministic`, setzen.
- `getConnection` (Z. 109-118) gibt die frisch erzeugte Connection durch
  `withSearchFunctions` zurück, **vor** `Open()`. Die Registrierung muss auf jeder Connection
  passieren; Microsoft.Data.Sqlite legt die Funktionen bei jedem `Open()` auf den Handle,
  über Pool-Zyklen hinweg nachgemessen.
- Die Faltfunktion kann **nicht** in `Ledger.Domain` liegen, obwohl sie rein ist:
  `Server.fsproj` kompiliert `Persistence.fs` (Z. 18) vor `Ledger/Domain.fs` (Z. 24).
- F#-Stolperstein: explizite Typargumente setzen und das Lambda als `(fun s -> searchFold s)`
  schreiben, nicht den Funktionswert `searchFold` übergeben, sonst scheitert die
  Überladungsauflösung.

**2. `src/Server/Ledger/Persistence.fs`**, `buildFilterClause`, die beiden Zweige in
Z. 650-660:

- Payee: `" AND bb_lower(payee) = @payee"`, Parameter `Core.searchFold payee`.
- Memo: `" AND bb_lower(memo) LIKE @memo"`, Parameter `$"%%{Core.searchFold memo}%%"`.
- `COLLATE NOCASE` verschwindet an beiden Stellen restlos.
- **Beide Seiten des Vergleichs müssen durch dieselbe Funktion.** `buildFilterClause` faltet
  den Filterwert selbst, statt sich auf den Aufrufer zu verlassen. `EntryFilter.Payee`/`Memo`
  bleiben roher Eingabetext. Faltet nur die Spalte, findet jeder Suchtext mit einem
  Großbuchstaben still nichts, also derselbe Fehler in neuer Verkleidung.
- `Core` ist der Alias auf `global.Persistence` (Z. 21), `searchFold` ist wie `getConnection`
  `internal` und damit erreichbar.

**3. `src/Tests/LedgerPageTests.fs`**: neues Testkonto `pageTestAccountP` (nächste freie Guid
`0195bb00-0000-7000-8000-000000000019`) bei Z. 997 plus die `saveAccount`-Zeile in
`seedAccounts` nach Z. 1077. Laufende Nummern ab 96 (95 ist die höchste vergebene). Neuer
`testCase` in der Gruppe "Fünf Such-Filter im Lesepfad", direkt nach AC6 (endet Z. 1548).

**Nicht anfassen:** `entryParams`, `upsertEntrySql`, `appendAndApply`,
`initializeLedgerSchema`, `migrateLedgerEntryColumns`, `Ledger.Domain.Entry`, die
Ereignis-Payloads, `QueryHandlers.toEntryFilter`, `getLedgerPayeeSuggestions`,
`loadPayeeSuggestions`, `countNeedsReviewEntries`. Die fünf Lesefunktionen und der
`loadAllMatching`-Zweig ziehen über `buildFilterClause` automatisch mit, das ist der Zweck von
ADR 0036 Decision 1.

## Acceptance criteria

- [ ] Ein neuer `testCase` in `src/Tests/LedgerPageTests.fs`, Testgruppe "Fünf Such-Filter im
      Lesepfad", sät auf `pageTestAccountP` echte Produktionsschreibweisen, kein
      transliteriertes ASCII: n=96 `MÜLLER GMBH` / `GRÜNE ERDE Rechnung` / -10,00; n=97
      `Müller GmbH` / `Grüne Erde Rechnung` / -20,00; n=98 `Schmidt AG` / `Blaue Erde` /
      -30,00; n=99 `STRAßE DES 17. JUNI` / `Parkgebühr` / -4,50; n=100 `MÜLLER GMBH` /
      `GRÜNE ERDE Nachtrag` / -4,50.
- [ ] Dieser Test ist gegen den Stand vor der Änderung **rot** und danach grün. Er prüft
      mindestens: `Payee = Some "müller gmbh"` liefert `TotalCount = 3` (heute 1);
      `Payee = Some "MÜLLER GMBH"` liefert ebenfalls `TotalCount = 3` (heute 2, fängt eine
      Lösung ab, die nur eine Seite faltet); `Memo = Some "grüne"` liefert `TotalCount = 3`
      (heute 1); `Memo = Some "GRÜNE ERDE"` liefert `TotalCount = 3`;
      `Payee = Some "straße des 17. juni"` liefert `TotalCount = 1`.
- [ ] Die Memo-Assertions stehen ausdrücklich im Test: sie sind der Beleg dafür, dass eine
      SQLite-Kollation nicht gereicht hätte (`LIKE` ignoriert Kollationen, ADR 0037).
- [ ] Im Fall `Payee = Some "müller gmbh"` prüft der Test zusätzlich
      `List.length page.Entries = 3` und `TotalAmount = { Amount = -34.50m; Currency = "EUR" }`,
      damit nicht nur `countEntries`, sondern auch `loadEntriesPage` und
      `loadAmountsForQuery` belegt sind.
- [ ] Der `loadAllMatching`-Zweig ist mit abgedeckt: `Payee = Some "müller gmbh"` zusammen mit
      `AmountMin = Some -4.50m; AmountMax = Some -4.50m` liefert `TotalCount = 1` und
      `TotalAmount = { Amount = -4.50m; Currency = "EUR" }` (nur n=100).
- [ ] Die Registrierung ist über den Verbindungs-Pool belegt: derselbe gefilterte Aufruf wird
      im selben Testfall ein zweites Mal ausgeführt und liefert dasselbe Ergebnis. Das ist der
      einzige echte Ausfallmodus dieser Lösung.
- [ ] Die Scope-Grenze ist als Test festgenagelt: `Payee = Some "strasse des 17. juni"`
      liefert `TotalCount = 0` und `Memo = Some "gruene"` liefert `TotalCount = 0`, je mit
      Kommentar, dass Diakritika-Entfernung ausdrücklich nicht Teil von ADR 0037 ist.
- [ ] `grep -c "COLLATE NOCASE" src/Server/Ledger/Persistence.fs` ergibt 0, und
      `grep -c "Normalize" src/Server/Persistence.fs src/Server/Ledger/Persistence.fs` ergibt
      ebenfalls 0.
- [ ] Der Schreibpfad ist unangetastet: der Diff enthält kein `ALTER TABLE`, kein
      `CREATE INDEX` und keine Änderung an `entryParams`, `upsertEntrySql`, `appendAndApply`,
      `initializeLedgerSchema`, `migrateLedgerEntryColumns` oder `Ledger.Domain.Entry`.
- [ ] Die bestehenden Tests bleiben unverändert grün, namentlich AC6 der Filtergruppe
      (ASCII-Unempfindlichkeit, `"cafe lotte"` findet `"Cafe Lotte"`) und die drei
      Replay-Pflichttests in `src/Tests/LedgerTests.fs` (Z. 932, 972, 1014).
- [ ] README und ADR 0036 stimmen mit dem Code überein: der Eintrag **Such-Filter
      (Kassenbuch)** im `ledger`-README sagt bei Payee und Memo, dass die
      Groß-/Kleinschreibung auch für Umlaute gefaltet wird (`bb_lower`, ADR 0037), und benennt
      die Grenze (`mueller` findet `Müller` nicht, `STRASSE` findet `Straße` nicht). Der
      Neutral-Abschnitt von ADR 0036, der "der Payee-Filter selbst `COLLATE NOCASE`
      vergleicht" behauptet, trägt einen Verweis auf ADR 0037.
- [ ] ADR 0037 liegt unter `.agentheim/knowledge/decisions/` mit
      `related_tasks: [ledger-k3wqn]`.
- [ ] `dotnet build` und `dotnet test` grün, ohne `--settings`-Umgehung (ADR 0030).

## Notes

Gefunden im QA-Review (`qa-milestone-reviewer`) nach dem Work-Lauf vom 2026-09-19, unabhängig
gegen sqlite3 nachgeprüft. Der Befund betrifft ausgelieferten Code auf `feature/kassenbuch`,
Commit `203232d`.

Laufzeit gegen 14.000 synthetische Zeilen gemessen (2026-09-20): Payee-Filter über `bb_lower`
8,6 ms, Memo-Filter 10,6 ms, ungefilterter Count 0,2 ms. Kein Anlass für eine Normalform-Spalte
bei heutigem Volumen; die Schwelle steht in ADR 0037 (Neutral).

Reihenfolge für den Worker: Test zuerst (rot sehen), dann `searchFold`/`withSearchFunctions`,
dann die zwei Zeilen in `buildFilterClause`, dann README-Delta und ADR, dann Diary.

Terminierung: `ledger-xk9wq` ändert dieselbe Funktion `buildFilterClause` (Kategorie-Zweig,
Z. 638-648, unmittelbar oberhalb). Nicht parallel in getrennten Worktrees laufen lassen.
`ledger-h4qtx` sollte nach dieser Aufgabe laufen und dann `bb_lower`/`Core.searchFold`
wiederverwenden.

## Outcome

Der Payee- und der Memo-Filter des Kassenbuchs falten Groß-/Kleinschreibung jetzt auch für
Umlaute. Der Fix folgt ADR 0037 wörtlich: eine anwendungsdefinierte SQL-Skalarfunktion
`bb_lower` (`Persistence.searchFold`, `ToLowerInvariant`, `null` durchgereicht) wird über
`withSearchFunctions` auf jeder von `Persistence.getConnection` ausgegebenen Connection
registriert, vor `Open()` (`src/Server/Persistence.fs`, zwischen `activeConnectionString` und
`getConnection`). `Ledger.Persistence.buildFilterClause` vergleicht beide Textfilter jetzt über
`bb_lower(payee) = @payee` und `bb_lower(memo) LIKE @memo`, mit dem Parameterwert über
`Core.searchFold` vorgefaltet, `COLLATE NOCASE` ist an beiden Stellen restlos verschwunden
(`src/Server/Ledger/Persistence.fs`, Payee-/Memo-Zweig von `buildFilterClause`). Schreibpfad,
Schema und `Ledger.Domain.Entry` sind unangetastet.

Neuer Test `src/Tests/LedgerPageTests.fs`, Testgruppe "Fünf Such-Filter im Lesepfad", direkt
nach AC6: `AC-Umlaut: Payee- und Memo-Filter falten Groß-/Kleinschreibung auch bei Umlauten
(ledger-k3wqn, ADR 0037)` auf einem eigenen Testkonto `pageTestAccountP`
(`0195bb00-0000-7000-8000-000000000019`) mit fünf Buchungen in echter Produktionsschreibweise
(`MÜLLER GMBH`/`GRÜNE ERDE Rechnung`, `Müller GmbH`/`Grüne Erde Rechnung`, `Schmidt AG`/`Blaue
Erde`, `STRAßE DES 17. JUNI`/`Parkgebühr`, `MÜLLER GMBH`/`GRÜNE ERDE Nachtrag`). Er deckt beide
Filterrichtungen (Suchtext klein wie groß), den Memo-Teiltext als Beleg gegen eine reine
SQLite-Kollation (die `LIKE` nicht erreicht), `loadEntriesPage`/`loadAmountsForQuery` über
`TotalCount`/`List.length`/`TotalAmount`, den `loadAllMatching`-Zweig mit Betragsfilter, die
Registrierung über den Verbindungs-Pool sowie die bewusste Scope-Grenze (`strasse`/`gruene`
transliteriert finden `Straße`/`Grüne` nicht).

Rot vor Grün, mit Zahlen: gegen den Stand vor der Änderung lieferte
`Payee = Some "müller gmbh"` `TotalCount = 1` statt der erwarteten `3`
(`dotnet test --filter "FullyQualifiedName~Umlaut"`, Fehlermeldung "expected: 3, actual: 1",
exakt der in den Task-Notes vorhergesagte Ist-Zustand). Nach der Änderung grün.

Nebenbefund während der Umsetzung: `pageEntryId` (`src/Tests/LedgerPageTests.fs`) baute die
Entry-Guid mit `n:D2`; für n=100 (von AC1 verlangt) lieferte das drei statt zwei Ziffern und
damit eine ungültige Guid. Behoben durch eine Nullstelle weniger im Präfix plus `n:D3`
(`000000000{n:D3}` statt `0000000000{n:D2}`); für n=0..99 identischer Text wie zuvor, für n=100
jetzt gültig. Keine bestehende Entry-Id ändert sich dadurch.

**Verifikation (Iteration 1, PASS).** Der Verifier hat den Rot-Zustand nicht geglaubt, sondern
unabhängig nachgemessen: ein eigenes `dotnet fsi`-Skript gegen denselben gebundenen Provider
(Microsoft.Data.Sqlite 9.0.13) mit exakt denselben fünf Zeilen ergab für die alten Klauseln
`payee = @p COLLATE NOCASE` → 1 (Suchtext klein) und 2 (Suchtext groß), `memo LIKE '%grüne%'
COLLATE NOCASE` → 1, also punktgenau die im Kriterium vorhergesagten Ist-Werte. Mit `bb_lower`
liefern dieselben Abfragen 3/3/3. Zusätzlich hat er die **einseitige** Faltung nachgestellt (nur
die Spalte gefaltet, Parameter roh): Ergebnis 0. Die Zusicherung auf den großgeschriebenen
Suchtext ist damit nachweislich falsifizierend und fängt genau die Lösung ab, die nur eine Seite
faltet. Der laute Ausfallmodus aus ADR 0037 ist ebenfalls nachgestellt worden
(`SQLite Error 1: 'no such function: bb_lower'` auf einer Connection ohne Registrierung), womit
die Registrierung vor `Open()` auch empirisch belegt ist. `dotnet test` im Worktree: Exit 0,
941 erfolgreich, 7 übersprungen (`.env`-gebundene Comdirect-/YNAB-Integrationstests, vorbestehend),
0 Fehler. Die drei Replay-Pflichttests (`src/Tests/LedgerTests.fs:912, :938, :991`) laufen mit.
Die `pageEntryId`-Änderung hat er für alle 100 Werte nachgerechnet statt den Kommentar zu
glauben, und geprüft, dass kein Test eine Entry-Guid als Literal festnagelt.

**Vom Conductor auf dem Hauptbaum nachgezogen** (der Worker darf unter `.agentheim/` nicht
schreiben): der Eintrag **Such-Filter (Kassenbuch)** im `ledger`-README nennt jetzt die Faltung
über `bb_lower` und ihre Grenze; der Neutral-Abschnitt von ADR 0036 trägt einen Nachtrag, der
seine `COLLATE NOCASE`-Aussage als mit ADR 0037 entfallen kennzeichnet; ADR 0037 hat einen
Eintrag im `adr-local`-Block des ledger-INDEX bekommen, der bisher fehlte.
