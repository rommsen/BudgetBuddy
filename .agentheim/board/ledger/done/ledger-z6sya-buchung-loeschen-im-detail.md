---
id: ledger-z6sya
title: "Buchung löschen im Detail, auch in YNAB, mit Rückgängig"
status: done
type: feature
context: ledger
created: 2026-11-09
completed:
depends_on: [ledger-m7k2d]
blocks: []
tags: [etappe-2, ynab, detail, loeschen]
related_adrs: [0053, 0058, 0083, 0091, 0092]
related_research: []
prior_art: [ledger-at63w, ledger-m7k2d]
---

## Why
Etappe 2 des Fahrplans (Lücken schließen, ADR 0086): Nichts soll mehr zu YNAB zwingen außer Reports. Eine
Buchung löschen geht heute nur in YNAB; BB sieht die Löschung erst über den Spiegel. Seit ledger-at63w gibt es
"Rückgängig" direkt nach dem Erfassen (ADR 0091), aber kein Löschen einer beliebigen Buchung im Detail.

## What
Entscheidung Roman (2026-11-09, ADR 0092 Punkte 1, 2 und 4).

- **Wer:** jede Buchung außer abgeglichenen (`Reconciled`) und in YNAB schon gelöschten. Barbuchungen,
  Comdirect-Importe und Buchungen aus YNAB, auch Überträge und aufgeteilte Buchungen.
- **Befehl `deleteEntry`** (Sprache: *BuchungLöschen*) nach dem Vorbild `discardCapturedBooking` (ADR 0091):
  unter `WriteGuard.exclusive` zuerst das YNAB-DELETE (`YnabClient.deleteTransaction` auf die aktuelle
  YNAB-Id, `currentYnabTransactionId`), nur bei Erfolg danach ein neues Ereignis `EntryDeletedInBb`
  (Entscheidungs-Familie, Actor `UserInBb`, Payload mindestens die gelöschte YNAB-Id und der Stand vor dem
  Löschen, den Rückgängig braucht), dann der Spiegel-Auslöser. `apply` setzt die Buchung auf gelöscht wie
  `EntryDeletionObservedInYnab`. Scheitert das DELETE, schreibt BB nichts ("Löschen nicht möglich: <Grund>").
  Eine Buchung ohne YNAB-Verknüpfung (Noch nicht in YNAB) wird abgelehnt, aus demselben Grund wie in ADR 0091
  Punkt 3.
- **Befehl `restoreDeletedEntry`** (Sprache: *GelöschteBuchungWiederherstellen*) für Rückgängig: unter der
  Schreib-Sperre ein POST mit allen Feldern aus dem Ereignis (Datum, Betrag, Konto, Payee, Kategorie oder
  Split-Zeilen samt Übertrags-Zeilen, Memo, Flag, `cleared`, `approved`; ein Übertrag mit der
  Übertrags-Payee). Hatte die Buchung eine `import_id`, bekommt die neue die Ersatz-`import_id`
  (`replacementImportId`); ohne bleibt sie ohne. Nur bei Erfolg ein Ereignis, das die Löschung aufhebt und die
  neue YNAB-Id stiftet; die alte wird `ynabsuperseded` (ADR 0058 Punkt 5). Scheitert der POST, bleibt die
  Buchung gelöscht ("Wiederherstellen nicht möglich: <Grund>").
- **Spiegel:** die spätere Beobachtung der Löschung (`EntryDeletionObservedInYnab`) ändert nach
  `EntryDeletedInBb` nichts mehr; nach einem Wiederherstellen setzt die Löschung der alten Id
  `DeletedInYnab` nicht (`isSupersededDeletion`).
- **Client (Detail, ADR 0053):** wie im Prototyp unten im Detail ein Knopf "Buchung löschen" in der
  Danger-Variante (`Button.Danger`, Größe klein), nach Erfolg schließt das Detail und ein Toast "Buchung
  gelöscht, auch in YNAB" mit "Rückgängig". Gesperrt (Knopf fehlt) bei abgeglichen, gelöscht und Noch nicht
  in YNAB. Ein Fehler kommt als Toast mit dem Grund, das Detail bleibt offen.
