---
id: ledger-t7v2q
title: "Übertrag: die beiden Buchungen miteinander verknüpfen"
status: done
type: feature
context: ledger
created: 2026-09-06
completed:
depends_on: [ledger-m8r2v]
blocks: []
tags: [ledger, spiegel, uebertrag]
related_adrs: [0013, 0015]
related_research: []
prior_art: [ledger-e3k7p]
---

## Why
ADR 0015 sagt: ein Übertrag entsteht im Kassenbuch als **zwei** Buchungen, und "die
Verbindung zwischen ihnen wird als eigene Verknüpfung mitgeführt (Buchung-zu-Buchung
statt Buchung-zu-Fremdsystem)". Das Fundament (`ledger-e3k7p`) hat das Feld angelegt
(`Entry.TransferEntryId`, Spalte `ledger_entries.transfer_entry_id`), füllt es aber nie.

Grund: `EntryObservedInYnab` trägt laut verbindlichem Ereignis-Katalog YNABs
`transfer_account_id` und `transfer_transaction_id`, also Fremd-Ids. Die Übersetzung in
die eigene Buchungs-Id der Gegenseite braucht einen Lookup über die
Fremd-Id-Verknüpfungen, und die Gegenbuchung ist zum Zeitpunkt der ersten Beobachtung
oft noch gar nicht angelegt. `apply` ist rein und kann das nicht.

