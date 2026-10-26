---
id: ynab-sync-d9e51
title: "Lena fragen schon beim Import: eine Aktion an der einzelnen Zeile in Import prüfen, die Buchung geht mit der Lena-Flag-Farbe im Import-Push nach YNAB (auch ohne Kategorie) und steht im Kassenbuch sofort als Rückfrage Lena"
status: done
type: feature
context: ynab-sync
created: 2026-10-26
completed:
depends_on: [design-system-001]
blocks: []
tags: [frontend, backend, import, lena, flags, kassenbuch]
related_adrs: [0021, 0056, 0061, 0064]
related_research: []
prior_art: [ynab-sync-aj5tv, ynab-sync-e4gm3]
---

## Why
Roman stößt beim Prüfen eines Comdirect-Imports oft auf Buchungen, die er nicht einordnen
kann. Heute muss er sie erst nach YNAB schicken und dann im Kassenbuch "Lena fragen"
(ledger-xfjcp, ledger-y54gr). Er will die Rückfrage gleich beim Import setzen, in einem
Schritt (Roman, 2026-10-26).

## What
Entscheidungen von Roman (2026-10-26): Aktion **an der einzelnen Importzeile** (keine
Sammelaktion für eine Auswahl im Import), **auch ohne Kategorie** erlaubt, das Flag geht
**direkt im Import-Push** mit (kein zweiter Aufruf nach dem Anlegen).

1. **Aktion an der Zeile.** In "Import prüfen" trägt jede offene Zeile (Status `Pending`,
   `AutoCategorized`, `ManualCategorized`, `NeedsAttention`) eine Aktion "Lena fragen" bzw.
   bei gesetzter Rückfrage "Rückfrage zurücknehmen". Der Zustand hängt an der
   Sitzungs-Transaktion (`SyncTransaction`), über einen eigenen Befehl der SyncFlow-API.
   Überspringen hebt die Rückfrage auf. Eine Zeile mit Rückfrage zeigt das Kennzeichen
   "Rückfrage Lena" wie im Kassenbuch. Ohne gesetzte Flag-Farbe (`lena_flag_color`,
   ADR 0056/0061) ist die Aktion gesperrt und nennt den Grund.
2. **Ohne Kategorie.** Eine Zeile ohne Kategorie mit Rückfrage geht wie heute als
   `Pending` mit (der Push nimmt `Pending` schon heute mit); die Rückfrage ändert an der
   Kategorie-Logik nichts.
3. **Push.** Der Import-Push (`prepareTransactions`/Create-Body) sendet für Zeilen mit
   Rückfrage `flag_color` mit der eingestellten Lena-Farbe; Zeilen ohne Rückfrage senden
   kein `flag_color` (Body unverändert gegenüber heute). Ist bis zum Push die Farbe
   entfernt worden, geht die Zeile ohne Flag und der Bericht nach dem Import nennt das.
4. **Kassenbuch.** Nach ADR 0021 entsteht der Kassenbuch-Eintrag vor dem Push
   (`EntryImportedFromComdirect`). Er trägt bei Rückfrage Flag-Farbe und Flag-Name "Lena",
   so dass `ReadModel.flagOf` ihn sofort als *Rückfrage Lena* deutet, der Chip
   "Rückfrage Lena (n)" ihn zählt und das Detail "Rückfrage zurücknehmen" anbietet. Ein
   neues optionales Payload-Feld braucht beim Lesen alter Ereignisse eine Rückfalldeutung
   "kein Flag". Die Historie nennt die Herkunft ("Lena gefragt beim Import").