- **Wirkung:** eine gelöschte Buchung zählt wie heute nicht in Kontostand und Budget; der Filter "gelöschte
  Buchungen ausgeblendet" zeigt sie mit.

**Abweichungen vom Prototyp** (Version 17, "BB Prototyp Handy", Ansicht Buchungs-Detail): keine im Aussehen.
Der Prototyp sperrt nichts; BB lässt den Knopf bei abgeglichenen, gelöschten und nicht verknüpften Buchungen
weg (ADR 0092 Punkt 1, ADR 0091 Punkt 3).

## Acceptance criteria
- [ ] Handler: `deleteEntry` auf eine verknüpfte Buchung ruft genau ein DELETE mit der aktuellen YNAB-Id und
      hält danach `EntryDeletedInBb` fest; die Buchung ist gelöscht und zählt nicht im Kontostand.
- [ ] Handler: scheitert das DELETE, gibt es kein Ereignis und die Buchung bleibt unverändert.
- [ ] Handler: abgelehnt ohne Aufruf und ohne Ereignis bei abgeglichen, schon gelöscht und ohne YNAB-Verknüpfung,
      je ein Fall.
- [ ] Handler: `restoreDeletedEntry` legt eine einfache Buchung mit allen Feldern neu an (POST-Rumpf geprüft),
      mit Ersatz-`import_id` bei einer Buchung mit `import_id` und ohne bei einer ohne; danach ist die Buchung
      nicht mehr gelöscht und an die neue YNAB-Id verknüpft, die alte ist `ynabsuperseded`.
- [ ] Handler: Wiederherstellen einer aufgeteilten Buchung schickt die Split-Zeilen mit, ein Übertrag die
      Übertrags-Payee.
- [ ] Handler: scheitert der POST beim Wiederherstellen, bleibt die Buchung gelöscht, kein Ereignis.
- [ ] Spiegel: nach `EntryDeletedInBb` ändert die Beobachtung der Löschung den Zustand nicht; nach dem
      Wiederherstellen setzt die Löschung der alten Id `DeletedInYnab` nicht.
- [ ] Beide Befehle laufen unter `WriteGuard.exclusive` (Liste in der README nachgezogen).
- [ ] Endpoint-Negativtabelle (`RemotingEndpointNegativeTests.fs`) hat je neuem Endpoint eine Zeile; neue
      Pflichtfelder in `src/Shared` ziehen die Playwright-Fixtures in `scripts/*.test.mjs` im selben Schritt mit.
- [ ] Client-`update` unter .NET: Löschen schließt das Detail und zeigt den Toast mit Rückgängig; Rückgängig
      ruft das Wiederherstellen; ein Fehler zeigt den Toast mit Grund und lässt das Detail offen. Sichtbarkeit
      des Knopfs als reine Funktion getestet (abgeglichen, gelöscht, Noch nicht in YNAB, sonst sichtbar).
- [ ] Historie: "In BB gelöscht" und "In BB wiederhergestellt" mit Actor Roman.
- [ ] Seite neben der Skizze verglichen (Detail am Handy 412 px und am PC mit Panel, Screenshot je Ansicht
      neben dem Prototyp im Outcome), Abweichungen nur die genannten. [human-eye]
- [ ] BC-README ledger: Befehle, Ereignis(se), Schreib-Sperre und Buchungs-Detail nachgezogen, alte Sätze an
      allen Stellen (grep nach "nur in YNAB", "löschen", `EntryDiscardedInBb`).
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- ADR 0092 (Entscheidung), ADR 0091 (Vorbild Verwerfen), ADR 0058 Punkte 2 und 5 und Nachtrag 2026-11-09
  (Ersatz-`import_id`, `ynabsuperseded`), ADR 0053 (Detail), ADR 0083 (Ereignis).
