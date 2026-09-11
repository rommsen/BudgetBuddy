---
id: ledger-qvt84
title: "Import-Push schreibt zuerst die Kassenbuch-Buchung (Comdirect-Import + Review-Spur), dann YNAB, dann Verknüpfung; Push liefert YNAB-Ids und behebt drei Push-Bugs"
status: todo
type: feature
context: ledger
created: 2026-09-11
completed:
depends_on: [ledger-cscw1, categorization-gdrx8]
blocks: []
tags: [ledger, ynab-sync, push, integration, identity]
related_adrs: [0006, 0013, 0015, 0019, 0021]
related_research: []
prior_art: [ledger-m8r2v]
---

## Why
Die Vision verlangt, dass das Kassenbuch BB-Entscheidungen kennt (v1-Kriterium). Heute
endet `importToYnab` (`src/Server/Api.fs`) ohne jede Wirkung auf das Kassenbuch; eine
gepushte Buchung taucht erst über den nächsten Spiegel-Lauf mit falscher Herkunft auf
("YNAB-Spiegel" statt "Comdirect-Import" bzw. "Regel"). ADR 0021 legt Reihenfolge und
Fehlerverhalten fest: Kassenbuch zuerst, dann Push, dann Verknüpfung; kein Rollback bei
Push-Fehler.

Dafür muss der Push YNABs Antwort auswerten. `TransactionCreateResult` in
`src/Server/YnabClient.fs` trägt heute nur `CreatedCount`, `DuplicateImportIds` und
`RejectedTransferTransactionIds`, keine Transaction-Ids. Bei der Analyse (architect,
2026-09-11, am Code bestätigt) sind drei Bugs an derselben Stelle aufgefallen, die hier
mitbehoben werden:
- **Zukunftsdatum:** `createTransactions` filtert Buchungen mit Datum in der Zukunft still
  heraus, `importToYnab` markiert sie danach trotzdem als `Imported`. Eine solche Buchung
  kommt nie in YNAB an und gilt als erledigt.
- **Force-Import:** die neue ImportId wird nur innerhalb von `createTransactions` erzeugt
  und nie zurückgegeben; der Aufrufer kann nicht verknüpfen.
- **Nicht lesbare Antwort:** kann BB die 201-Antwort nicht dekodieren, gelten pauschal
  alle gesendeten Buchungen als angelegt.

Dieser Task nimmt den früheren Task `ynab-b7q2m` (Antwort auswerten, Import-Teil) in sich
auf (Entscheidung Roman, 2026-09-11).

## What
**Teil 1: Push in `ynab-sync` (Conformist-Rand)**
1. `createTransactions` in **Vorbereiten** (Skipped- und Zukunfts-Filter,
   Transfer-Payee-Auflösung und Rejects, ImportId-Zuweisung einschließlich Force-Import)
   und **Senden** (postet genau die vorbereitete Liste) aufteilen. Vorbereiten liefert die
   tatsächlich zu sendenden Buchungen samt ihrer ImportId zurück.
2. Die Antwort wird ausgewertet: `TransactionCreateResult` (oder ein neuer Rückgabetyp)
   trägt die YNAB-Transaction-Ids je angelegter Buchung, zugeordnet über die ImportId.
3. Force-Import: die neu erzeugte ImportId ist für den Aufrufer auslesbar.
4. Zukunftsdatum: eine nicht gesendete Buchung wird nicht als `Imported` geführt.
5. Nicht auswertbare Antwort: die Buchungen bekommen einen erkennbaren Zustand
   "unbekannt", keinen pauschalen Erfolg.

**Teil 2: Kassenbuch vor dem Push (`ledger`)**
6. Reine Funktion (z. B. `recordComdirectPush` in `Ledger/Domain.fs`) baut aus
   (AccountId, ImportId, ComdirectReference, Date, Amount, Payee, Memo, Cleared) plus der
   `ReviewSteps`-Liste (`categorization-gdrx8`) eine geordnete `LedgerEvent list`:
   `EntryImportedFromComdirect`, dann je Review-Schritt `EntryCategorizedByRule` (Actor
   `Rule`) bzw. `EntryRecategorized` (Actor `UserInBb`), mit den ursprünglichen
   Zeitpunkten aus `ReviewSteps`, nicht dem Push-Zeitpunkt.
7. Neue Fassade in `Ledger/CommandHandlers.fs`, aufgerufen aus `importToYnab` **vor** dem
   Senden, je zu importierender Buchung:
   a. Prüfen, ob die ImportId schon verknüpft ist (`findEntryByLink` für `importid`);
      wenn ja, überspringen (ADR 0021 Punkt 3, keine zweite Buchung).
   b. Sonst `appendAndApply` mit den Ereignissen aus Schritt 6. Scheitert das Schreiben
      für eine Buchung, wird sie nicht an YNAB gesendet.
8. Gesendet wird nur, was vorher erfolgreich ins Kassenbuch geschrieben wurde.
   Übersprungene Buchungen, Buchungen mit Zukunftsdatum und Transfer-Rejects (ADR 0006)
   erzeugen keine Kassenbuch-Buchung.
