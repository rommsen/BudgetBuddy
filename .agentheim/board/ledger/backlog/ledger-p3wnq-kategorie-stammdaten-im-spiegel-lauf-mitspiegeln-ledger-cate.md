---
id: ledger-p3wnq
title: "Kategorie-Stammdaten im Spiegel-Lauf mitspiegeln (ledger_categories)"
status: backlog
type: feature
context: ledger
created: 2026-09-20
completed:
depends_on: []
blocks: [ledger-h6ptn]
tags: [backend, kassenbuch, spiegel, persistence]
related_adrs: [0013, 0014, 0018, 0030, 0039]
related_research: []
prior_art: [ledger-t7v2q]
---

## Why

Die Kassenbuch-Liste soll die Kategorie am PC voll qualifiziert zeigen ("Alltag:
Lebensmittel", Entwurf `docs/ux/kassenbuch/handoff-ablauf-1-durchsehen-und-pruefen.md`,
1.9 Zeile 260). Die Kategoriegruppe steht nur an `YnabCategory.GroupName`
(`src/Shared/Domain.fs:429`), befüllt aus YNABs `/categories` bzw. den `category_groups`
in `/budgets/{id}` (`src/Server/YnabClient.fs:55-78`, `243-249`). YNABs
**Transaktions**-JSON trägt sie nicht: `observationDecoder`
(`src/Server/YnabObservations.fs:170-194`) liest `category_id` und `category_name`, mehr
gibt es dort nicht.

ADR 0039 hat entschieden, dass die Gruppe nicht ins Ereignis kommt, sondern im Lesepfad
aufgelöst wird, und zwar gegen gespiegelte Stammdaten in SQLite, damit der
Kassenbuch-Lesepfad netzfrei bleibt. Dieses Item legt diese Stammdaten an. Es ist die
Vorarbeit für `ledger-h6ptn` und für sich allein noch nicht sichtbar.

Muster ist `ledger_accounts` (`src/Server/Ledger/Persistence.fs:106`): YNAB-Stammdaten
werden schon heute in eine eigene Tabelle gespiegelt, nicht in Ereignisse geschrieben.

## What

### 1. Tabelle und Persistence

- Neue Tabelle im bestehenden Schema-Init von `src/Server/Ledger/Persistence.fs` (die
  `CREATE TABLE IF NOT EXISTS`-Blöcke stehen ab Zeile 106):

  ```sql
  CREATE TABLE IF NOT EXISTS ledger_categories (
      ynab_category_id TEXT PRIMARY KEY,
      name             TEXT NOT NULL,
      group_name       TEXT NOT NULL,
      updated_at       TEXT NOT NULL
  )
  ```

  `ledger_entries` bekommt **keine** neue Spalte. Keine SQLite-Spezialitäten, kein
  `json_extract` (ADR 0013 Punkt 8).
- Upsert-Funktion für eine `YnabCategory list`, nach dem Muster des
  `ON CONFLICT(id) DO UPDATE SET` für `ledger_accounts` (`Persistence.fs:331`). Ein
  geänderter Gruppenname überschreibt, es entsteht keine zweite Zeile.
- **Nur Upsert, nie Löschen.** Kategorien, die YNAB nicht mehr liefert (in YNAB gelöscht),
  bleiben mit ihrem letzten bekannten Gruppennamen stehen. Das ist Absicht: eine Buchung,
  deren Kategorie inzwischen gelöscht ist, soll ihre Beschriftung behalten statt sie zu
  verlieren. Ein `DELETE` der nicht mehr gelieferten Zeilen wäre die schlechtere Variante,
  weil er alte Buchungen rückwirkend entqualifiziert. Die Tabelle ist damit eine
  wachsende Sammlung aller je gesehenen Kategorien, nicht ein exaktes Abbild des heutigen
  Katalogs; für den Zweck (Beschriftung nachschlagen) ist genau das richtig.
- Ladefunktion, die die Zuordnung als `Map<Guid, string>` (Kategorie-Id auf Gruppenname)
  liefert. Sie ist das, was `ledger-h6ptn` im Lesepfad benutzt; hier wird sie nur angelegt
  und getestet.
- Zeitpunkte als exakter ISO-Text wie überall im `ledger`, nie als SQLite-Datumstyp.

### 2. Abruf im Spiegel-Lauf

- `MirrorFetchers` (`src/Server/Ledger/CommandHandlers.fs:31-38`) bekommt ein drittes Feld
  `FetchCategories: unit -> Async<YnabResult<YnabCategory list>>`.
- Verdrahtet in `mirrorYnabBudget` mit `Server.YnabClient.getCategories token budgetId`.
  Die Funktion existiert bereits (`src/Server/YnabClient.fs:226`) und flacht die
  `category_groups` schon auf `YnabCategory.GroupName` aus. Bewusst **ohne**
  `applyReadyToAssign`: dieses Item braucht nur Id, Name und Gruppe, nicht den
  Available-Betrag.
- **Voller Abruf je Lauf, kein Delta.** `getCategories` nimmt kein
  `last_knowledge_of_server` entgegen (Signatur `token -> YnabBudgetId -> ...`) und holt
  `/budgets/{id}/categories` immer vollständig. Das ist dieselbe Entscheidung, die ADR
  0018 für die Konten getroffen hat, und sie passt zur Upsert-Logik: ein Vollabruf plus
  Upsert ist selbstheilend, ein Delta wäre es nur mit zusätzlichem Gedächtnis. Der
  Katalog hat die Größenordnung einiger Dutzend Kategorien, das kostet nichts.
