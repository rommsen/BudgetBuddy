---
id: infrastructure-y6atr
title: Lesende Start-Abfragen wiederholen sich bei einem Netzwerkfehler (Status 0) ein- bis zweimal, bevor der Fehler erscheint
status: done
type: bug
context: infrastructure
created: 2026-10-24
completed:
depends_on: []
blocks: []
tags: [captured, pwa, netzwerk, firefox]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Live-Befund 2026-10-24 (Firefox am PC, nach einem Deploy): Beim Neuladen zeigt das
Kassenbuch öfter "Konten konnten nicht geladen werden" und den Toast "Failed to load
settings" mit "Http error (0)". Der Server hat dieselben Anfragen in etwa 1 ms mit 200
beantwortet, der Tailscale-Proxy meldet dazu "context canceled": Der Browser hat die
Verbindung abgebrochen. Nach etwa drei Versuchen oder mit Cmd+Shift+R (am Service Worker
vorbei) geht es. Ein einmaliger Abbruch lässt die Seite bisher dauerhaft im Fehlerzustand.

## What
Ein Client-Helfer wiederholt eine lesende Abfrage, wenn sie mit einem Netzwerkfehler
(HTTP-Status 0, also kein Antwort-Status vom Server) scheitert: höchstens zwei weitere
Versuche nach kurzer Pause, danach erscheint der Fehler wie bisher. Er umhüllt die vier
lesenden Abfragen des Kassenbuch-Starts:

- `Api.settings.getSettings` in `src/Client/Components/Settings/State.fs` (`LoadSettings`)
- `Api.ledger.getLedgerAccounts` in `src/Client/Components/Ledger/State.fs`
- `Api.ledger.getLastMirrorRun` in `src/Client/Components/Ledger/State.fs`
- `Api.ynab.getCategories` im Zweig `LedgerMsg LoadCategories` in `src/Client/State.fs`

Schreibende Aufrufe werden nie wiederholt (ein abgebrochener Schreibaufruf kann beim
Server angekommen sein).

Aufbau, damit es unter .NET testbar bleibt (der Fable.Remoting-Proxy ist es nicht):
der Helfer ist generisch über `Async`, bekommt die Fehler-Einordnung ("ist das Status 0?")
und die Pause als Parameter. Die Fable-spezifische Einordnung (`ProxyRequestException` mit
`StatusCode = 0`) ist eine eigene kleine Funktion, die nur im Browser läuft.

## Acceptance criteria
- [ ] Der Helfer wiederholt eine Abfrage, die mit einem als Netzwerkfehler eingeordneten
      Fehler scheitert, höchstens zweimal; gelingt ein Versuch, kommt dessen Ergebnis zurück
      (Test mit einer Fake-Abfrage, die zweimal scheitert und beim dritten Mal liefert).
- [ ] Scheitern alle drei Versuche, kommt der Fehler des letzten Versuchs unverändert zurück
      (Test).
- [ ] Ein Fehler, den die Einordnung nicht als Netzwerkfehler erkennt (zum Beispiel HTTP 500
      oder eine Ausnahme mit anderem Status), wird nicht wiederholt: genau ein Aufruf (Test mit
      Aufrufzähler).
- [ ] Ein `Ok`- oder fachliches `Error`-Ergebnis der Abfrage (zum Beispiel `Result.Error` aus
      `getCategories`) wird nicht wiederholt: genau ein Aufruf (Test).
- [ ] Die Pausen kommen über den Parameter; die Tests laufen ohne echte Wartezeit. Die
      Standardpausen summieren sich auf höchstens 2 Sekunden (zum Beispiel 400 ms und 1200 ms).
- [ ] Die vier oben genannten Abfragen laufen über den Helfer; kein schreibender Aufruf
      (`set*`, `save*`, `update*`, `delete*`, `create*`, `import*`, Sync-Befehle) läuft darüber
      (prüfbar im Diff).
- [ ] Die Tests tragen einen Kommentar, welcher Befund (Firefox, "Http error (0)",
      2026-10-24) damit verhindert wird.
- [ ] `dotnet build` und `dotnet test` grün.

## Notes
Captured via `quick-capture` on 2026-10-24, verfeinert am 2026-10-24.

Die Wiederholung behebt nur die Folge, nicht die Ursache der Abbrüche. Ursache offen
(Verdacht: Service Worker in Firefox, eventuell im Zusammenspiel mit dem sofortigen Neuladen
aus infrastructure-bnbdc, ADR 0057). Der Playwright-Wächter für den PWA-Wechsel ist
`infrastructure-aaryk`.

Überschneidung: `ledger-k9tq7` und `ledger-dmw83` ändern denselben Zweig
`LedgerMsg LoadCategories` in `src/Client/State.fs`. Nicht parallel bauen; diese Task zuerst.

Andere Aufrufer von `getSettings` (SyncFlow, Rules) bleiben in dieser Task unberührt. Ob der
Helfer später breiter genutzt wird, entscheidet sich, wenn der Befund dort auftritt.

## Outcome

Neuer Client-Helfer `Retry` in `src/Client/Retry.fs` (nach `Debounce.fs` in `Client.fsproj`). `Retry.withRetry` ist generisch über `Async`: Einordnung (`exn -> bool`), Pausenliste und `sleep` sind Parameter. Ergebnisse, auch fachliche `Error`-Werte, werden nie wiederholt; scheitert der letzte Versuch, kommt dessen Ausnahme unverändert. `Retry.isNetworkError` erkennt `ProxyRequestException` mit `StatusCode = 0`. `Retry.reading` umhüllt eine Abfrage mit den Standardwerten (400 ms, 1200 ms, Summe 1,6 s). Umhüllt sind `getSettings` (Settings/State.fs, `LoadSettings`), `getLedgerAccounts` und `getLastMirrorRun` (Ledger/State.fs) sowie `getCategories` im Zweig `LedgerMsg LoadCategories` (State.fs, nur das Umhüllen der Abfrage; der Rules-Zweig und SyncFlow bleiben unberührt). In Settings/State.fs und State.fs kam `open Client` dazu.

Tests: `src/Tests/RetryTests.fs` (8 Tests, Kommentar nennt den Befund Firefox "Http error (0)" 2026-10-24): zweimal scheitern, dritter liefert; alle drei scheitern (Fehler des letzten); HTTP 500 und fremde Ausnahme genau ein Aufruf; Ok und fachliches Error genau ein Aufruf; Standardpausen höchstens 2 s und zwei Stück; `isNetworkError` für Status 0, 500, 404, fremde Ausnahme; Quelltext-Zusicherung, dass genau diese vier Abfragen über `Retry.reading` laufen. Pausen werden per Parameter aufgezeichnet, keine echte Wartezeit.

Läufe: `dotnet build` 0 Warnungen, 0 Fehler; `dotnet test` 1886 bestanden, 0 Fehler, 7 übersprungen; `npm run build` ok; `npm run guard:classes` ok; `npm run test:scripts` (seriell) 198 von 198 bestanden.
