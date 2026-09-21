---
id: ledger-v5hkt
title: "Selbstbezügliche Übertrags-Verknüpfung legt den Spiegel dauerhaft still"
status: done
type: bug
context: ledger
created: 2026-09-20
completed:
depends_on: []
blocks: []
tags: [backend, kassenbuch, spiegel, uebertrag]
related_adrs: [0013, 0014, 0015]
related_research: []
prior_art: [ledger-t7v2q]
---

## Why

Lücke 7 aus dem QA-Review nach dem Work-Lauf vom 2026-09-19 (erfasst als
`ledger-r7dm2`). Anders als die übrigen Punkte dort ist das kein Testloch, sondern eine
fehlende Sperre im Produktionscode.

`apply` (`src/Server/Ledger/Domain.fs:220`) wirft, wenn ein
`EntryTransferCounterpartObservedInYnab` auf die eigene `EntryId` zeigt: eine Buchung ist
nie ihr eigener Übertrag. Das ist richtig und getestet.

`linkTransferCounterpart` (`src/Server/Ledger/CommandHandlers.fs:204`) hat keine
entsprechende Sperre. Löst `findEntryByLink YnabSystem` auf die eigene `entryId` auf,
baut die Funktion genau dieses Ereignis, `appendAndApply` ruft `apply`, `apply` wirft.
`runMirror` fängt das im äußeren `try/with` (Z. 362) und liefert
`Error "Spiegel-Lauf abgebrochen: ..."`. Der Schaden steckt in den Folgen: die
Beobachtungs-Schleife bricht ab, alle folgenden Beobachtungen des Deltas werden nicht
verarbeitet, und `server_knowledge` rückt nach ADR 0014 nicht vor. Der nächste Lauf holt
dasselbe Delta, stolpert über dieselbe Buchung und bricht wieder ab.

**Eine einzige defekte Buchung legt den Spiegel damit dauerhaft still.** Das ist kein
„unwahrscheinlich, aber teuer", wie der Elterntask `ledger-r7dm2` es einordnet, sondern
ein Totalausfall des Kassenbuch-Gedächtnisses ohne Selbstheilung: BB kommt aus diesem
Zustand von allein nicht mehr heraus, und die rund 14.000 übrigen Buchungen laufen mit
der defekten einen ins Aus. Wie selten der Auslöser ist, ändert daran nichts.

Der Auslöser muss nicht aus YNAB kommen. `findEntryByLink` löst über BBs eigene
Verknüpfungstabelle auf, und ADR 0015 macht nur System plus Wert eindeutig, nicht Buchung
plus System: eine Buchung kann mehr als eine `ynab`-Zeile tragen (Force-Import mit neuer
Versendung). Auch darüber kann der Lookup auf die eigene Id zeigen. Die Prüfung gehört
deshalb auf die aufgelöste Buchungs-Id, nicht auf einen Stringvergleich der YNAB-Ids.

## What

In `linkTransferCounterpart`, im `Some counterpartId`-Zweig, vor dem Laden der beiden
Zustände auf `counterpartId = entryId` prüfen. Trifft es zu: gar kein Ereignis bauen,
auch nicht für die „andere" Seite (es gibt keine), eine Warnzeile über `printfn` mit
Buchungs-Id und YNAB-Transaction-Id schreiben (Muster: `triggerMirrorInBackground`,
CommandHandlers.fs:773) und `return ()`. Der Lauf geht weiter.

Stilles Überspringen statt Abbruch, weil ein abgebrochener Lauf nach ADR 0014 dasselbe
Delta erneut holt und der Selbstbezug in den Daten nicht von allein verschwindet: aus
einem einmaligen Datendefekt würde ein dauerhafter Stillstand. Die Verknüpfung ist
ohnehin ein Nachtrag des Laufs, kein Beobachtungsergebnis; sie zählt ausdrücklich nicht
in `NewEntries`/`ChangedEntries`/`DeletedEntries`, und ihr Fehlen kostet nichts außer der
noch nicht gesetzten `TransferEntryId`. Die Warnzeile geht nicht in den Lauf-Status: ein
erfolgreicher Lauf soll nicht wegen eines übersprungenen Nachtrags als Fehler erscheinen
(ADR 0014).

