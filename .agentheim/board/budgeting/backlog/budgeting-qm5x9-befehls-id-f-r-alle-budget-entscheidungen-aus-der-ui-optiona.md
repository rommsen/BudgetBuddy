---
id: budgeting-qm5x9
title: "Befehls-Id für alle Budget-Entscheidungen aus der UI: optionales CommandId-Feld, Prüfung gegen processed_commands unter der Sperre, Antwort aus dem Stream, Client wiederholt einmal"
status: backlog
type: feature
context: budgeting
created: 2026-11-05
completed:
depends_on: [budgeting-hyn77]
blocks: []
tags: [idempotenz, remoting, client, fuehrung]
related_adrs: [0083, 0084]
related_research: []
prior_art: [budgeting-afd8h, budgeting-c5w2n]
---

## Why
Zuweisen setzt einen absoluten Wert. Kommt eine Wiederholung verspätet an, setzte sie einen
inzwischen späteren Wert zurück. Ein Verschieben könnte doppelt buchen. ADR 0084 Punkt 8 gibt
deshalb allen UI-Entscheidungen im Budget eine Befehls-Id (Grundlage ADR 0083 Punkt 3).

## What
- `CommandId: Guid option` in `MoveRequest`, `AssignRequest`, `TargetPauseRequest`,
  `TargetChangeRequest` (`src/Shared/Budgeting.fs:343-428`); `applyTargets` bekommt einen
  Request-Record oder ein Feld in `TargetPreview`. `retryFailedTargets` bekommt keins.
- Formatprüfung an der API-Grenze (`src/Server/Validation.fs`).
- Unter der Sperre des Befehls zuerst gegen `processed_commands` prüfen, vor dem YNAB-Aufruf.
  Steht die Id dort, entsteht kein Ereignis; die Antwort baut der Server aus dem Stream
  (`command_id` → Entscheidungsereignis → `reportWith`, `CommandHandlers.fs:115`).
- Ergebnis-Ereignisse tragen keine `command_id`.
- Client: eine Id je Absicht, nach Status 0 einmal automatisch mit derselben Id wiederholen
  (`Retry.writing` neben `Retry.reading`, `src/Client/Retry.fs:45`).

## Acceptance criteria
- [ ] Test: Fable.Remoting liest einen Request ohne `CommandId`-Feld als `None` (alter Client),
      und der Server verhält sich dann wie heute.
- [ ] GIVEN ein Zuweisen mit Id X ist festgehalten, WHEN derselbe Request mit Id X erneut kommt,
      THEN entsteht kein Ereignis, YNAB wird nicht aufgerufen (Port zählt null Aufrufe), und die
      Antwort entspricht der ersten.
- [ ] GIVEN ein Zuweisen mit Id X auf 50 € und danach ein Zuweisen mit Id Y auf 60 €, WHEN der
      Request X verspätet wiederholt wird, THEN bleibt das Soll 60 €.
- [ ] Dasselbe Paar (Wiederholung ohne neues Ereignis, Antwort gleich) je Befehl: Verschieben,
      Targets anwenden, Pausieren und Aufheben, Target ändern.
- [ ] Ein Befehl ohne Ereignis (etwa YNAB steht schon auf dem Wert) hinterlässt keine Zeile in
      `processed_commands`; seine Wiederholung wird neu ausgewertet.
- [ ] Ergebnis-Ereignisse haben `command_id` leer (Test im Handler).
- [ ] Eine ungültige Id wird an der API-Grenze mit konkretem Fehler abgelehnt.
- [ ] Client-`update` unter .NET: Nach Status 0 wird genau einmal mit derselben Id wiederholt; eine
      neue Absicht bekommt eine neue Id (reine Funktion oder Lambda statt `Client.Api`).
- [ ] Die Fixtures in `scripts/*.test.mjs` werden im selben Schritt um das neue Feld ergänzt.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Quelle: ADR 0084 Punkt 8, ADR 0083 Punkt 3.
- Target ändern läuft unter `TargetGuard`, nicht unter `MonthGuard`; dort prüft der Befehl unter
  `TargetGuard` gegen `processed_commands`.
- Sinnvoll vorher, kein Blocker: infrastructure-aaryk (alter Client gegen neuen Server).