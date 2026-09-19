---
id: ledger-fq3kd
title: Force-Import (Duplikat erneut pushen) schreibt noch nicht ins Kassenbuch
status: done
type: feature
context: ledger
created: 2026-09-11
depends_on: []
blocks: []
tags: [ledger, ynab-sync, push, integration]
related_adrs: [0021]
related_research: []
prior_art: [ledger-qvt84]
---

## Why
`ledger-qvt84` hat den normalen Import-Push (`importToYnab`) auf "erst Kassenbuch, dann
YNAB, dann Verknüpfung" umgestellt (ADR 0021). `forceImportDuplicates` (der
"trotzdem importieren"-Knopf für YNAB-Duplikate) ruft weiterhin direkt
`YnabClient.createTransactions` auf und schreibt keine Kassenbuch-Buchung. Eine per
Force-Import angelegte YNAB-Buchung taucht damit erst über den nächsten Spiegel-Lauf mit
falscher Herkunft ("YNAB-Spiegel" statt "Comdirect-Import") im Kassenbuch auf, genau das
Muster, das ADR 0021 für den normalen Import bereits vermeidet.

`ledger-qvt84`s Task-Text ("Was") nennt ausdrücklich nur `importToYnab`; Force-Import blieb
deshalb bewusst außerhalb des Scopes dieses Tasks.

## What
`forceImportDuplicates` (`src/Server/Api.fs`) muss die schon vorhandene Kassenbuch-Buchung
**wiederverwenden und neu verknüpfen**, nicht `pushComdirectImports` mit einer neuen
ImportId aufrufen (siehe Notes, Verifier-Fund Iteration 2 von `ledger-qvt84`): den
Comdirect-Import-Push-Pfad ist bereits so gebaut, dass die Kassenbuch-Buchung mit der
**alten, deterministischen** ImportId (`Shared.Domain.generateImportId txId`) schon vor
YNABs Duplikat-Meldung entstand (Kassenbuch zuerst, ADR 0021). Force-Import muss also:
1. die bestehende Buchung über `findEntryByLink ImportIdSystem` (alte, deterministische
   ImportId) finden,
2. mit `YnabClient.prepareTransactions ... forceNewImportId = true` senden (neue ImportId
   nur für YNABs Dedup, nicht für die Kassenbuch-Suche),
3. bei Erfolg die **gefundene** Buchung mit `EntryLinkedToYnab` verknüpfen (kein neues
   `EntryImportedFromComdirect`, keine neue Buchungs-Id).

## Acceptance criteria
- [ ] Ein Force-Import verknüpft die schon bestehende Kassenbuch-Buchung (über die alte,
      deterministische ImportId gefunden) mit der neuen YNAB-Id, statt eine zweite
      "Comdirect-Import"-Buchung anzulegen.
- [ ] Existiert (Randfall) noch keine Kassenbuch-Buchung für die alte ImportId, entsteht
      genau eine, wie beim normalen Import-Push.
- [ ] Ein zweiter Force-Import-Versuch erzeugt keine zweite Buchung.
- [ ] Test mit injiziertem Push, in-memory SQLite.
- [ ] `dotnet build`, `dotnet test` grün.

## Notes
**Wichtig (Verifier-Fund, `ledger-qvt84` Iteration 2):** der normale Push hat, bevor YNAB
die Buchung als Duplikat meldet, bereits eine unverknüpfte Kassenbuch-Buchung mit der
**alten** ImportId geschrieben (`pushComdirectImports` schreibt immer zuerst, ADR 0021).
Ruft Force-Import `pushComdirectImports` einfach mit einer **neuen** ImportId auf (wie
`YnabClient.prepareTransactions ... forceNewImportId = true` sie erzeugt), findet
`findEntryByLink ImportIdSystem neueImportId` nichts (andere ImportId als die alte!) und
legt eine **zweite** "Comdirect-Import"-Buchung an, obwohl die erste schon existiert. Der
Folgetask darf `pushComdirectImports` deshalb nicht unverändert wiederverwenden; er muss
die bestehende Buchung über die alte ImportId finden und nur nachträglich verknüpfen
(neue Fassade oder ein Parameter, der eine externe "diese Buchung existiert schon"-Suche
statt der eingebauten ImportId-basierten Suche erlaubt). Auch die Review-Spur ist beim
Force-Import-Fall leer (die Buchung war ja schon vorher kategorisiert und abgelehnt); das
ist beim Wiederverwenden zu berücksichtigen (kein zweiter Kategorisierungs-Versuch nötig,
die Kategorie steht schon in der bestehenden Buchung).