Der `invalidOp` in `apply` bleibt unverändert stehen. Faustregel: der Wächter wirft, der
Erzeuger baut nie ein Ereignis, das den Wächter treffen kann. Trifft der Wächter doch,
ist das ab jetzt ein Programmierfehler, kein Datenfehler, und darf laut sein.

## Acceptance criteria

- [ ] **Die Sperre steht im Produktionscode.** `linkTransferCounterpart` in
      `src/Server/Ledger/CommandHandlers.fs` prüft im `Some counterpartId`-Zweig vor dem
      Laden der beiden Zustände auf `counterpartId = entryId`, schreibt in diesem Fall
      eine Warnzeile über `printfn` mit Buchungs-Id und `obs.YnabTransactionId` und
      kehrt ohne Ereignis zurück. Ohne diese Änderung ist der Task nicht erfüllt, auch
      wenn alle Tests stehen.
- [ ] **Der Lauf überlebt den Selbstbezug.** Ein Testfall in
      `src/Tests/LedgerMirrorTests.fs`, neben den bestehenden Übertrags-Fällen und mit
      eigenem `Run.budget`, fährt `Run.run` über ein Delta aus zwei Beobachtungen: die
      erste hat `TransferYnabTransactionId = Some "<ihre eigene YnabTransactionId>"`, die
      zweite ist eine gewöhnliche Buchung auf demselben Konto. Erwartet: der Lauf ist
      `Ok` (`Run.expectOk`) und `summary.NewEntries = 2`. Vor dem Fix ist der Lauf
      `Error "Spiegel-Lauf abgebrochen: ..."` und die zweite Buchung fehlt.
- [ ] **Keine Verknüpfung, kein Ereignis.** Derselbe Testfall erwartet für die erste
      Buchung `TransferEntryId = None` und ein Ereignis-Log ohne jeden Eintrag vom Typ
      `EntryTransferCounterpartObservedInYnab`, also genau eine Zeile
      (`EntryObservedInYnab`). Fängt eine Umsetzung, die den Selbstbezug zwar nicht mehr
      wirft, aber trotzdem ein sinnloses Ereignis anhängt.
- [ ] **Das Gedächtnis rückt vor.** Derselbe Testfall erwartet
      `summary.ServerKnowledge` gleich dem `ServerKnowledge` des gesäten Batches; ein
      zweiter Lauf mit demselben Delta liefert `NewEntries = 0` und hängt kein weiteres
      Ereignis an. Das ist der eigentliche Schaden des Fehlers: ohne den Fix bleibt der
      Spiegel auf diesem Delta stehen.
- [ ] **Die Sperre greift auf der aufgelösten Buchungs-Id.** Die Prüfung vergleicht
      `counterpartId = entryId`, nicht
      `obs.TransferYnabTransactionId = Some obs.YnabTransactionId`. Belegt durch einen
      zweiten Testfall: eine Buchung trägt über `Ledger.Persistence.linkEntry` eine
      zweite `ynab`-Verknüpfung mit einem anderen Wert, und eine Beobachtung derselben
      Buchung zeigt mit `TransferYnabTransactionId` auf genau diesen zweiten Wert. Die
      YNAB-Ids sind verschieden, die aufgelöste Buchung ist dieselbe. Erwartet: Lauf
      `Ok`, `TransferEntryId = None`, kein Verknüpfungs-Ereignis. Ein Stringvergleich der
      YNAB-Ids fällt hier durch.