Solange das offen ist, zeigt das Kassenbuch einen Übertrag als zwei unverbundene
Buchungen. Für v1 ist das laut ADR 0015 hinnehmbar ("Die Sicht 'ein Übertrag ist eine
Bewegung' ist v2-Arbeit"), aber die Verknüpfung selbst ist ausdrücklich v1.

## What

### Modelliert am 2026-09-19: Beobachtungs-Familie

Die Frage nach der Familie war offen und ist entschieden. Das Ereignis gehört zu den
**Beobachtungen**, Actor `YnabMirror`, Name `EntryTransferCounterpartObservedInYnab`.

Begründung, ausdrücklich gegen die Entscheidungs-Familie abgewogen: Festgestellt wird die
Tatsache "diese zwei Buchungen sind eine Bewegung" von YNAB, nicht von BB. BBs Lookup
über `findEntryByLink` übersetzt nur eine Fremd-Id in die eigene Buchungs-Id, genauso wie
der Spiegel heute schon YNAB-Id und ImportId auflöst, ohne dass daraus eine Entscheidung
würde. Ausschlaggebend ist ADR 0013: **Abweichung** ist dort definiert als "letzte
Entscheidung widerspricht letzter Beobachtung". Trüge die Verknüpfung das
Entscheidungs-Etikett, stünde im Log eine Festlegung, die Roman nie getroffen hat, und
ein späteres Auflösen des Übertrags in YNAB sähe wie eine Abweichung aus, obwohl keine
besteht. Dass die Lookup-Mechanik außerhalb von `apply` sitzt, spricht nicht dagegen: das
gilt für jede Beobachtung, auch `observe` bekommt seinen Vergleichszustand von außen.

Ein eigenes Ereignis ist nötig (statt einer stillen Verknüpfungszeile wie bei
`EntryLinkedToYnab`), weil `TransferEntryId` ein Feld auf `Entry` ist, also Teil des
Zustandsmodells. Zustand entsteht nach ADR 0013 ausschließlich aus dem Log, sonst
reproduziert `replayAll` die Verknüpfung nicht. `EntryLinkedToYnab` kommt ohne
`apply`-Wirkung aus, weil die Fremd-Id in einer eigenen Tabelle lebt; hier ist das anders.

### 1. Katalog-Eintrag im `ledger/README.md`

Wortlaut steht fest, zwei Stellen. Als letzter Punkt der Liste `**Beobachtungen**`:

```markdown
- `EntryTransferCounterpartObservedInYnab`: die Gegenbuchung eines Übertrags ist gefunden.
  YNAB führt beide Seiten über `transfer_transaction_id`, der Spiegel hat diese Fremd-Id
  über `findEntryByLink` (System `ynab`) auf die eigene Buchungs-Id aufgelöst (ADR 0015).
  Payload: Buchungs-Id der Gegenbuchung und die YNAB-Transaction-Id, über die aufgelöst
  wurde (sie macht die Historie-Zeile nachprüfbar, ohne die Gegenbuchung zu laden). Setzt
  `Entry.TransferEntryId`. Die Verknüpfung ist wechselseitig, der Umschlag trägt aber
  genau eine Buchungs-Id: deshalb **zwei** Ereignisse, eins je Seite, mit demselben
  Zeitpunkt und in einem `appendAndApply`-Aufruf, also einer Transaktion.
```

Als Absatz hinter dem bestehenden Absatz `**Verknüpfung:**`:

```markdown
**Übertrags-Verknüpfung:** Die Buchung-zu-Buchung-Verknüpfung aus ADR 0015 entsteht nicht
beim Entstehungs-Ereignis. `EntryObservedInYnab` trägt nur YNABs Fremd-Ids
(`TransferAccountId`, `TransferYnabTransactionId`), und die Gegenbuchung ist beim ersten
Sehen oft noch nicht angelegt. Aufgelöst wird sie im Spiegel-Lauf, nachdem die
Beobachtung geschrieben ist: findet der Lookup die Gegenseite, schreibt der Lauf
`EntryTransferCounterpartObservedInYnab` für beide Buchungen, sonst nichts. Zeigt eine
Seite bereits auf die andere, entsteht kein weiteres Ereignis (Idempotenz wie bei allen
Beobachtungen: unveränderte Daten ergeben kein Ereignis). `apply` selbst setzt das Feld
ohne Rückfrage auf den Wert der Payload: eine spätere Umverknüpfung ist in YNAB möglich
und darf keinen Lauf abbrechen. Laut abgelehnt wird nur die Verknüpfung einer Buchung mit
sich selbst, die in keinem gültigen Log vorkommt. Das Aufheben eines Übertrags in YNAB ist
heute nicht modelliert: die Verknüpfung bleibt dann stehen, bis die zusammengefasste
Übertrags-Sicht in v2 kommt.
```

### 2. `src/Server/Ledger/Events.fs`

Payload-Record zwischen die Beobachtungs-Payloads, nach
`EntryFlagChangeObservedInYnabPayload`:

```fsharp
/// Die Gegenbuchung eines Uebertrags ist gefunden. YNAB fuehrt beide Seiten ueber
/// `transfer_transaction_id`; der Spiegel loest diese Fremd-Id auf die eigene
/// Buchungs-Id auf (ADR 0015). Wechselseitig, aber je Seite ein eigenes Ereignis:
/// der Umschlag traegt genau eine Buchungs-Id.
type EntryTransferCounterpartObservedInYnabPayload = {
    /// Die Kassenbuch-Id der Gegenbuchung.
    CounterpartEntryId: EntryId
    /// YNABs `transfer_transaction_id`, ueber die aufgeloest wurde.
    CounterpartYnabTransactionId: string
}
```

Dazu der DU-Fall in `LedgerEventBody` (hinter `EntryFlagChangeObservedInYnab`, vor
`EntryDeletionObservedInYnab`), `eventTypeName` mit demselben String, `schemaVersion` = 1,
ein Codec-Paar nach dem Muster von `encodeLinkedToYnab`/`decodeLinkedToYnab` (Feldnamen
`counterpartEntryId` und `counterpartYnabTransactionId`), der Zweig in `encodePayload` und
der Fall `"EntryTransferCounterpartObservedInYnab", 1` in `decodePayload`. In
`Ledger.Domain.linksFrom` **keine** Änderung: es entsteht keine Fremd-Id.

### 3. Behandlung in `apply`

```fsharp
| EntryTransferCounterpartObservedInYnab p ->
    let e = requireExisting ()
    if p.CounterpartEntryId = event.EntryId then
        let (EntryId id) = event.EntryId
        invalidOp $"Uebertrags-Verknuepfung fuer Buchung {id} zeigt auf sich selbst"
    { e with TransferEntryId = Some p.CounterpartEntryId }
```

Die Regelverteilung ist bewusst anders als beim zweiten Entstehungs-Ereignis:

- **Kein Entstehungs-Ereignis**, also `requireExisting ()`. Trifft es auf eine unbekannte
  Buchung, ist das Log kaputt und `apply` schlägt laut fehl, wie bei allen
  Änderungs-Ereignissen.
- **Eine abweichende bestehende Verknüpfung wird nicht abgelehnt**, das letzte Ereignis
  gewinnt. YNAB kann einen Übertrag aufheben und neu verbinden; ein lautes `invalidOp`
  risse hier den ganzen Spiegel-Lauf an einem fachlich erlaubten Vorgang mit. Beim
  zweiten Entstehungs-Ereignis bleibt die harte Regel, wo sie steht: das ist unter keinen
  Umständen erklärbar.
- **Selbstverknüpfung wird laut abgelehnt.** Sie kann nur aus einem Lookup-Fehler stammen.
- **Idempotenz sitzt im Erzeuger, nicht in `apply`.** Wie `observe` bei unveränderten
  Daten kein Ereignis liefert, liefert der Verknüpfungs-Schritt keins, wenn das Feld schon
  auf dieselbe Buchung zeigt. `apply` bleibt eine reine Faltung ohne Sonderwissen über
  Wiederholungen.

### 4. Erzeugung im Spiegel-Lauf

In `Ledger.CommandHandlers.runMirror`, in derselben `for obs in batch.Observations`-Schleife,
direkt nach `do! Ledger.Persistence.appendAndApply events` und vor dem Hochzählen der
Zähler. Als eigene Funktion herausziehen (etwa `linkTransferCounterpart now entryId obs`),
damit `runMirror` nicht weiter wächst. Bedingungen, alle erfüllt:

1. `obs.Deleted = false`.
2. `obs.TransferYnabTransactionId = Some tid`.
3. `findEntryByLink YnabSystem tid` liefert `Some counterpartId`. Ohne Treffer passiert
   nichts; das ist der Normalfall, kein Fehler.
4. Beide Zustände laden und je Seite nur dann ein Ereignis bauen, wenn deren
   `TransferEntryId` noch nicht auf die jeweils andere zeigt. Damit heilt auch der
   halbseitige Fall aus einem früher abgebrochenen Lauf.
5. Die entstehenden ein oder zwei Ereignisse gehen in **einen** `appendAndApply`-Aufruf.
   Die Funktion führt `states` und `sequences` als `Map<Guid, _>` und verarbeitet
   Ereignisse mehrerer Buchungen in einer Transaktion.

Liegen beide Beobachtungen im selben Batch, genügt ein Lauf: beim ersten findet der Lookup
die Gegenseite noch nicht, beim zweiten beide.

**Lauf-Status:** das Verknüpfungs-Ereignis zählt **nicht** als `changedEntries`. Der
Zähler beschreibt, was sich in YNAB an den Buchungen geändert hat; die Verknüpfung ist
BBs Nachtrag zu bereits gezählten Beobachtungen und ließe die Zahlen sonst doppelt
aussehen.

## Acceptance criteria
- [ ] Der Ereignis-Katalog im `ledger/README.md` nennt `EntryTransferCounterpartObservedInYnab`
      mit Payload, an beiden oben genannten Stellen.
- [ ] `apply` auf eine bestehende Buchung A mit
      `EntryTransferCounterpartObservedInYnab { CounterpartEntryId = B; ... }` liefert
      einen Zustand mit `TransferEntryId = Some B`; alle übrigen Felder von A sind
      unverändert, Feld für Feld geprüft.
- [ ] `apply` wirft, wenn `CounterpartEntryId` gleich der Buchungs-Id des Umschlags ist,
      und ebenso, wenn das Ereignis ohne vorangehendes Entstehungs-Ereignis auf eine
      unbekannte Buchung trifft.
- [ ] Ein zweites Ereignis mit einer **anderen** Gegenbuchung C überschreibt
      `TransferEntryId` auf `Some C`, ohne zu werfen.
- [ ] Ein Spiegel-Lauf über einen Batch mit zwei Beobachtungen, die YNAB wechselseitig
      über `transfer_transaction_id` verbindet, hinterlässt `TransferEntryId = Some B` auf
      A und `Some A` auf B, und im Log steht je Buchung genau **ein**
      `EntryTransferCounterpartObservedInYnab` mit Actor `YnabMirror`.
- [ ] Ein zweiter Lauf über dieselben Daten lässt die Ereignisanzahl beider Buchungen
      unverändert: kein weiteres Verknüpfungs-Ereignis, keine neue laufende Nummer.
- [ ] Kommt die Gegenseite erst im zweiten Lauf: nach Lauf 1 ist `TransferEntryId` von A
      `None` und es existiert kein Verknüpfungs-Ereignis; nach Lauf 2 tragen beide Seiten
      die Verknüpfung.
- [ ] `replayAll` reproduziert dieselbe `TransferEntryId` wie die Zustandstabelle
      (Pflichttest aus ADR 0013), und `encodePayload`/`decodePayload` ist in Version 1 für
      die Payload verlustfrei.
- [ ] Tests gegen In-Memory-SQLite, nie gegen die Entwicklungsdatenbank (ADR 0030).
- [ ] `dotnet build` und `dotnet test` grün.

## Notes
Vorarbeit siehe `ledger-e3k7p`: Feld, Spalte und Fremd-Id-Lookup
(`Ledger.Persistence.findEntryByLink`) stehen bereits, es fehlt nur das Ereignis und
sein Auslöser. Modelliert am 2026-09-19 vom tactical-modeler, auf Romans ausdrückliche
Delegation der Familienfrage.

### Randfälle, die sonst auf die Füße fallen
- **Gegenseite nicht auflösbar.** Existiert für die Gegenseite keine Buchung im
  Kassenbuch (nie beobachtetes Konto), findet der Lookup nichts und es passiert nichts:
  keine Fehlermeldung, kein Abbruch. Der bestehende harte Fehler "zeigt auf das YNAB-Konto
  X, das der Konten-Abruf nicht kennt" bleibt unberührt, er greift eine Ebene früher.
- **Gegenseite in YNAB gelöscht.** Ist sie bereits als gelöscht beobachtet, wird trotzdem
  verknüpft, sie steht ja im Kassenbuch. Wird sie später gelöscht, bleibt die Verknüpfung
  stehen und zeigt auf eine ausgeblendete Buchung. Der Lesepfad darf die Gegenbuchung
  nicht als garantiert sichtbar annehmen.
- **Übertrag in YNAB aufgehoben.** `transfer_transaction_id` verschwindet, die Verknüpfung
  bleibt stehen. Bewusst nicht für v1 modelliert. Wenn es stört, kommt ein eigenes
  Auflösungs-Ereignis, nicht ein optionales Feld in dieser Payload, sonst verwässert der
  häufige Fall.

### Offene Punkte, die der Umsetzende klären muss
Der Modellierer hat sie ausdrücklich als ungeprüft benannt, statt zu raten:

1. **Codec-Form für `EntryId`.** Ob in `Events.fs` bereits ein Helfer für `EntryId`
   existiert oder ob `Encode.guid` auf dem entpackten Wert die übliche Form ist, ist an
   einer Nachbarstelle abzugleichen.
2. **Übertrag als Split-Zeile.** YNAB kann einen Übertrag in einer Subtransaktion führen.
   Dann hängt `transfer_transaction_id` an der Split-Zeile, nicht an der Buchung, und
   dieser Entwurf greift dort nicht. Ob `YnabObservations` das überhaupt mitführt, ist
   ungeprüft. Falls ja, braucht es entweder eine Split-Zeilen-Zuordnung oder eine bewusste
   Beschränkung auf Überträge auf Buchungsebene. Das ist eine eigene Modellierungsfrage
   und **nicht** in diesem Ereignis zu verstecken: in dem Fall zurück ins Backlog melden.
3. **Historien-Satz.** Der Katalog verlangt einen sprechenden Satz je Ereignis ohne
   Nachschlagen. Mit dieser Payload lautet er etwa "Als Übertrag mit einer anderen Buchung
   verbunden", ohne den Kontonamen der Gegenseite. Ob Roman den Namen dort sehen will
   (dann gehört er in die Payload), ist offen.

## Outcome

`EntryTransferCounterpartObservedInYnab` (Beobachtungs-Familie, Actor `YnabMirror`) verknüpft die beiden Buchungen eines Übertrags über `Entry.TransferEntryId`, wie ADR 0015 vorsieht und die Modellierung vom 2026-09-19 entschieden hat (Task-Text, Abschnitt "Modelliert am 2026-09-19").

**Code:**
- `src/Server/Ledger/Events.fs`: Payload `EntryTransferCounterpartObservedInYnabPayload` (`CounterpartEntryId`, `CounterpartYnabTransactionId`), DU-Fall in `LedgerEventBody`, `eventTypeName`/`schemaVersion` (= 1), Codec-Paar (`encodeTransferCounterpart`/`decodeTransferCounterpart`, Felder `counterpartEntryId`/`counterpartYnabTransactionId`), `encodePayload`/`decodePayload`-Zweige. Offener Punkt 1 (Codec-Form für `EntryId`) gelöst: ein `decodeEntryId`-Helfer ergänzt den bereits vorhandenen, bisher ungenutzten `encodeEntryId`, nach demselben Muster wie `decodeAccountId`.
- `src/Server/Ledger/Domain.fs`: `apply`-Fall setzt `TransferEntryId`, verlangt eine bestehende Buchung (`requireExisting`), lehnt nur die Selbstverknüpfung laut ab (`invalidOp`); eine abweichende bestehende Verknüpfung wird überschrieben, nicht abgelehnt (YNAB kann einen Übertrag aufheben und neu verbinden). Idempotenz sitzt bewusst nicht hier.
- `src/Server/Ledger/CommandHandlers.fs`: neue private Funktion `linkTransferCounterpart`, aufgerufen in `runMirror` direkt nach dem bestehenden `appendAndApply events` je Beobachtung. Prüft die vier Bedingungen aus der Modellierung (nicht gelöscht, `transfer_transaction_id` vorhanden, Gegenbuchung per `findEntryByLink` auffindbar, `TransferEntryId` noch nicht auf die Gegenseite gesetzt je Seite), baut ein oder zwei Ereignisse und schreibt sie in einem `appendAndApply`-Aufruf. Heilt den halbseitigen Fall: kommt die Gegenseite erst in einem späteren Lauf, trägt dieser Aufruf die Verknüpfung auf beiden Buchungen nach, auch wenn nur eine davon in diesem Lauf tatsächlich neu beobachtet wurde. Zählt bewusst nicht in `changedEntries`/`newEntries`/`deletedEntries`.
- `src/Server/Ledger/QueryHandlers.fs`: Offener Punkt 3 (Historien-Satz) gelöst — `describe` bekommt einen Zweig `describeTransferCounterpart`, Satz "Als Übertrag mit einer anderen Buchung verbunden" ohne Kontonamen der Gegenbuchung (die Payload trägt bewusst keinen Namen; ein Nachschlagen würde `describe` unrein machen). Ohne diesen Zweig wäre der Pattern-Match auf `LedgerEventBody` unvollständig gewesen (Compiler-Warnung FS0025).

**Tests (7 neu, TDD je Akzeptanzkriterium):**
- `src/Tests/LedgerTests.fs`: vier neue `apply`-Tests (Feld-für-Feld-Gleichheit nach dem Setzen über Record-Gleichheit, Selbstverknüpfung/fehlende Erstbeobachtung wirft, Überschreiben mit anderer Gegenbuchung wirft nicht, Replay reproduziert `TransferEntryId`); die bestehenden `eventTypeName`- und Roundtrip-Listen um den neuen Fall erweitert.
- `src/Tests/LedgerMirrorTests.fs`: bestehender Übertrags-Test korrigiert (die Verknüpfung ist jetzt v1 statt v2, Assertion vorher `Expect.isNone` mit veraltetem Kommentar) plus zwei neue Lauf-Tests: Idempotenz über einen zweiten identischen Lauf (keine neue laufende Nummer), Nachtrag der Verknüpfung auf beiden Seiten, wenn die Gegenseite erst im zweiten Lauf ankommt und die schon bekannte Seite dabei gar nicht erneut beobachtet wird.
- `src/Tests/LedgerPageTests.fs`: ein `describe`-Test für den neuen Historien-Satz.

Alle Tests laufen gegen In-Memory-SQLite (`USE_MEMORY_DB=true`, ADR 0030), nie gegen die Entwicklungsdatenbank.

**Offener Punkt 2 (Split-Zeilen-Überträge)** ist keine Implementierungsdetail dieses Ereignisses, sondern eine eigene Modellierungsfrage, wie der Task-Text verlangt: `Server.YnabObservations.Decoders.RawSubtransaction` liest heute pro Split-Zeile nur `transfer_account_id`, nicht `transfer_transaction_id` — obwohl YNABs Subtransaction-Read-Modell laut Recherche `ynab-transfer-in-split-subtransaction-2026-06-13` beides führt. Dieser Task deckt deshalb bewusst nur Überträge auf Buchungsebene ab (die einzige Form, die der heutige Beobachtungs-Contract liefert); die Lücke ist als Backlog-Item gemeldet, siehe `BACKLOG_ITEMS`.

`dotnet build` (Server, Tests) und `dotnet test` sind grün: 923 erfolgreich, 7 übersprungen (unveränderte YNAB-/Comdirect-Integrationstests, die `.env` brauchen), 0 Fehler.