## Outcome

`forceImportDuplicates` (`src/Server/Api.fs`) schrieb bisher nicht ins Kassenbuch: es rief
`YnabClient.createTransactions ... forceNewImportId = true` direkt auf und markierte danach
pauschal, was YNAB laut `result.Created` angelegt hatte. Das Notes-Feld dieses Tasks (Verifier-
Fund aus `ledger-qvt84` Iteration 2) hielt fest, warum eine naive Wiederverwendung von
`pushComdirectImports` mit der neuen Force-ImportId eine zweite "Comdirect-Import"-Buchung
angelegt hätte: der normale Push schreibt die Buchung schon vor YNABs Duplikat-Meldung mit der
alten, deterministischen ImportId (`Shared.Domain.generateImportId`); eine Suche über die neue
ImportId findet sie nie.

**Fix** (`Ledger.CommandHandlers.ComdirectImportCandidate`, `src/Server/Ledger/CommandHandlers.fs`):
neues optionales Feld `ExistingEntryImportId`. `pushComdirectImports` sucht jetzt über
`candidate.ExistingEntryImportId |> Option.defaultValue prepared.ImportId` statt immer über
`prepared.ImportId`; der Versand bleibt unverändert über `prepared.ImportId`. Normalfall
(`None`): Suche und Versand teilen sich weiterhin dieselbe ImportId, kein Verhaltenswechsel.
Zusätzlicher Fund beim TDD (roter Test "ein zweiter Force-Import-Versuch..."): im Randfall
"noch keine Buchung vorhanden" (AC 2) schrieb die bestehende `None`-Zweig-Logik die neue
Buchung bisher mit `prepared.ImportId` (der neuen, nur für diesen einen Versuch erzeugten
Force-ImportId). Ein späterer zweiter Force-Import-Versuch (wieder eine neue ImportId) hätte
diese Buchung über die alte ImportId nie wiedergefunden und beim Versuch, sie erneut
anzulegen, eine SQLite-`UNIQUE constraint failed` auf der Comdirect-Reference-Verknüpfung
ausgelöst (`LedgerWriteFailed`, kein stiller Erfolg, aber auch keine funktionierende zweite
Verknüpfung). Behoben: die im `None`-Zweig neu geschriebene Buchung trägt jetzt `searchImportId`
(die Such-ImportId) als `ImportId`, nicht `prepared.ImportId` — ein späterer Versuch findet sie
zuverlässig wieder.

`Server.Api.forceImportDuplicates` löst jetzt (wie `importToYnab`, ADR 0021) zuerst das
Kassenbuch-Konto zum Import-Konto auf (`Ledger.Persistence.findAccountByYnabAccountId`; fehlt
es, klare Fehlermeldung statt eines erfundenen Kontos), löst Transfer-Payees auf
(`resolveTransferPayees`), bereitet mit `forceNewImportId = true` vor (`prepareTransactions`)
und baut je vorbereiteter Buchung einen `ComdirectImportCandidate` mit
`ExistingEntryImportId = Some (Shared.Domain.generateImportId p.TransactionId)`. Der
eigentliche Kassenbuch-zuerst-Push-Verknüpfung-Ablauf läuft über
`Ledger.CommandHandlers.pushComdirectImports` (injizierter `push = sendPrepared`, Muster wie
`importToYnab`). Importiert markiert wird, was `Linked` oder `AlreadyImported` ist. Payee/Memo
kommen wie beim normalen Push aus dem tatsächlich vorbereiteten Request
(`p.Request.PayeeName`/`p.Request.Memo`), nicht aus den rohen Bank-Feldern (Verifier-Lehre aus
`ledger-qvt84` Iteration 3).

