---
id: budgeting-c2t9w
title: "Target-Formular: Tab-Wechsel schließt Rückfragen, Entfernen-Bestätigung schließt nach Neulesen, genaue Meldungen beim Teilergebnis und beim Anlegen außerhalb des Monats"
status: doing
type: bug
context: budgeting
created: 2026-11-04
completed:
depends_on: [budgeting-b5n8q]
blocks: [budgeting-d7r4k]
tags: [budget, target, rueckfrage, meldungen]
related_adrs: [0072, 0080]
related_research: []
prior_art: [budgeting-g2ty9, budgeting-f4k9n, budgeting-xp46a]
---

## Why
Aus budgeting-h9c3w (Reviews zu g2ty9, Punkte 6, 7, 9 bis 13). Roman 2026-11-04: selbst entscheiden; BB soll bald führend werden und YNAB spiegeln. Deshalb hier keine neuen Abläufe für YNAB-Sonderfälle, nur richtige Meldungen und Sperren. Screenreader-Punkte sind nicht im Umfang.

## What

### 1. Tab-Wechsel und gleichzeitige Aufrufe (Punkt 6)
Heute: im Tab Target die Warnung oder "Target entfernen?" öffnen, auf Tab Geld wechseln, "Target pausieren" läuft (`pauseHeldByConfirm` gilt nur bei `model.Tab = TargetTab`, `src/Client/Components/Budget/Pause.fs`). Zurück im Tab Target ist die Rückfrage noch offen, "Wechseln" oder "Entfernen" prüft nur `targetBusy`, nicht `pauseBusy` (`TargetEdit.fs`, `start`, `askRemoveTarget`). Zwei Aufrufe laufen mit altem `Shown`.

Entscheidung: `SelectInspectorTab` weg vom Tab Target schließt offene Rückfragen (`ConfirmingChange`, `ConfirmingRemove`), die Eingabe bleibt. `start` und `askRemoveTarget` tun nichts, solange eine Pause läuft (`pauseBusy`). Der Pause-Knopf im Tab Geld bleibt bedienbar.

### 2. Entfernen-Bestätigung nach stillem Neulesen (Punkt 7)
`syncTargetForm` (`TargetEdit.fs`) schließt nur die Warnung. Liefert ein Neulesen die Kategorie ohne Target, bleibt "Target entfernen?" offen, "Entfernen" tut nichts, "Target anlegen" und Pause sind gesperrt bis "Abbrechen". Entscheidung: `syncTargetForm` setzt `ConfirmingRemove` zurück, wenn kein Target mehr gezeigt ist.

### 3. Meldung beim Teilergebnis (Punkte 9, 10, 11)
- Der Client hängt "Speichern setzt das Datum erneut." ohne Satzzeichen an die Servermeldung (`targetNoticeText`, `TargetEdit.fs`) und auch dann, wenn BB Schritt 2 selbst zurückgehalten hat (falscher Rhythmus oder Art in YNABs Antwort). Dort plant erneutes Speichern wieder einen Wechsel, setzt also nicht nur das Datum.
- `unexpectedRhythmText` (`src/Server/Budgeting/Domain.fs`) und `targetRhythmUnexpectedText` (`src/Shared/Budgeting.fs`) sagen "anderer Rhythmus" auch, wenn YNAB eine andere Art (etwa MF statt NEED) oder gar kein Target zeigt ("monatlich statt monatlich").

Entscheidung: Der Server baut den ganzen Satz, der Vertrag bleibt (`TargetPartlyWritten of Target option * message`). Nur wenn YNAB das Datum abgelehnt hat (`LaterNotWritten`), endet er mit "Speichern setzt das Datum erneut."; die Meldung davor endet mit Punkt, wenn der Grund kein Satzzeichen am Ende hat. Beim zurückgehaltenen Schritt 2 (`LaterHeldBack`) nennt der Satz den Grund aus drei Fällen: kein Target in YNABs Antwort, keine NEED-Art (Art nennen), anderer Rhythmus (beide nennen); ohne Satz zum erneuten Speichern. Der Client hängt nichts mehr an. Reine Funktion in `Shared.Budgeting` mit einem Test je Fall.

### 4. Anlegen außerhalb des laufenden Monats (Punkt 12)
`validateTargetChange` (`src/Server/Validation.fs`) und der Monatswächter in `changeTarget` (`CommandHandlers.fs`) melden "Target ändern geht nur im laufenden Monat." auch beim Anlegen. Entscheidung: bei `Shown = None` und `Wanted = Some _` gilt `TargetCreateMonthText`.

### 5. Knopfzustände im Walk (Punkt 13)
Der Walk der Rückfragen (`askWalkTests`, `src/Tests/TargetChangeClientTests.fs`) prüft je Schritt gesendete Aufrufe, offene Rückfragen und `canSaveTarget`, aber nicht, ob "Target entfernen" und "Target pausieren" gesperrt aussehen. Die Pause-Sperre steht inline in `PauseView.fs` (`IsDisabled = ...`). Entscheidung: Sperre als reine Funktion in `Pause.fs` (Vorbild `saveButtonState`), `PauseView` ruft sie nur auf; der Walk prüft je Schritt `removeButtonState(...).Disabled` und die Pause-Sperre gegen das Orakel und bekommt das Ereignis "Tab wechseln" (Target, Geld) für Punkt 1.

## Acceptance criteria
- [ ] Walk mit "Tab wechseln": in keiner Reihenfolge laufen ein Target-Aufruf und ein Pause-Aufruf gleichzeitig; nach dem Wechsel weg vom Tab Target ist keine Rückfrage offen; im Tab Geld ist Pausieren bei geschlossener Rückfrage möglich. Mit dem alten `SelectInspectorTab` rot (Regressionstest, Kommentar zum Bug).
- [ ] Test mit stillem Neulesen ohne Target: `ConfirmingRemove` ist danach false, "Target anlegen" bedienbar. Vor dem Fix rot.
- [ ] Reine Tests für den Satz beim Teilergebnis: Datum abgelehnt mit Grund ohne Punkt ergibt "...: <Grund>. Speichern setzt das Datum erneut."; Grund mit Punkt ohne doppelten Punkt; zurückgehalten ohne Target, mit anderer Art, mit anderem Rhythmus je ein eigener Satz ohne "Speichern setzt ...". Der Client zeigt die Meldung unverändert (Test am `update`).
- [ ] Validation-Test und Negativzeile in `RemotingEndpointNegativeTests.fs`: Anlegen im Vormonat meldet `TargetCreateMonthText`; Ändern im Vormonat weiter `TargetEditMonthText`.
- [ ] Walk prüft je Schritt die Sperre von "Target entfernen" und "Target pausieren"; die Pause-Sperre ist eine reine Funktion, `PauseView` ruft sie nur auf.
- [ ] README budgeting (Target-Änderung) per README_DELTA nachgezogen, wo Meldungen oder Sperren beschrieben sind.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Nicht parallel zu anderen Arbeiten in `TargetEdit*.fs`, `Pause*.fs`, `changeTarget`. Ob Schritt 1 nach unerwartetem Rhythmus erneut gehen darf, bleibt offen (mit BB als Quelle vermutlich hinfällig).
