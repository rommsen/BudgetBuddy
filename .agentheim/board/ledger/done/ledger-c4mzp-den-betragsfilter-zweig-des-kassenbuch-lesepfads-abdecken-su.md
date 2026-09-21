---
id: ledger-c4mzp
title: "Den Betragsfilter-Zweig des Kassenbuch-Lesepfads abdecken: Summe, Gelöschten-Zahl, Payee-Vorschläge, Blättern, einseitige Grenzen"
status: done
type: chore
context: ledger
created: 2026-09-20
completed:
depends_on: []
blocks: []
tags: [tests, kassenbuch, filter, abdeckung]
related_adrs: [0017, 0030, 0032, 0033, 0036]
related_research: []
prior_art: [ledger-vgfw3]
---

## Why

Lücken 1, 2 und 6 aus dem QA-Review nach dem Work-Lauf vom 2026-09-19 (erfasst als
`ledger-r7dm2`).

`src/Server/Ledger/Persistence.fs` gabelt fünf Lese-Funktionen über `hasAmountFilter`
(Z. 580) in zwei Implementierungen: einen SQL-Pfad und einen F#-Nachfilter-Pfad über
`loadAllMatching` (Z. 673) plus `amountMatches` (Z. 586). Betroffen sind
`loadEntriesPage`, `countEntries`, `countDeletedEntries`, `loadAmountsForQuery` und
`loadPayeeSuggestions`. Geprüft wird davon heute nur `TotalCount` (AC6 aus
`ledger-vgfw3`). Ungeprüft: `TotalAmount`, `DeletedCount`, das Blättern über `safePage`
(Z. 683) und die Payee-Vorschläge, die im F#-Pfad über `List.countBy id` ohne Tiebreak
gruppieren statt über `GROUP BY payee ORDER BY cnt DESC`. Fünf Funktionen, je zwei
Implementierungen, eine davon getestet.

`amountMatches` prüft beide Enden unabhängig über `Option.forall`; der einzige
bestehende Test setzt `AmountMin = AmountMax = -4.50m`. Eine Umsetzung, die beide Enden
verlangt oder die Vergleiche vertauscht, käme durch. Bei überwiegend negativen Beträgen
ist die Min/Max-Richtung leicht zu verdrehen.

