---
id: ynab-sync-d9e51
title: "Lena fragen schon beim Import: eine Aktion an der einzelnen Zeile in Import prüfen, die Buchung geht mit der Lena-Flag-Farbe im Import-Push nach YNAB (auch ohne Kategorie) und steht im Kassenbuch sofort als Rückfrage Lena"
status: todo
type: feature
context: ynab-sync
created: 2026-10-26
completed:
depends_on: [design-system-001]
blocks: []
tags: [frontend, backend, import, lena, flags, kassenbuch]
related_adrs: [0021, 0056, 0061]
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
