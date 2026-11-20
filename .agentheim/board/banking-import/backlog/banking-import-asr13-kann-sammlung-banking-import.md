---
id: banking-import-asr13
title: "Kann-Sammlung banking-import"
status: backlog
type: chore
context: banking-import
created: 2026-11-07
completed:
depends_on: []
blocks: []
tags: [kann-sammlung, tests]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Kann-Sammlung des BC nach der Regel Backlog-Disziplin in CLAUDE.md. Funde ohne Geld-, Daten-, Sicherheits- oder Alltagsfehler stehen hier als Zeile. Sie wird nie automatisch gebaut. Angelegt bei der Fahrplan-Zuordnung vom 2026-11-07 (ADR 0086).

## What
Die aufgenommenen Tickets stehen unten vollständig. Geschnitten und priorisiert wird im Refinement.

## Acceptance criteria
- [ ] Roman hat die Zeilen priorisiert und geschnitten, bevor etwas davon gebaut wird.

## Aufgenommen aus banking-import-w7k3m (Fahrplan-Zuordnung 2026-11-07)
**Integrationstest mit echten Schlüsseln und falschem Passwort entfernen** (chore, erfasst 2026-10-30)

### Why
"handles invalid credentials gracefully" in `src/Tests/ComdirectIntegrationTests.fs` meldet sich mit echten API-Schlüsseln und falschem Passwort an. Punkt 12 der Test-Doktrin verbietet das. banking-import-cx4b7 hat den Test nur auf `initOAuth` begrenzt und neben der Sonde abgeschaltet.

### What
Den Testfall entfernen. Falls das Verhalten bei abgelehnter Anmeldung abgesichert bleiben soll, einen Test gegen einen Fake-Handler mit `initOAuthWith` schreiben.

### Acceptance criteria
- [ ] GIVEN `src/Tests` THEN kein Testfall meldet sich mit echten Schlüsseln und falschem Passwort an.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Aus dem Review-Lauf nach design-system-dak16 (2026-11-08)
- Import prüfen: `src/Client/Components/SyncFlow/State.fs:569` schluckt einen Ladefehler der Lena-Flag-Farbe (`fun _ -> LenaFlagColorLoaded None`), und `SyncFlow.Types.LenaFlagColor` ist `string option` statt `RemoteData`. Das Kassenbuch macht es richtig (`RemoteData` plus `lenaColorLoadFailedReason`); hier nachziehen.
- Review-Lauf 2026-11-11 (design-system-k6kfw), Roman: Im Sync-Ablauf stehen in der Prüfung zwei Rückwege übereinander (oben „Zurück“ zur Übersicht, darunter der alte Pfeil, der die Prüfung abbricht). Den Abbrechen-Pfeil entfernen, Abbrechen als eigener neutraler Knopf in der Prüfung. Entfällt, wenn `ledger-v6s7c` (Durchgehen) den Sync-Ablauf ablöst.

## Aus banking-import-d0fey (2026-11-12)
- Die Tabelle `sync_transactions` liest und schreibt niemand mehr; `TransactionStatus` (`ManualCategorized`, `Imported`) ist im Abruf weitgehend tot. Aufräumen samt Migration.
- Quick Add zeigt keine zuletzt genutzten Kategorien mehr, sie kamen nur aus dem Sync-Ablauf.
- Der Satz „<n> sichere Duplikate übersprungen“ zählt auch Umsätze, die schon vor BB in YNAB standen; bei jedem Abruf im Zeitraum kommen sie wieder.
- Die beiden Punkte oben aus den Review-Läufen 2026-11-08 und 2026-11-11 zum Sync-Ablauf (Lena-Farbe in `SyncFlow/State.fs`, zwei Rückwege) sind mit dem Ablauf entfallen.
- Review-Lauf 2026-11-12: `MatchYnabTransactionId = ""` im Rückfall von `holdOf` ist ein Platzhalter; langfristig sollte die Duplikaterkennung den Treffer selbst mitgeben (Shared-Typ). ledger-r7h2w muss das leere Feld beachten.
- Review-Lauf 2026-11-12: Abbrechen und sofort wieder „Abrufen“, solange `cancelSync` unterwegs ist: das Lesen kann die gerade abgebrochene Sitzung noch mit TAN liefern, „Ich habe bestätigt“ scheitert dann mit „Der Abruf ist nicht mehr offen“ (keine zweite Challenge).
- Review-Lauf 2026-11-12: Wächter ohne erreichbaren Weg (Versionssprünge in `startWith`, `SyncStarted`, `ConfirmTan`, `ReadPlanned`, Vergleich von `ReadsLeft`) bleiben als Absicherung, Mutanten dort bleiben grün.
- banking-import-t4raw (2026-11-12): Die Fälligkeit des automatischen Abrufs zählt jeden gescheiterten Eintrag in `sync_sessions`, auch eine von Hand abgelehnte TAN; `sync_sessions` unterscheidet Bankfehler nicht von anderen. Folge: höchstens ein später automatischer Lauf.
- banking-import-t4raw: Zwischen „Bank trennen“ und dem Block „Automatisch abrufen“ steht keine Trennlinie.
- banking-import-t4raw: Felder über `Input.group` sind nicht per `for` mit ihrer Bezeichnung verbunden (auch das Auswahlfeld „Alle“).
- banking-import-t4raw: `[flow:bank-trennen]` reagiert empfindlich auf Layout unter dem Status (Rückfrage fällt nach 3 s zurück, während der Knopf wandert).
- banking-import-t4raw: Die README nennt unter **Wiederaufnahme** „den Hintergrunddienst“ im Singular (gemeint `BankConnectionWorker`), seit `AutoFetchWorker` mehrdeutig.
- Review-Lauf 2026-11-12: Gegenrichtung der Speichersperre in den Einstellungen offen: schaltet jemand „Automatisch abrufen“ um, während das Speichern der Sync-Karte noch unterwegs ist, gehen die geänderten Tage mit dem alten Stand hinaus (Fenster eine Server-Antwort lang; braucht einen Laufzustand für die Sync-Karte).
- Review-Lauf 2026-11-12: `autoFetchErrorText` erkennt Validierungsfehler am String `DatabaseError ("validation", …)`; ein eigener `SettingsError`-Fall für Validierung wäre robuster.
- banking-import-pknvp (2026-11-13): `Shared.Domain.YnabTransaction` geht nicht über Remoting und könnte ein reiner Server-Typ werden; dann fiele das neue Feld `TransferAccountId` aus `src/Shared`.
- Work-Lauf 2026-11-20 (Verifier ledger-s2cg3): `[flow:bank-trennen]` in `scripts/settings-secrets.test.mjs` war unter Last in mehreren Worktrees einmal rot, weil die Navigation unten den Klick auf „Wirklich trennen“ abfängt; allein wiederholt grün. Knopf vor dem Klick in den Blick scrollen oder die Leiste berücksichtigen.
- Work-Lauf 2026-11-20 (banking-import-vwhfm): die Hilfezeile von `Input.group` („Gesetzt. Leer lassen, um den Wert zu behalten“, `text-text-muted/70`) hat 3,23:1 Kontrast; Ausnahme im Sweep-Zustand `einstellungen-fehler` (Invariante 10).
- Work-Lauf 2026-11-20 (banking-import-vwhfm): der Fehler von „Automatisch abrufen“ steht ohne „Nicht gespeichert:“, die drei anderen Felder mit (bewusst unverändert aus banking-import-t4raw).