9. Nach der Antwort: für jede angelegte Buchung `EntryLinkedToYnab` schreiben (Zuordnung
   über die ImportId aus Teil 1). Ein gescheiterter Push, ein Timeout oder eine
   Duplikat-Ablehnung durch YNAB rollt die Kassenbuch-Buchung nicht zurück (ADR 0021
   Punkt 2); sie bleibt ohne `ynab`-Verknüpfung, bis ein neuer Versuch oder der Spiegel
   sie über die ImportId verknüpft.
10. Split mit Transfer-Zeile (ADR 0006, 0019): das Transfer-Ziel steht als
    `YnabAccountId` plus Name im Payload, eine `LedgerAccountId` ist dafür nicht nötig. Das
    Import-Konto selbst muss als `LedgerAccountId` vorliegen; ist es unbekannt, vorher
    Konten nachziehen oder mit klarer Fehlermeldung abbrechen (Entscheidung beim
    Implementieren, im Diary begründen).

## Acceptance criteria
- [ ] `TransactionCreateResult` trägt YNAB-Transaction-Ids je gesendeter Buchung,
      zuordenbar über die ImportId (Test).
- [ ] Force-Import: die neu erzeugte ImportId ist vom Aufrufer auslesbar
      (Regressionstest, rot vor dem Fix).
- [ ] Buchungen mit Zukunftsdatum werden nicht als `Imported` geführt, wenn sie nicht
      gesendet wurden (Regressionstest, rot vor dem Fix).
- [ ] Eine nicht auswertbare YNAB-Antwort markiert Buchungen nicht pauschal als
      importiert (Regressionstest, rot vor dem Fix).
- [ ] Nach erfolgreichem Import-Push zeigt die Historie der neuen Buchung:
      Import-Ereignis, gegebenenfalls Regel-Ereignis, gegebenenfalls Korrektur-Ereignis
      (mit den ursprünglichen Review-Zeitpunkten), Verknüpfungs-Ereignis.
- [ ] Übersprungene Buchungen und von YNAB als Duplikat abgelehnte Buchungen erzeugen
      keine zweite Kassenbuch-Buchung.
- [ ] Ein simulierter Push-Fehler (Netzwerkfehler, Timeout) lässt die bereits
      geschriebene Kassenbuch-Buchung unverändert stehen, ohne `ynab`-Verknüpfung (Test).
- [ ] Ein zweiter Versuch für dieselbe ImportId erzeugt keine zweite Buchung (Test).
- [ ] Der nächste Spiegel-Lauf trifft eine so angelegte Buchung (über YNAB-Id oder
      ImportId) und legt keine zweite an (Test).
- [ ] Eine Split-Buchung mit Transfer-Zeile landet korrekt im Kassenbuch (Kategorie-Zeile
      und Transfer-Zeile, Summe stimmt).
- [ ] Replay-Test bleibt grün; Tests nur gegen In-Memory-SQLite.
- [ ] `dotnet build` und `dotnet test` grün.

## Notes
- Reihenfolge (Kassenbuch vor Push) und "kein Rollback bei Push-Fehler" sind in ADR 0021
  entschieden und von Roman bestätigt (2026-09-11), nicht mehr offen für den Worker.
- Die Markierung "noch nicht in YNAB" für unverknüpfte Buchungen ist gewollt (Roman,
  2026-09-11), gehört aber in die Oberfläche (`ledger-xbrht`), nicht in diesen Task.
- Der größte Task dieser Reihe. Teil 1 zuerst mit seinen Regressionstests grün machen,
  dann Teil 2 darauf setzen. `ledger-tdnpk` ändert parallel `createManualTransaction` in
  derselben Datei (`YnabClient.fs`), aber eine andere Funktion.
- Die ImportId-Rekonstruktion in `importToYnab` (`BB:{txId ohne Bindestriche}` im
  Duplikat-Zweig) muss nach der Umstellung dieselbe ImportId ergeben wie
  `Shared.Domain.generateImportId`; im Force-Fall stimmt sie heute nicht.
- **Nachtrag 2026-09-11 (Entscheidung Roman zu `categorization-gdrx8`):** `ReviewActor` hat
  einen dritten Fall für Kategorien aus dem Order-ID-Abgleich beim Abruf (Vorschlag aus der
  YNAB-Historie derselben Amazon-Bestellung), Anzeigename "Order-ID-Abgleich". Die
  Übertragung auf Geschwister in der Review ist `ByUser`. Für Schritt 6 heißt das: das
  Kassenbuch braucht einen passenden Urheber (neuer `Actor`-Fall, z. B. `OrderIdMatch`)
  und ein Ereignis dafür (`EntryRecategorized` mit diesem Actor oder ein eigenes Ereignis,
  Wahl beim Implementieren, im Ereignis-Katalog des README festhalten). Die Historie zeigt
  dann z. B. "Order-ID-Abgleich: Büro". Akzeptanz: ein Test mit einer Buchung, deren
  Kategorie aus dem Order-ID-Abgleich stammt, belegt diesen Urheber in der Historie.