- Ob `EntryDiscardedInBb` erweitert oder ein eigenes `EntryDeletedInBb` angelegt wird, entscheidet der Worker
  nach dem Muster der Ereignis-Familien; die Task nimmt ein eigenes an, weil `EntryDiscardedInBb` auf
  erfasste Buchungen begrenzt ist.
- Der Conductor legt beim Bauen die Prototyp-Datei und Screenshots der Detail-Ansicht in den Scratchpad und
  gibt Worker und Verifier die Pfade (CLAUDE.md, Skizzen).

## Verifier note (iteration 1)
**REASONS:**
- Kriterium "Beide Befehle laufen unter `WriteGuard.exclusive`" ist durch keinen Test belegt: ohne `WriteGuard.exclusive` in `deleteEntry` oder `restoreDeletedEntry` bliebe jeder Test grün. Das Projekt hat dafür `LedgerWriteGuardProbe.commandWaitsForMirror` (src/Tests/LedgerWriteGuardProbe.fs), genutzt etwa in `memoWriteGuardTests` (src/Tests/LedgerMemoTests.fs:397).
- Alles andere bis dahin geprüft und belegt; `./verify.sh` nicht beurteilt (erster Fehlschlag vorher).

**SUGGESTED_FIX:** Eine Write-Guard-Testliste in src/Tests/LedgerEntryDeletionTests.fs nach dem Muster `memoWriteGuardTests`: `commandWaitsForMirror` für `deleteEntry` (kein DELETE, kein `EntryDeletedInBb`, solange der Spiegel die Sperre hält) und für `restoreDeletedEntry` (kein POST, kein `EntryRestoredInBb`). Gegenprobe: beide rot ohne `WriteGuard.exclusive`. Danach `./verify.sh` bis VERIFY: PASS.

**ITERATION_HINT:** likely-fixable

## Outcome

Buchung löschen im Buchungs-Detail und Rückgängig, nach ADR 0092 Punkt 1, 2 und 4 und dem Vorbild Verwerfen (ADR 0091). `./verify.sh` im Worktree: VERIFY: PASS (dotnet 4574 grün, node 311 grün, beide Wächter, npm run build).

**Server**
- Ereignisse (`src/Server/Ledger/Events.fs`): eigenes `EntryDeletedInBb` (Payload: gelöschte YNAB-Id und Stand vor dem Löschen: Datum, Betrag, Payee, Memo, Kategorie, Splits, Übertrags-Ziel, Flag-Farbe, Cleared, Approved, import_id) und `EntryRestoredInBb` (alte und neue YNAB-Id, neue import_id), beide Version 1, Entscheidungs-Familie. Ein eigenes Ereignis statt `EntryDiscardedInBb` zu erweitern, weil das auf in BB erfasste Buchungen begrenzt ist und keinen Stand trägt (wie in der Task angenommen; kein eigenes ADR, ADR 0092 trägt die Entscheidung).
- `apply` (`Domain.fs`): Löschen setzt `DeletedInYnab`; Wiederherstellen setzt es zurück und leert Gegenbuchung und Split-Übertrags-Zuordnung. `linksFrom` von `EntryRestoredInBb` stiftet neue YNAB-Id, alte als `ynabsuperseded`, neue import_id; dadurch greift `isSupersededDeletion` im Spiegel ohne Änderung.
- Befehle (`CommandHandlers.fs`, Abschnitt "Buchung löschen und Rückgängig"): `deleteEntry` und `restoreDeletedEntry` unter `WriteGuard.exclusive`, YNAB zuerst, Ereignis nur bei Erfolg, Spiegel-Auslöser danach. Reine Helfer `deletedInBbPayloadOf`, `restoreRequestOf`, `pendingDeletionIn`. Abgelehnt ohne Aufruf: abgeglichen, schon gelöscht, ohne YNAB-Verknüpfung; Wiederherstellen nur nach einem noch offenen `EntryDeletedInBb`.
- Neuanlage über `YnabClient.createReplacement`: `SplitReplacement` hat ein neues Feld `TransferAccountId`; ein Übertrag ohne Split geht mit der `transfer_payee_id` des Ziels als `payee_id` hinaus, ohne `payee_name` (`buildReplacementBody`). Der Split-Schreibweg setzt `None`, sein Verhalten bleibt.
- API (`src/Server/Api.fs`): `EntryDeletionPorts`, `deleteEntryWith`, `restoreDeletedEntryWith`, `liveEntryDeletionPorts`; `LedgerApi.deleteEntry` und `restoreDeletedEntry` (`src/Shared/Api.fs`). Die Live-Verdrahtung belegt `apiWirings` in `StructureGuardTests.fs` mit drei Mustern und drei Gegenbeweisen (Ports getauscht in beiden Endpoints, Create ersetzt).
- Historie (`QueryHandlers.fs`): "In BB gelöscht", "In BB wiederhergestellt", Herkunft `DecidedInBb` (Roman).
- Neues Pflichtfeld `LedgerEntryView.Reconciled: bool` (für die Sichtbarkeit des Knopfs); alle F#-Konstruktionen in den Tests und die Playwright-Fixtures `scripts/ledger-filter-bar.mjs` und `scripts/ledger-status-column.mjs` im selben Schritt nachgezogen.