- [ ] **Der Wächter bleibt stehen.** `src/Server/Ledger/Domain.fs` wirft für ein
      `EntryTransferCounterpartObservedInYnab` auf die eigene Id weiterhin; der
      bestehende Test in `src/Tests/LedgerTests.fs` (Z. 432, „eine Buchung darf sich
      nicht selbst als Uebertrags-Gegenseite verknuepfen") bleibt unverändert und grün.
- [ ] **Die echten Übertrags-Fälle bleiben unberührt.** Die drei bestehenden
      Übertrags-Testfälle in `src/Tests/LedgerMirrorTests.fs` (zwei echte Seiten, zweiter
      Lauf ohne weiteres Ereignis, halbseitiger Nachtrag) bleiben unverändert und grün.
- [ ] `dotnet build` und `dotnet test` grün.

## Notes

**Herkunft.** Der Elterntask `ledger-r7dm2` ordnet diesen Punkt als „unwahrscheinlich,
aber teuer, und billig abzusichern" ein und verweist auf einen Review-Volltext im Diary
vom 2026-09-19 20:14, den es nicht gibt. Beide Angaben sind hier korrigiert: die Folgen
sind im `## Why` aus `src/Server/Ledger/CommandHandlers.fs` (äußeres `try/with`, Z. 362)
und ADR 0014 hergeleitet, nicht übernommen.

`linkTransferCounterpart` bleibt `private`. Der Test fährt über `runMirror` mit
injizierten Fetchern (`Run.run` plus `Run.fetchers`), wie alle vier bestehenden
Übertrags-Tests. Das `internal` von `MirrorGuard` ist kein Präzedenzfall: dort ging es
darum, einen werfenden Lauf an `runMirror`s Fehlerbehandlung vorbei zu prüfen, also um
das Gegenteil des hier gewünschten Verhaltens.

Zwei Kommentar-Ergänzungen gehören dazu: der Bedingungs-Block über
`linkTransferCounterpart` listet die Fälle auf, die der Erzeuger abfängt (gelöscht, keine
`transfer_transaction_id`, Gegenbuchung nicht gefunden, zeigen schon aufeinander), der
Selbstbezug ist der fünfte; und der Kommentar über dem `invalidOp` in
`src/Server/Ledger/Domain.fs` (ab Z. 209) hält fest, dass der Erzeuger diesen Fall
vorher abfängt und ein Treffer hier deshalb einen Programmierfehler bedeutet.

README-Delta für `.agentheim/knowledge/contexts/ledger/README.md`, Abschnitt
„Übertrags-Verknüpfung" (Z. 322): der Satz „Laut abgelehnt wird nur die Verknüpfung einer
Buchung mit sich selbst, die in keinem gültigen Log vorkommt." bekommt den Nachsatz, dass
der Spiegel-Lauf ein solches Ereignis gar nicht erst baut, den Fall wie „Gegenbuchung
nicht gefunden" überspringt, eine Warnzeile schreibt und weiterarbeitet, und dass der
`invalidOp` daneben nur noch einen Programmierfehler abdeckt.

## Outcome

`linkTransferCounterpart` (`src/Server/Ledger/CommandHandlers.fs:225`) prüft im
`Some counterpartId`-Zweig jetzt vor dem Laden der beiden Zustände, ob die aufgelöste
Gegenbuchungs-Id mit der eigenen `entryId` übereinstimmt (neuer `when`-Guard-Fall vor dem
bisherigen `Some counterpartId ->`-Zweig, Z. 241). Trifft das zu, wird kein Ereignis gebaut,
eine Warnzeile über `printfn` mit der Buchungs-Id und `obs.YnabTransactionId` geschrieben, und
der Lauf läuft mit der nächsten Beobachtung weiter, statt im äußeren `try/with` von `runMirror`
(Z. 362) abzubrechen. Die Sperre vergleicht bewusst die aufgelöste Id, nicht die
YNAB-Id-Strings (ADR 0015: System plus Wert ist eindeutig, nicht Buchung plus System; eine
Buchung kann mehr als eine `ynab`-Verknüpfung tragen).

`apply` (`src/Server/Ledger/Domain.fs:220`) ist unverändert stehen geblieben; der Kommentar
über dem `invalidOp` hält jetzt fest, dass der Erzeuger den Selbstbezug vorher abfängt und ein
Treffer dort ab jetzt ein Programmierfehler ist, kein Datenfehler.

**Tests (2 neu, TDD, rot vor der Änderung):**

- `src/Tests/LedgerMirrorTests.fs:817`, "eine selbstbezügliche Übertrags-Verknüpfung wird
  übersprungen, der Lauf läuft weiter": eigenes `Run.budget "uebertrag-selbstbezug"`, Delta aus
  einer selbstbezüglichen Beobachtung und einer gewöhnlichen zweiten Buchung. Nach der
  Änderung: `Ok`, `NewEntries = 2`, `summary.ServerKnowledge = 100L` und persistiert
  (`loadServerKnowledge budgetId = Some 100L`), ein zweiter Lauf über dasselbe Delta liefert
  `NewEntries = 0` bei unveränderter Ereigniszahl. Der Log der defekten Buchung ist auf seinen
  Inhalt geprüft, nicht nur auf `TransferEntryId`: `loadEventsForEntry |> List.map EventType`
  gleich `[ "EntryObservedInYnab" ]`.
- `src/Tests/LedgerMirrorTests.fs:863`, "die Sperre vergleicht die aufgelöste Buchungs-Id,
  nicht die YNAB-Id-Strings": die Buchung bekommt über
  `Ledger.Persistence.linkEntry entryId YnabSystem "zweitversand-1-zweitversand"` eine zweite
  `ynab`-Verknüpfung mit anderem Wert; die Beobachtung derselben Buchung
  (`YnabTransactionId = "zweitversand-1"`) zeigt mit `TransferYnabTransactionId` auf genau
  diesen zweiten Wert. Die YNAB-Id-Strings sind verschieden, die aufgelöste Buchung ist
  dieselbe. Nach der Änderung `Ok`, `TransferEntryId = None`, kein Verknüpfungs-Ereignis.

Der bestehende Wächter-Test in `src/Tests/LedgerTests.fs:431` und die drei bestehenden
Übertrags-Lauf-Tests in `LedgerMirrorTests.fs` bleiben unverändert grün (74 Einfügungen, 0
Löschungen in der Testdatei).

`dotnet build` und `dotnet test` (`USE_MEMORY_DB=true`, ohne `--settings`): 943 erfolgreich, 7
übersprungen (`.env`-gebundene Comdirect-/YNAB-Integrationstests), 0 Fehler.

**Verifikation (Iteration 1, PASS).** Der Verifier hat zwei Dinge unabhängig nachgestellt,
statt sie zu glauben.

Erstens **Rot vor Grün**: in einer Arbeitskopie des Worktrees (`rsync` ohne `bin`/`obj`/`.git`,
der Worktree selbst unangetastet, `git status --porcelain` danach leer) hat er
`CommandHandlers.fs` und `Domain.fs` auf `HEAD~1` zurückgesetzt und die beiden neuen Tests
gefahren: beide rot, jeweils mit exakt
`Spiegel-Lauf abgebrochen: Uebertrags-Verknuepfung fuer Buchung <guid> zeigt auf sich selbst`.

Zweitens eine **Mutationsprobe** auf die entscheidende Frage, ob der zweite Test wirklich
diskriminiert: ersetzt man den Guard durch den Stringvergleich
`obs.TransferYnabTransactionId = Some obs.YnabTransactionId`, läuft Test 1 grün und Test 2 rot.
Der Test fängt also nachweislich genau die Halb-Lösung ab, die das Kriterium benennt.

Außerdem geprüft: der Guard steht vor beiden `loadEntry`-Aufrufen (F#-Match-Reihenfolge, ein
`when`-Guard nach dem allgemeinen Fall hätte nie gefeuert); auf dem Selbstbezug-Pfad wird
weder der `Error`-Kanal noch `NewEntries`/`ChangedEntries`/`DeletedEntries` noch der Lauf-Status
berührt, der `printfn` geht nur nach stdout (ADR 0014: ein erfolgreicher Lauf soll nicht wegen
eines übersprungenen Nachtrags als Fehler erscheinen).

**Vom Conductor auf dem Hauptbaum nachgezogen.** Das README-Delta des Workers zielte korrekt
auf den Absatz `**Übertrags-Verknüpfung:**` im Abschnitt `## Ereignisse`, war über
`applyReadmeDelta` aber nicht anwendbar: der Trockenlauf ergab Disposition `anchor-missing`,
weil die Stelle ein Absatz ist und kein Spalte-0-Bullet, `findBulletsInRange` dort also nichts
zum Ankern findet. Angewandt hätte das Werkzeug den neuen Text unter den alten gehängt, sodass
das README zwei widersprechende Fassungen getragen hätte. ADR 0040 führt genau diesen Abschnitt
als per `replace` unerreichbar und weist den Fall dem Conductor zu; der Absatz ist deshalb von
Hand ersetzt worden, mit dem `body`-Text des Workers unverändert.
