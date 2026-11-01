---
id: banking-import-bftq0
title: "Status der Bankverbindung sichtbar, Bank trennen"
status: done
type: feature
context: banking-import
created: 2026-10-30
completed:
depends_on: [banking-import-tshq9]
blocks: [banking-import-t4raw, banking-import-r9tfx]
tags: [backend, frontend, comdirect, session]
related_adrs: [0071]
related_research: []
prior_art: []
---

## Why
Hintergrundlogik darf nicht still ausfallen. Roman muss sehen, ob der nächste Sync eine TAN braucht,
und die Verbindung selbst beenden können.

## What
- `getBankConnectionStatus` liefert `Disconnected`, `ChallengePending` oder `Connected` als eigenen
  Typ in `src/Shared/`, ohne Tokens und ohne Bankzugriff.
- `disconnectBank` ruft das vorhandene `disconnectWith`.
- Anzeige in den Einstellungen und im SyncFlow, Knopf "Bank trennen" in den Einstellungen.

Die Meldung per `notify` bei einem Verlust ist `banking-import-r9tfx`.

## Acceptance criteria
- [ ] GIVEN `Connected` WHEN `getBankConnectionStatus` THEN der Status lautet verbunden, kein Bankzugriff.
- [ ] GIVEN `ChallengePending` WHEN `getBankConnectionStatus` THEN der Status nennt die offene TAN und zählt nicht als verbunden.
- [ ] GIVEN "Bank trennen" THEN kein `ReportLoss`, `Disconnected`, der gespeicherte Wert ist gelöscht.
- [ ] GIVEN das Löschen des gespeicherten Werts scheitert WHEN "Bank trennen" THEN Roman sieht einen Fehler, statt dass ein Token still liegen bleibt.
- [ ] GIVEN eine Sitzung in `AwaitingBankAuth` WHEN "Bank trennen" THEN `confirmTan` scheitert sichtbar und die Sitzung bleibt nicht in `AwaitingBankAuth` stehen.
- [ ] GIVEN eine Sitzung in `ReviewingTransactions` WHEN "Bank trennen" THEN die Prüfung bleibt offen.
- [ ] Der Status enthält keine Tokens (Vorbild `src/Tests/SettingsSecretsTests.fs`).
- [ ] `src/Tests/RemotingEndpointNegativeTests.fs` hat eine Zeile je neuem Endpoint, Fixtures in `scripts/*.test.mjs` sind im selben Schritt nachgezogen.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Die Anzeige ist am Handy und am PC geprüft. [human-eye]

## Notes
Geteilt am 2026-11-01: Status und Trennen brauchen keinen Push, deshalb hängt dieser Task nicht mehr
an `infrastructure-jsys4`.

`disconnectWith` wartet am Bank-Gate, ein laufender Abruf endet also zuerst. Der Status liest nur den
Zustand und ruft nicht `awaitResumeWith`, das könnte bis zu 100 s an der Bank hängen. Direkt nach einem
Neustart kann er deshalb kurz `Disconnected` zeigen, bis der Hintergrunddienst die Verbindung
wiederaufgenommen hat.

Design-System: nichts Neues. Der Styleguide nennt `Badge` für persistenten Status, Toast ist dafür
verboten, Texte nach §8. Vor dem Bau gegen `/styleguide` prüfen, fehlt eine Variante, geht ein
Follow-up an `design-system`.

## Verifier note (iteration 1)

REASONS:
- Check 1, Kriterium `AwaitingBankAuth`: Die Zeile `serialized ports (...)` in `disconnectBankWith` (`src/Server/BankImport.fs:222`) hat keinen Test, der rot wird. Ein Mutant, der sie durchreicht, lässt die ganze Suite grün.
- Das ist ein wörtlicher Fall des Kriteriums: `StartNew` legt die Sitzung in `AwaitingBankAuth` an, sie bleibt dort während `Bank.Resume` (bis 100 s). Ohne Gate setzt das Trennen `Failed`, danach überschreibt `requireTan` das mit `AwaitingTan` und startet nach dem Trennen eine Challenge.
- Beweis mit einem Wegwerf-Test in einer Kopie: grün auf dem Code des Workers, rot auf dem Mutanten (`actual: Some AwaitingTan`). Der Code ist richtig, nur der Test fehlt.
- Die vorgeschlagene "Invariante Trennen" im README-Delta beschreibt dieses Verhalten ohne Test dahinter.
- Kein Befund: der fehlende Rotlauf als solcher. 10 von 11 Mutanten wurden getötet.