**Client**
- `Components/Ledger/View.fs`: `detailDeleteVisible` (rein: nicht bei abgeglichen, gelöscht, Noch nicht in YNAB) und unter der Historie der Knopf "Buchung löschen" (`Button.Danger`, `Button.Small`, volle Breite, `px-4` wie die Feldzeilen, `IsBusy` während des Aufrufs).
- `State.fs`/`Types.fs`: `StartDeleteEntry`, `RunDeleteEntry`, `EntryDeleteFinished`, `StartRestoreDeletedEntry`, `RunRestoreDeletedEntry`, `EntryRestoreFinished`, Sperren `DeletingEntries`/`RestoringEntries`. Erfolg: Detail zu, Toast "Buchung gelöscht, auch in YNAB" mit "Rückgängig" (`UndoDeleteEntry`, über `toastActionMsg` zu `StartRestoreDeletedEntry`); Fehler: Toast "Löschen nicht möglich: <Grund>", Detail bleibt offen. Wiederherstellen: Toast "Buchung wiederhergestellt" bzw. "Wiederherstellen nicht möglich: <Grund>".

**Tests**
- `src/Tests/LedgerEntryDeletionTests.fs` (24, davon 2 Schreib-Sperre): Codec, `linksFrom`, Historie, `pendingDeletionIn`; Handler mit `Probe` gegen In-Memory-SQLite (genau ein DELETE mit aktueller Id auch nach Ersatz, Ereignis mit Stand, Kontostand ohne die Buchung; DELETE scheitert; je eine Ablehnung abgeglichen, gelöscht, nicht verknüpft; POST-Anfrage mit allen Feldern und Ersatz-import_id, ohne import_id, lange import_id gekürzt; Split-Zeilen samt Übertrags-Zeile; Übertrags-Ziel; POST scheitert; kein zweites Wiederherstellen); Spiegel (Lösch-Beobachtung nach dem Löschen ohne Wirkung, Löschung der alten Id nach dem Wiederherstellen setzt `DeletedInYnab` nicht); API mit Fake-Ports; JSON-Vertrag `payee_id` beim Übertrag.
- `src/Tests/LedgerEntryDeletionClientTests.fs` (9): Sichtbarkeit in vier Fällen, `update` für Löschen, Fehler, Rückgängig und Wiederherstellen.
- `RemotingEndpointNegativeTests.fs`: je eine Zeile für `deleteEntry` und `restoreDeletedEntry` (leere Id).
- Schreib-Sperre (Iteration 2, Verifier-Fund): `deletionWriteGuardTests` in `LedgerEntryDeletionTests.fs` nach dem Muster `memoWriteGuardTests` mit `LedgerWriteGuardProbe.commandWaitsForMirror`: solange der Spiegel die Sperre hält, kein DELETE und kein `EntryDeletedInBb` (`deleteEntry`) bzw. kein POST und kein `EntryRestoredInBb` (`restoreDeletedEntry`); danach je genau ein Aufruf und das Ereignis. Gegenprobe: `WriteGuard.exclusive` in beiden Befehlen durch `id` ersetzt, beide Tests rot, danach zurückgesetzt.
- Gegenprobe: ohne die `ynabsuperseded`-Verknüpfung in `linksFrom` von `EntryRestoredInBb` werden drei Tests rot (linksFrom, Wiederherstellen, Spiegel); danach zurückgesetzt.