5. **Fehlschlag.** Scheitert der Push, gilt der bestehende Push-Fehlschlag ("Noch nicht in
   YNAB", ledger-cks06); "Erneut senden" schickt das Flag mit.

## Acceptance criteria
- [ ] Domain/Server-Test: der Befehl setzt und entfernt die Rückfrage an einer offenen Sitzungs-Transaktion; bei `Skipped`/`Imported` wird er abgelehnt; Überspringen hebt eine gesetzte Rückfrage auf.
- [ ] Server-Test: ohne gesetzte `lena_flag_color` lehnt der Befehl "Lena fragen" ab, mit Grund.
- [ ] YnabClient/Push-Test: eine Zeile mit Rückfrage trägt im Create-Body `flag_color` mit der eingestellten Farbe, eine Zeile ohne Rückfrage hat keinen `flag_color`-Schlüssel (Body sonst byte-gleich zu heute).
- [ ] Push-Test: eine Zeile ohne Kategorie mit Rückfrage geht mit (`Pending`), mit `flag_color` und ohne `category_id`.
- [ ] Ledger-Test: der Import-Eintrag einer Zeile mit Rückfrage trägt Farbe und Name "Lena", `flagOf` ergibt `AskLena`, `AskLenaCount` zählt ihn; ein altes Ereignis ohne das neue Feld liest sich als "kein Flag" (Codec-Test).
- [ ] Historie-Test: der Eintrag zeigt die Herkunft "Lena gefragt beim Import".
- [ ] Retry-Test: "Erneut senden" (`retryPushToYnab`) schickt bei einer Buchung mit Rückfrage `flag_color` mit.
- [ ] Reducer/Ansichts-Test: die Importzeile zeigt "Lena fragen" bzw. "Rückfrage zurücknehmen" und das Kennzeichen; ohne Farbe gesperrt mit Grund.
- [ ] Playwright (PC und Handy): in Import prüfen "Lena fragen" an einer Zeile wählen, Kennzeichen erscheint, der abgefangene Push-Aufruf trägt `flag_color` nur für diese Zeile.
- [ ] `dotnet build` (0 Warnungen), `dotnet test`, `npm run build`, `guard:classes`, `guard:tokens`, `test:scripts` grün.
- [ ] Die Aktion fügt sich in die Importzeile ein, ohne die ruhige Liste (ynab-sync-aj5tv, ADR 0049) zu stören. [human-eye]

## Notes
- Schreibweg Flag: ADR 0056 (nur `flag_color`, Name kommt aus YNABs Farbbenennung), ADR 0061 (Lena-Deutung `Ledger.Domain.isLenaFlagName`, Einstellung über `SettingsApi.getLenaFlagSettings`).
- Ablauf Kassenbuch zuerst, dann Push: ADR 0021. Import-Ereignis `EntryImportedFromComdirect` (`src/Server/Ledger/Events.fs`); Push-Auswahl in `src/Server/Api.fs` (`toImport`), Body in `prepareTransactions`.
- Berührt zwei Kontexte: `ynab-sync` (Import prüfen, Push) und `ledger` (Import-Ereignis, Lesemodell). README-Deltas für beide liefern.
- Prior art im Kassenbuch: ledger-xfjcp (Lena fragen im Detail), ledger-y54gr (Sammelaktion), ledger-cks06 (Erneut senden).
- Nicht in dieser Task: Lena fragen für eine Auswahl von Importzeilen, Quick Add.

## Verifier note (iteration 1)

**REASONS:**
- ADR-0065 (Entwurf), Entscheidung 1: "Die Flag-Farbe wird nicht an der Zeile gespeichert, sondern erst beim Push ... gelesen" ist die "nicht X, sondern Y"-Antithese, die Romans Schreibregel verbietet.
- ADR-0065 (Entwurf), Folgen, erster Punkt: der Satz zur alten installierten PWA ist verdreht und durch das "nicht" sachlich falsch. Geprüft: kein Pfad vom Client zum Server deserialisiert `SyncTransaction` oder `ImportResult`; ein alter Client überspringt die neuen Schlüssel, ein serverseitiger Rückfall ist nicht nötig.
- Optional: mildere "X, nicht Y"-Wendungen in Kommentaren (`src/Server/Api.fs`, `src/Server/Ledger/CommandHandlers.fs`) und im ADR-Kontext.
- Alles andere geprüft und grün: Kriterien auf benannte Tests abgebildet, `dotnet build` 0 Warnungen, `dotnet test` 2298/0/7, `npm run build`, `guard:classes`, `guard:tokens`, `test:scripts` seriell 247/247; Abweichung beim Playwright-Kriterium (Push läuft serverseitig) als Nachweis anerkannt.

**SUGGESTED_FIX:** Nur Text der ADR: Entscheidung 1 ohne "nicht ..., sondern ..." formulieren, Folgen-Punkt 1 richtigstellen (nur Server zum Client, alter Client überspringt die Felder, kein Rückfall nötig). Kein Code- oder Testeingriff.

**ITERATION_HINT:** likely-fixable

**Conductor:** ADR-Text im Ergebnis-JSON korrigiert (Entscheidung 1, Folgen-Punkt 1, Kontext-Satz), Code unverändert. ADR-Nummer auf 0064 gezogen, weil der Platz 0064 (vorgesehen für budgeting-h2zfn) noch frei ist und `finalizeAdrNumbering` sonst herunternummeriert; budgeting-h2zfn bekommt 0065.

## Outcome

"Lena fragen" funktioniert jetzt schon in Import prüfen, an der einzelnen Zeile, auch ohne Kategorie.

**Umsetzung**
- Shared: `SyncTransaction.AskLena`, `SyncError.AskLenaRejected`, `SyncApi.setAskLena`, `ImportResult.LenaFlagNotSentCount`.
- Server: `src/Server/ImportAskLena.fs` (reine Regeln: setzen/entfernen nur an offenen Zeilen, ohne gültige `lena_flag_color` abgelehnt mit dem Kassenbuch-Wortlaut, Überspringen hebt auf). `Server.Api.syncApi.setAskLena`, `skipTransaction` nutzt `ImportAskLena.skip`. `YnabClient.applyLenaFlag` setzt `FlagColor` im Request (nur für Zeilen mit Rückfrage; `encodeTransaction` schreibt `flag_color` nur bei `Some`, jetzt öffentlich testbar); `importToYnab` und `forceImportDuplicates` lesen die Farbe erst beim Push. `LenaFlagNotSentCount` zählt Rückfragen ohne Farbe; der Client hängt einen Hinweis an die Toast-Meldung (`importLenaFlagNote`).
- Ledger: `EntryImportedFromComdirectPayload` hat optionale `FlagColor`/`FlagName` (JSON-Schlüssel nur bei Flag, alte Ereignisse lesen sich als kein Flag); `apply` setzt sie im Zustand, `recordComdirectPush` schreibt Farbe und Name "Lena" (`ComdirectPushInput.LenaFlagColor`, aus `Prepared.Request.FlagColor`). Historie: "Comdirect-Import angelegt: ..., Lena gefragt beim Import". Erneut senden: `pushFlagColorOf`, `YnabClient.createTransactionWithFlag` und `buildManualTransactionBodyWithFlag` (die alten Funktionen delegieren mit `None`, Body unverändert).
- Client: `askLenaActionFor` (rein), Modell `LenaFlagColor` (wird mit `LoadTransactions` frisch geladen), Nachrichten `SetAskLena`/`AskLenaSet`/`LoadLenaFlagColor`, in `TransactionRow` das Kennzeichen "Rückfrage Lena" (Badge.Purple, in der Kopfzeile der Zeile) und der Aktions-Chip in der aufgeklappten Zeile, gesperrt mit sichtbarem Grund.
- Persistenz: `AskLena` ist sitzungsgebunden (ADR 0064), `getTransactionsBySession` liefert `false`.
- Bestehende Record-Literale in Tests und Mock-Fixture (`scripts/import-pruefen-duplikat-details.test.mjs`) um die neuen Pflichtfelder ergänzt (rein mechanisch).

**Tests (29 neu)**: `src/Tests/ImportAskLenaTests.fs` (17: Befehl, Create-Body byte-gleich ohne Rückfrage, Pending ohne category_id, Farbe entfernt, Kassenbuch-Eintrag mit Flag/flagOf/AskLenaCount, Historie, Fehlschlag plus Erneut senden mit Farbe, Retry-Body, Codec alt/neu), `src/Tests/ImportAskLenaClientTests.fs` (8: Aktionswahl, Reducer, Quelltext der Zeile), `scripts/import-lena.test.mjs` (4 Playwright, PC und 390 px).
Läufe aus dem Worktree: `dotnet build` 0 Warnungen/0 Fehler; `dotnet test` 2298 erfolgreich, 0 Fehler, 7 übersprungen; `npm run build` grün; `guard:classes` und `guard:tokens` grün; `npm run test:scripts` (seriell) 247 Tests, 245 grün im ersten Lauf, die 2 roten waren das Mock-Fixture ohne `AskLena` (behoben, Datei danach grün); die übrigen 245 blieben unverändert grün.

**Abweichung vom Wortlaut des Kriteriums "abgefangener Push-Aufruf trägt flag_color"**: der Push läuft serverseitig an YNAB, ein Browser-Mock sieht nur den Remoting-Aufruf `importToYnab` (ohne Body-Inhalt). Playwright belegt deshalb: `setAskLena` geht nur für die gewählte Zeile raus (Argumente geprüft), Kennzeichen nur an dieser Zeile, Zurücknehmen, gesperrt ohne Farbe mit Grund, Import wird angestoßen, keine waagerechte Überbreite. `flag_color` im Create-Body nur für diese Zeile belegen die .NET-Tests an `applyLenaFlag` + `encodeTransaction`. Keine YNAB-Aufrufe in dieser Task.

**[human-eye] Screenshots** (einmalig außerhalb der Tests, Mock-Daten): /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/04b6aafe-6e4f-485e-a143-c16e2369c195/scratchpad/ynab-sync-d9e51/pc-1440x900-import-lena.png, .../handy-390x844-import-lena.png, .../pc-1440x900-import-lena-gesperrt.png, .../handy-390x844-import-lena-gesperrt.png. Das Kennzeichen sitzt neben dem Kategorie-Chip in der Kopfzeile, die Aktion in der Chip-Reihe der aufgeklappten Zeile; die ruhige Liste (zugeklappte Zeilen ohne Rückfrage) ist unverändert.

### README von Hand (ledger)
Datei `.agentheim/knowledge/contexts/ledger/README.md`, Abschnitt Ereignis-Katalog `## Ereignisse`, Unterpunkt "Entscheidungen".
Alt:
```
- `EntryImportedFromComdirect`: Comdirect-Buchung beim Push angelegt, ohne Kategorie. Die
  Kategorisierung folgt als eigenes Ereignis in derselben Sequenz.
```
Neu:
```
- `EntryImportedFromComdirect`: Comdirect-Buchung beim Push angelegt, ohne Kategorie. Die
  Kategorisierung folgt als eigenes Ereignis in derselben Sequenz. Seit ynab-sync-d9e51
  optional `flagColor` und `flagName` (Schema-Version 2, Schlüssel nur bei Rückfrage Lena
  beim Import, Name "Lena"): die Buchung entsteht sofort als *Rückfrage Lena*, ohne
  zusätzliches `EntryFlagChangedInBb`. Ein Ereignis ohne die Felder liest sich als kein Flag
  (ADR 0064). Historie: "Lena gefragt beim Import".
```
Zusätzlich im Absatz "Prüfvermerk und Flags in Schema-Version 2" den Satz `**Version 1 aller drei bleibt lesbar**` unverändert lassen; kein weiterer Eingriff nötig.