**Tests** (TDD; die drei neuen Testfälle per Sabotage rot beobachtet — Suche testweise wieder
auf `prepared.ImportId` zurückgesetzt, `dotnet test` gezeigt, dass zwei der drei Tests dabei
mit `LedgerWriteFailed` statt `Linked`/`AlreadyImported` fehlschlagen, danach wiederhergestellt),
alle in `src/Tests/LedgerTests.fs` (`ledgerImportPushCommandTests`, `pushComdirectImports`,
in-memory SQLite):
- "Force-Import verknüpft die schon bestehende Buchung über die alte ImportId, statt eine
  zweite anzulegen" — Hauptregressionstest (AC 1): eine über den normalen Push (mit
  gescheitertem Sende-Versuch) geschriebene, unverknüpfte Buchung wird von einem
  Force-Import-Versuch mit neuer ImportId wiedergefunden, gesendet und verknüpft; genau
  `EntryImportedFromComdirect`, `EntryLinkedToYnab` im Log, keine zweite Entstehung.
- "Force-Import ohne bestehende Buchung legt genau eine neue an" (AC 2, Randfall).
- "ein zweiter Force-Import-Versuch erzeugt keine zweite Buchung" (AC 3): deckte den oben
  beschriebenen zusätzlichen Fund auf.

`PushFixtures.candidate` (LedgerTests.fs) und der direkt konstruierte `ComdirectImportCandidate`
in `LedgerMirrorTests.fs` um `ExistingEntryImportId = None` ergänzt (Normalfall, kein
Verhaltenswechsel); neuer Helfer `PushFixtures.forceCandidate` für die drei neuen Tests.

**Verifikation:** `dotnet build BudgetBuddy.sln` grün, 0 Warnungen. `dotnet test
src/Tests/Tests.fsproj` grün: 919 bestanden (3 neu), 7 übersprungen (extern), 0 Fehler.
`npm run build` grün.

**Diary-Eintrag (für die Session-Sammelnotiz):**

## 2026-09-19 - Force-Import verknüpft die bestehende Kassenbuch-Buchung, statt eine zweite anzulegen

**What I did:** `pushComdirectImports` bekommt ein optionales `ExistingEntryImportId` auf
`ComdirectImportCandidate`, über das die Kassenbuch-Suche von der Versand-ImportId abweichen
kann. `forceImportDuplicates` nutzt das jetzt: sucht über die alte, deterministische ImportId,
sendet mit einer neuen (`forceNewImportId = true`), verknüpft die gefundene Buchung neu, statt
eine zweite "Comdirect-Import"-Buchung anzulegen (ADR 0021, Verifier-Fund aus `ledger-qvt84`).
Ein zusätzlicher Fund beim TDD: der Randfall "noch keine Buchung vorhanden" schrieb die neue
Buchung bisher unter der falschen (neuen statt der gesuchten) ImportId, wodurch ein zweiter
Force-Import-Versuch sie nie wiedergefunden hätte — behoben, mit Regressionstest.

**Files Modified:**
- `src/Server/Ledger/CommandHandlers.fs` — `ComdirectImportCandidate.ExistingEntryImportId`;
  `pushComdirectImports` sucht darüber statt immer über `prepared.ImportId`; neu geschriebene
  Buchungen tragen die Such-ImportId, nicht `prepared.ImportId`.
- `src/Server/Api.fs` — `forceImportDuplicates` läuft jetzt über
  `Ledger.CommandHandlers.pushComdirectImports` (Kassenbuch-Konto auflösen, Transfer-Payees,
  `prepareTransactions ... true`, `ExistingEntryImportId` je Kandidat); normaler Import-Push
  setzt `ExistingEntryImportId = None` explizit.
- `src/Tests/LedgerTests.fs`, `src/Tests/LedgerMirrorTests.fs` — neues Feld ergänzt, drei neue
  Testfälle, neuer `PushFixtures.forceCandidate`-Helfer.

**Files Added/Deleted:** keine.

**Rationale:** ADR 0021 verlangt für jeden BB-geführten Push dasselbe Muster
(Kassenbuch-zuerst); Force-Import war die einzige verbliebene Lücke. Die Notes dieses Tasks
(Verifier-Fund) hatten die Falle einer naiven Wiederverwendung schon benannt: ein Parameter,
der Suche und Versand entkoppelt, war die im Task selbst vorgeschlagene, einfachste Lösung.

**Outcomes:**
- Build: grün, 0 Warnungen (`dotnet build BudgetBuddy.sln`).
- Tests: 919 bestanden (3 neu), 7 übersprungen (extern), 0 Fehler
  (`dotnet test src/Tests/Tests.fsproj`). `npm run build` grün.
- Issues: keine. Alle drei neuen Tests per Sabotage rot beobachtet, danach wiederhergestellt.