**Vergleich mit der Skizze** (Prototyp Version 17, Buchungs-Detail). Screenshots von BB gegen eine Kopie der Entwicklungsdatenbank (echte Buchungen, nur im Scratchpad, nicht ins Repo): `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/09c6dbe6-3a1e-4560-9130-ac0c7d910325/scratchpad/z6sya-shots/handy-detail-unten.png` (412 px, Touch) neben `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/09c6dbe6-3a1e-4560-9130-ac0c7d910325/scratchpad/prototyp/handy-detail-unten.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/09c6dbe6-3a1e-4560-9130-ac0c7d910325/scratchpad/z6sya-shots/pc-detail-unten.png` (1440 px, Panel) neben `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/09c6dbe6-3a1e-4560-9130-ac0c7d910325/scratchpad/prototyp/pc-detail-panel.png`; dazu `handy-detail.png` und `pc-detail.png`. Aufnahme-Skript `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/09c6dbe6-3a1e-4560-9130-ac0c7d910325/scratchpad/z6sya-tool/shot.mjs` (nutzt `scripts/screenshot-route.mjs`, das die Route `#/ledger/entry/<id>` nicht kennt). Ergebnis: Knopf ganz unten, volle Breite, roter Rand und rote Schrift, Text "Buchung löschen" wie im Prototyp. Unterschiede, die nicht aus der Task stammen und Roman sehen sollte: (1) `Button.Danger` des DS hat einen leichten roten Grund (`bg-error/10`), der Prototyp keinen; (2) Höhe am Handy 48 px (DS-Trefferfläche, Styleguide §4) statt 36 px, am PC etwa 30 px (`md:min-h-0`) statt 36 px; (3) in BB steht der Knopf unter der Historie, weil BB die Historie unter der Herkunft zeigt (Reihenfolge des heutigen Details). Die Task nennt "keine Abweichung im Aussehen" und fordert `Button.Danger` klein; ob (1) und (2) als gleich gelten, entscheidet Roman [human-eye].

**Für den Conductor**: README_DELTA hat keinen Eintrag für `## Ereignisse`: ein append landet dort hinter Fließtext (Gate-Prüfung 3). Vorschlag zum Platzieren von Hand hinter dem Bullet `EntryDiscardedInBb`: "- `EntryDeletedInBb` / `EntryRestoredInBb`: Buchung im Detail gelöscht, nachdem YNAB sie gelöscht hat, bzw. mit \"Rückgängig\" wiederhergestellt, nachdem YNAB sie neu angelegt hat (ledger-z6sya, ADR 0092). Payload wie unter Key events. Schema-Version 1, Actor `UserInBb`. Historien-Sätze: \"In BB gelöscht\", \"In BB wiederhergestellt\"." Die README enthält "nur in YNAB" nirgends; "löschen" trifft nur "Alle Filter löschen" und den Spiegel-Abschnitt (unberührt). Für den Merge mit ledger-m7mye: dieselben Stellen (`LedgerEntryView.Reconciled`, Ereignis-DU, `apiWirings`) sind additiv angelegt.