- Aufruf in `runMirror` **nach** der Beobachtungsschleife und **nach**
  `saveServerKnowledge`, so gekapselt, dass weder ein `Error` noch eine Ausnahme aus diesem
  Abruf den Lauf auf `MirrorRunFailed` zieht (ADR 0039 Punkt 4). Die Reihenfolge ist nicht
  kosmetisch: stünde der Abruf vorn und bekäme ein 429, scheiterte ein Lauf an etwas, das
  er für seine eigentliche Arbeit nicht braucht.
- Der Abruf zählt in **keinen** der Lauf-Zähler (`newEntries`, `changedEntries`,
  `deletedEntries`). Er beschreibt keine Änderung an einer Buchung.

### 3. Test-Fixtures aufräumen

Das dritte Feld in `MirrorFetchers` zwingt alle Lauf-Fixtures an. Dabei einen
Default-Record für `MirrorFetchers` einziehen, damit das nächste Feld eine Zeile kostet
statt zwölf. Betroffen: `src/Tests/LedgerMirrorTests.fs` ab Zeile 510 sowie 918, 1133,
1167 (Zeilenangaben vor der Änderung, per Grep nachprüfen).

## Acceptance criteria

- [ ] `src/Server/Ledger/Events.fs` ist unverändert: kein neues Feld in `CategoryRef`,
      keine neue Schema-Version, kein neuer Zweig in `decodePayload`. Per Diff belegt.
- [ ] Die Tabelle `ledger_categories` wird vom Schema-Init angelegt, auch auf einer
      bestehenden Datenbank, die sie noch nicht hat (`CREATE TABLE IF NOT EXISTS`).
      Belegt durch einen Test gegen In-Memory-SQLite (ADR 0030).
- [ ] Ein Upsert schreibt eine Liste von Kategorien; ein zweiter Upsert derselben
      `ynab_category_id` mit geändertem `group_name` überschreibt den Wert und lässt die
      Zeilenzahl der Tabelle unverändert.
- [ ] Ein Upsert mit einer Liste, die eine zuvor gespeicherte Kategorie **nicht** mehr
      enthält (in YNAB gelöscht), lässt deren Zeile unverändert stehen: die Funktion
      löscht nie.
- [ ] Die Ladefunktion liefert für eine gefüllte Tabelle eine `Map` mit genau den
      gespeicherten Kategorie-Ids, und für eine leere Tabelle eine leere `Map` statt eines
      Fehlers.
- [ ] Ein Spiegel-Lauf mit erfolgreichem `FetchCategories` füllt `ledger_categories` mit
      allen gelieferten Kategorien.
- [ ] Ein Spiegel-Lauf, dessen `FetchCategories` `Error` liefert, endet trotzdem `Ok` und
      hat die Buchungen des Laufs geschrieben; `ledger_categories` behält ihren vorherigen
      Stand (also nicht leer, wenn vorher etwas drinstand).
- [ ] Dasselbe, wenn `FetchCategories` eine Ausnahme wirft statt `Error` zu liefern.
- [ ] Ein Lauf ohne Buchungsänderungen meldet dieselben Werte in `newEntries`,
      `changedEntries` und `deletedEntries` wie vor dieser Änderung: der Kategorie-Abruf
      verändert keinen Zähler.
- [ ] Ein zweiter Lauf über dieselben Daten schreibt kein zusätzliches Ereignis und keine
      neue laufende Nummer (die bestehende Idempotenz-Zusage des Laufs bleibt unberührt).
- [ ] Alle Tests gegen In-Memory-SQLite, nie gegen die Entwicklungsdatenbank (ADR 0030).
- [ ] `dotnet build` und `dotnet test` grün.

## Notes

`YnabClient.getCategories` liefert heute auch die interne Gruppe
`"Internal Master Category"` mit der Kategorie `"Inflow: Ready to Assign"`
(`src/Shared/Domain.fs:442`, `456`). Sie landet mit in der Tabelle; das ist unschädlich,
weil die Buchungen im Kassenbuch sie nicht als Kategorie tragen. Kein Filtern, damit die
Tabelle einfach das spiegelt, was YNAB liefert.

`YnabCategory` hat heute kein `Hidden`-Feld. Versteckte Kategorien landen deshalb ohne
Kennzeichen in der Tabelle. Für die Gruppenbeschriftung ist das egal; wer das Kennzeichen
braucht (`ledger-j8xq4` klappt versteckte Kategorien im Picker ein), muss `YnabCategory`
erweitern, und das gehört dorthin, nicht hierher.

Nach einem frischen Deploy ohne gelaufenen Spiegel ist die Tabelle leer. Das ist der
vorgesehene Zustand, nicht ein Fehler: `ledger-h6ptn` fällt dann auf den Blattnamen zurück.

Offen für später, ausdrücklich nicht hier zu entscheiden: ob der Kategorie-Abruf ein
Frische-Kriterium bekommt (für jetzt: jeder Lauf, eine Mechanik statt zwei, ADR 0039 Punkt
6).

**Was diese Tabelle ausdrücklich nicht ist:** eine Quelle für Kategorie-Picker. Weil sie
nie löscht, enthält sie auch in YNAB gelöschte Kategorien. Ein Picker, der daraus
gespeist würde, böte Roman Kategorien an, die es nicht mehr gibt. Die
`YnabClient.getCategories`-Aufrufe in `src/Server/Api.fs` bleiben deshalb, wo sie sind.