SUGGESTED_FIX: Verhaltenstest in `disconnectBankTests` (`src/Tests/BankImportTests.fs`): Sitzung in `AwaitingBankAuth`, `startSyncWith` per `Barrier` in `Bank.Resume` gehalten, `disconnectBankWith` starten, freigeben, dann `Failed "Bank getrennt. Sync neu starten."` und `Disconnected` prüfen. Er muss ohne `serialized ports` rot sein. Den Absatz "Grenzen" im Outcome nachziehen.

ITERATION_HINT: likely-fixable

## Outcome

`getBankConnectionStatus` und `disconnectBank` liegen in `SyncApi` (`src/Shared/Api.fs`), weil `liveImportPorts` dort verfügbar ist. Der eigene Typ `BankConnectionStatus` steht in `src/Shared/Domain.fs`, seine Fälle tragen das Präfix `Bank`, damit sie nicht mit `Server.BankConnection` kollidieren. Gelesen wird nur der Zustand (`connectionStatusWith`).

Der Ablauf zum Trennen ist `disconnectBankWith` in `src/Server/BankImport.fs`. Er läuft am `ImportPorts.Gate` und ruft `disconnectWith`. Eine Sitzung in `AwaitingBankAuth` oder `AwaitingTan` endet als `Failed`, `ReviewingTransactions` bleibt. Ein gescheitertes Löschen liefert eine Meldung ohne Ausnahmetext, die Sitzung endet trotzdem.

Anzeige: `src/Client/Components/BankConnectionBadge.fs`, genutzt in den Einstellungen und in der Startansicht des SyncFlow. "Bank trennen" fragt einmal nach ("Wirklich trennen"), die Fehlermeldung bleibt per `ErrorDisplay.cardCompact` stehen, und der Knopf bleibt für den zweiten Versuch. Der Kopf der Comdirect-Karte sagt jetzt "Eingerichtet" statt "Verbunden", sonst stünde "Verbunden" neben "Nicht verbunden".

Live-Anschlüsse: die neuen Endpoints und `connectionStatus` hängen im Strukturwächter (`src/Tests/StructureGuardTests.fs`) mit Gegenbeweisen. Die Negativzeile für `disconnectBank` läuft über `disconnectBankWith` mit eigener Sitzungsansicht, weil das Feld die Prozess-Sitzung parallel laufender Tests beenden könnte.

Grenzen: Die Tests aus Iteration 1 entstanden im selben Schritt wie der Code, einen Rotlauf je Kriterium gab es dort nicht. Dass `disconnectBankWith` am `ImportPorts.Gate` wartet, belegt seit Iteration 2 der Test "ein Start, der noch auf die Wiederaufnahme wartet, kann das Trennen nicht überholen" in `src/Tests/BankImportTests.fs`. Rotlauf: Mit `id` statt `serialized ports` in `disconnectBankWith` scheiterte er in 8 von 8 Läufen, fünf davon mit ausgewerteter Meldung: `expected: Disconnected`, `actual: ChallengePending`, die Sitzung stand dort schon auf `Failed`. Das `actual: Some AwaitingTan` aus der Verifier-Notiz trat in diesen Läufen nicht auf, der Test prüft beide Ausgänge. Geprüft wurde nur dieser eine Mutant, der Produktionscode ist in Iteration 2 unverändert. Der Test enthält eine feste Pause von 100 ms, ohne sie wäre nicht sicher, dass das Trennen schon wartet. Das Trennen wartet auch auf einen Start, der an der Bank hängt (bis 100 s).

Offen: Die Prüfung am Handy und am PC [human-eye] steht aus. Ein Browser-Wächter prüft den Ablauf in 1440 und 390 px Breite samt seitlichem Überlauf, das ersetzt den Blick nicht. `./verify.sh`: `VERIFY: PASS` (dotnet 3228 grün, node 422 grün).