`countNeedsReviewEntries` ignoriert die fünf Such-Filter bewusst (der Chip „Zu prüfen
(n)" ist ein fester Zähler über das Konto), `countDeletedEntries` wendet sie bewusst an.
Nur die `DeletedCount`-Hälfte ist getestet (AC8). Ein späteres „Vereinheitlichen" käme
durch.

## What

Testfälle in `src/Tests/LedgerPageTests.fs`, in der bestehenden Liste
`ledgerPagingTests`, nach dem dortigen Muster: eigenes Konto je Testfall, Aufbau in der
`testCase`-Closure (ADR 0033), In-Memory-DB über `test.runsettings` (ADR 0030), Abfragen
über `baseQuery ... with`. Kein Produktionscode.

Die sechs neuen Konten sind `0195bb00-0000-7000-8000-000000000019` bis `...001e`
(P bis U, im Anschluss an `pageTestAccountO`). Sie müssen in `seedAccounts ()` ergänzt
werden.

Nebenher, ohne neue Abdeckung: `pageEntryId` erzeugt heute
`0195bb01-0000-7000-8000-0000000000{n:D2}` und trägt damit nur `n < 100`; belegt sind
bereits 20 bis 93. Auf `...000000000{n:D3}` erweitern (dieselbe Gesamtlänge), damit die
neuen Tests bei 100 beginnen können. **Dieser Task besitzt diese Änderung**, siehe Notes.

Außerdem, ohne neue Abdeckung: die zehn vorbestehenden, vollständig ausgeschriebenen
`LedgerEntryPageQuery`-Literale (Z. 1174, 1202, 1244, 1265, 1284, 1288, 1307, 1333, 1350,
1354) auf `baseQuery` umstellen, das derselbe Commit genau dafür eingeführt hat. Für die
Payee-Vorschläge analog ein `baseSuggestionQuery : Guid option -> LedgerPayeeSuggestionQuery`
einführen und das Literal ab Z. 1572 darauf umstellen.

## Acceptance criteria

- [ ] **TotalAmount im Betrags-Zweig.** Ein Testfall in `src/Tests/LedgerPageTests.fs`
      sät auf Konto P die Beträge -5,00 / -50,00 / -500,00 / +20,00 EUR und ruft
      `getLedgerEntriesPage { baseQuery (Some rawP) with PageSize = 1; AmountMin = Some -100m; AmountMax = Some 0m }`.
      Erwartet: `TotalCount = 2`, `TotalAmount = { Amount = -55m; Currency = "EUR" }`,
      `List.length Entries = 1`. Fängt eine Umsetzung, die `loadAmountsForQuery` im
      Betrags-Zweig ungefiltert lässt (ergäbe -535,00) und eine, die nur die geladene
      Seite summiert (ergäbe -5,00).
- [ ] **DeletedCount im Betrags-Zweig.** Ein Testfall sät auf Konto Q -10,00 EUR aktiv
      (`seedEntryFull`), -20,00 EUR in YNAB gelöscht und -900,00 EUR in YNAB gelöscht
      (`seedDeletedEntryFull`) und ruft
      `getLedgerEntriesPage { baseQuery (Some rawQ) with AmountMin = Some -100m; AmountMax = Some -1m }`.
      Erwartet: `TotalCount = 1`, `TotalAmount = -10,00 EUR`, `DeletedCount = 1`. Fängt
      eine Umsetzung, die `DeletedCount` im Betrags-Zweig ungefiltert zählt (ergäbe 2)
      und eine, die dort `IncludeDeleted = false` erbt statt es wie der SQL-Zweig auf
      `true` zu setzen (ergäbe 0).
- [ ] **Payee-Vorschläge: beide Pfade liefern dasselbe.** Ein Testfall sät auf Konto R
      dreimal Payee `"Rewe"` (-1,00 / -2,00 / -300,00), zweimal `"Baecker"` (-3,00 /
      -4,00) und einmal `"Aldi"` (-500,00) und ruft `getLedgerPayeeSuggestions` zweimal:
      einmal ohne Betragsgrenzen (SQL-Pfad), einmal mit
      `AmountMin = Some -1000m; AmountMax = Some 0m`, also einem Intervall, das alle
      sechs einschließt (F#-Pfad). Erwartet: beide Ergebnisse, nach `Payee` sortiert,
      sind gleich und lauten `[ "Aldi", 1; "Baecker", 2; "Rewe", 3 ]`. Fängt einen
      F#-Pfad, der über die geladene Seite statt über den Treffer zählt, der
      `List.choose (fun e -> e.Payee)` vergisst, oder der nach einem anderen Feld
      gruppiert.
- [ ] **Payee-Vorschläge: Sortierrichtung.** Derselbe Testfall prüft für beide Aufrufe
      `List.head |> fst = "Rewe"` (die eindeutig größte Zahl steht vorn). Fängt ein
      `List.sortBy snd` statt `List.sortByDescending snd` im F#-Pfad. Die Reihenfolge
      bei Gleichstand wird bewusst nicht geprüft: `ORDER BY cnt DESC` hat im SQL-Pfad
      keinen Tiebreak, eine Zusicherung darüber wäre nicht erfüllbar.
- [ ] **Payee-Vorschläge: der Betragsfilter wirkt auf die Vorschläge.** Derselbe
      Testfall ruft ein drittes Mal mit `AmountMax = Some -250m` (ohne `AmountMin`) und
      erwartet genau `[ "Aldi", 1; "Rewe", 1 ]` nach Payee sortiert, insbesondere
      `List.tryFind (fst >> (=) "Baecker") = None`. Fängt einen F#-Pfad, der
      `amountMatches` vor dem Gruppieren auslässt.
- [ ] **Blättern im Betrags-Zweig.** Ein Testfall sät auf Konto S fünf Buchungen
      -1,00 bis -5,00 EUR mit aufsteigenden Daten und ruft
      `{ baseQuery (Some rawS) with PageSize = 2; AmountMin = Some -10m }` für
      `Page = 0, 1, 2, 3`. Erwartet: 2, 2, 1 und 0 Einträge; alle vier Antworten tragen
      `TotalCount = 5` und `TotalAmount = -15,00 EUR`; die Ids der Seiten 0 bis 2
      ergeben zusammen genau die fünf gesäten Ids, ohne Dublette. Seite 3 darf keine
      Ausnahme werfen. Fängt ein `safePage` ohne Offset-Wächter (`List.skip` über das
      Listenende wirft) und eines, das `List.truncate` vor `List.skip` anwendet (Seite 1
      lieferte dann dieselben zwei wie Seite 0).
- [ ] **Nur `AmountMin` gesetzt.** Ein Testfall sät auf Konto T -100,00 / -50,00 /
      -5,00 / +30,00 EUR und ruft `{ baseQuery (Some rawT) with AmountMin = Some -50m }`
      ohne `AmountMax`. Erwartet: `TotalCount = 3`, die Beträge der Einträge sind als
      sortierte Liste `[ -50m; -5m; 30m ]`, `-100m` ist nicht enthalten. Fängt eine
      Umsetzung, die beide Enden verlangt (ergäbe 0 Treffer) und eine, die `>=` und `<=`
      vertauscht (ergäbe nur -100,00).
- [ ] **Nur `AmountMax` gesetzt, Randwert eingeschlossen.** Derselbe Testfall ruft
      `{ baseQuery (Some rawT) with AmountMax = Some -50m }` ohne `AmountMin` und
      erwartet `TotalCount = 2` mit den Beträgen `[ -100m; -50m ]`. Der Randwert -50,00
      ist in beiden Richtungen enthalten, also `>=`/`<=`, nicht `>`/`<`.
- [ ] **Vertauschtes Intervall bleibt leer.** Derselbe Testfall ruft
      `{ baseQuery (Some rawT) with AmountMin = Some 0m; AmountMax = Some -100m }` und
      erwartet `TotalCount = 0`, `Entries = []`, `TotalAmount = { Amount = 0m; Currency = "EUR" }`.
      Fängt eine Umsetzung, die die Grenzen hilfsbereit tauscht, und eine, die die
      beiden Enden mit ODER statt UND verknüpft.
- [ ] **`NeedsReviewCount` bleibt von jedem Such-Filter unberührt.** Ein Testfall sät
      auf Konto U drei ungeprüfte Buchungen (`seedEntry`, `Cleared = Uncleared`) mit
      -10,00 / -20,00 / -3000,00 EUR und eine geprüfte (`seedReviewedEntry`) mit
      -40,00 EUR und ruft dreimal: `baseQuery (Some rawU)` ohne Filter,
      `{ ... with AmountMin = Some -100m }` (F#-Zweig, schließt die -3000,00 aus) und
      `{ ... with Payee = Some "payee 130" }` (SQL-Zweig, trifft genau eine). Erwartet:
      `TotalCount` ist 4, 3 und 1, `NeedsReviewCount` ist in allen drei Antworten 3.
      Fängt ein späteres „Vereinheitlichen", das den Filter an
      `countNeedsReviewEntries` durchreicht (ergäbe 3, 2 und 1).
- [ ] **Keine ausgeschriebenen Query-Literale mehr.** In
      `src/Tests/LedgerPageTests.fs` kommt der Text `AmountMax = None` genau zweimal
      vor: einmal in `baseQuery`, einmal in dem neu eingeführten
      `baseSuggestionQuery`. Alle übrigen Abfragen entstehen per `with` daraus.
- [ ] **Die neuen Konten sind gesät.** `seedAccounts ()` legt `pageTestAccountP` bis
      `pageTestAccountU` an; ohne das schlägt jeder der neuen Testfälle fehl.
- [ ] `dotnet build` und `dotnet test` grün. Die Zahl der bestandenen Tests steigt um
      die neuen Testfälle; kein bestehender Test wird entfernt oder abgeschwächt.

## Notes

**Herkunft der Testdefinitionen.** Der Elterntask `ledger-r7dm2` behauptet, der QA-Review
liege als Volltext im Diary-Eintrag vom 2026-09-19 20:14 und nenne je Test Datei, Eingabe
und erwartetes Ergebnis. Das stimmt nicht: dort steht ein Nachtrag von rund 15 Zeilen zum
Umlaut-Defekt und zu zwei korrigierten Zahlen, mehr nicht. Die Eingaben und
Erwartungswerte unten sind aus `src/Server/Ledger/Persistence.fs` und
`src/Server/Ledger/QueryHandlers.fs` abgeleitet, nicht abgeschrieben. Wer sie ändern
will, prüft gegen den Code, nicht gegen ein Protokoll, das es nicht gibt.

Alle DB-Testfälle laufen in `testSequenced` gegen die In-Memory-Datenbank (ADR 0030,
erzwungen über `src/Tests/test.runsettings`); Schema- und Kontenaufbau stehen in der
`testCase`-Closure, nie auf Modulebene (ADR 0033, mechanisch geprüft von
`ModuleLevelDbInitGuardTests.fs`).

**Dieser Task besitzt die Erweiterung von `pageEntryId` auf drei Stellen.** Parallel
verfeinert werden `ledger-k3wqn`, `ledger-h4qtx` und `ledger-xk9wq`, die dieselbe Datei
anfassen. Von den zweistelligen Nummern sind nur noch 94 bis 99 frei; wenn zwei Tasks
unabhängig voneinander die Stellenzahl erweitern, kollidieren sie. Dieser Task nimmt die
Nummern ab 100, die drei anderen bleiben bei 94 bis 99 oder laufen nach diesem.

Die Payee-Vorschlags-Kriterien sind bewusst so formuliert, dass sie unter `ledger-h4qtx`
(case-insensitive Gruppierung) grün bleiben: alle gesäten Payees stehen in genau einer
Schreibweise, die Zusicherung gilt der Gleichheit beider Pfade, nicht einer bestimmten
Gruppierungsregel. Läuft dieser Task vor `h4qtx`, bekommt `h4qtx` damit genau das
Sicherheitsnetz, das sein eigenes drittes Kriterium („gilt in beiden Pfaden gleich")
verlangt.

## Outcome

Der Betragsfilter-Zweig (`hasAmountFilter`) des Kassenbuch-Lesepfads war bis auf `TotalCount`
(AC6 aus `ledger-vgfw3`) ungetestet: fünf Lesefunktionen mit je zwei Implementierungen, eine
davon geprüft. Sechs neue Testfälle in `src/Tests/LedgerPageTests.fs` schließen die Lücke, je
einer pro Kriteriengruppe: `TotalAmount` (Konto Q), `DeletedCount` (R), Payee-Vorschläge samt
Sortierrichtung und Betragswirkung (S), Blättern über `safePage` (T), einseitig offene Grenzen
samt vertauschtem Intervall (U), Unberührtheit von `NeedsReviewCount` (V).

Nebenher, ohne neue Abdeckung: die zehn ausgeschriebenen `LedgerEntryPageQuery`-Literale sind
auf `{ baseQuery ... with ... }` umgestellt, und für die Payee-Vorschläge ist analog
`baseSuggestionQuery` eingeführt. `AmountMax = None` kommt dadurch genau zweimal im Text vor.

`dotnet test`: 969 erfolgreich, 7 übersprungen, 0 Fehler.

**Drei Vorgaben des Task-Texts waren beim Start überholt und wurden bewusst verschoben.** Die
Aufgabe wurde am 2026-09-20 verfeinert; ihre eigenen Notes sahen die Kollision mit
`ledger-k3wqn` voraus, nahmen aber an, dieser Task liefe zuerst. Tatsächlich lief `k3wqn` in
Welle 1 desselben Laufs. Gemessen und angewiesen vom Conductor vor dem Dispatch:

- `pageEntryId` war bereits dreistellig (`{n:D3}`), von `k3wqn`. Der Task beansprucht diese
  Änderung ausdrücklich für sich ("**Dieser Task besitzt diese Änderung**"); sie ist nicht
  erneut gemacht worden, und kein Kriterium hängt daran. Im Diff erscheint `pageEntryId` nur
  als Verwendung in einem neuen Test.
- `pageTestAccountP` (`...019`) war von `k3wqn` belegt. Die sechs neuen Konten heißen deshalb
  **Q bis V** (`...01a` bis `...01f`) statt P bis U, mit gleicher Zuordnung zu den Kriterien
  (P→Q, Q→R, R→S, S→T, T→U, U→V).
- Die laufenden Nummern 96 bis 100 waren belegt. Die neuen Buchungen beginnen bei **101** und
  laufen bis 126. Entsprechend nennt der `NeedsReviewCount`-Test `"payee 123"` statt des im
  Task-Text stehenden `"payee 130"`.

**Verifikation (Iteration 1, PASS), mit sechs Mutationen.** Der Verifier hat jede Zusicherung
gebrochen, die das Kriterium als falsifizierend beschreibt, und den gemeldeten Istwert gegen
den im Kriterium vorhergesagten gehalten:

| Mutation | Ergebnis |
|---|---|
| `loadAmountsForQuery` ohne `amountMatches` im F#-Zweig | rot: "Expected -55M, but got -535.00M", exakt der vorhergesagte Wert |
| `loadAllMatching` mit `filter` statt `deletedFilter` | rot, `DeletedCount` erwartet 1 |
| `List.sortBy snd` statt `List.sortByDescending snd` | rot: "Expected char 'R', but got 'A'" |
| `amountMatches` vor dem Gruppieren ausgelassen | rot, Baecker bleibt mit Count 2 im dritten Aufruf |
| `safePage` ohne Offset-Wächter | rot: `invalidArgOutOfRange` aus `List.skip` bei Seite 3 |
| `truncate` vor `skip`, und `>=`/`<=` vertauscht | rot: "Seite 1 trägt zwei weitere, nicht dieselben wie Seite 0" bzw. `TotalCount` erwartet 3 |

Die Mutationen belegen nebenbei auch die Zweig-Zuordnung: die F#-only-Mutation machte nur die
F#-Zusicherung des Payee-Tests rot, nicht die SQL-Zusicherung, womit gezeigt ist, dass der erste
Aufruf wirklich den SQL-Pfad nimmt. Und der Betrags-Aufruf des `NeedsReviewCount`-Tests wurde
von Mutation 2 getroffen, läuft also tatsächlich über den F#-Zweig.

**Die 29 gelöschten Zeilen sind kein entfernter Test.** Der Verifier hat das eigens geprüft:
keine der gelöschten Zeilen enthält `testCase`, `testSequenced` oder `Expect.`; die Zahl der
`testCase`-Deklarationen steigt von 19 auf 25, die der `Expect.`-Aufrufe von 219 auf 252. Die
zehn ersetzten Literale sind **feldweise** gegen `baseQuery` geprüft (alle zwölf Felder), jede
`with`-Form setzt genau die Felder, die das alte Literal abweichend trug, keine stille
Feldänderung. Das Vorschlags-Literal ist in allen neun Feldern identisch.

Weiter bestätigt: `seedAccounts ()` legt alle sechs neuen Konten an und steht innerhalb der
`testCase`-Closure (ADR 0033, `ModuleLevelDbInitGuardTests` läuft grün mit); die
Payee-Vorschlags-Tests sichern bewusst nichts über die Reihenfolge bei Gleichstand zu, weil
`ORDER BY cnt DESC` im SQL-Pfad keinen Tiebreak hat; der Diff fasst genau eine Datei an, kein
`Tests.fsproj`, nichts vom parallel laufenden Schwester-Task.

**Eine Änderung nach der Verifikation, deklariert.** Der Verifier hat in einer neuen
Kommentarzeile "ausgewaehlt" statt "ausgewählt" gefunden und als nicht verdikt-relevant
eingestuft. Der Conductor hat das eine Wort beim Integrieren korrigiert; der integrierte Diff
weicht damit um dieses Wort in einem Kommentar vom verifizierten ab, an keiner Prüfung hängt
etwas davon. Die beiden gleichlautenden Stellen in Zeile 408 und 411 sind Altbestand und
unberührt geblieben.
