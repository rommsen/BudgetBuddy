---
id: ledger-z6sya
title: "Buchung löschen im Detail, auch in YNAB, mit Rückgängig"
status: backlog
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
