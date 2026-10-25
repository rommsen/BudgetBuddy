---
id: ledger-y54gr
title: "Lena fragen in der Auswahl-Leiste: eine Sammelaktion für viele Buchungen, ein Sammel-PATCH mit flag_color"
status: done
type: feature
context: ledger
created: 2026-10-24
completed:
depends_on: [design-system-001, ledger-xfjcp, ledger-w4vte]
blocks: []
tags: [frontend, backend, kassenbuch, flags, v2, lena]
related_adrs: [0050, 0053, 0056]
related_research: []
prior_art: [ledger-nhwrt, ledger-xfjcp]
---
## Why
ADR 0053 Punkt 2: für viele Buchungen die Leiste. Roman will mehrere Buchungen auf einmal
an Lena geben, wie er heute mehrere in YNAB flaggt.

## What
"Lena fragen" in der Auswahl-Leiste, daneben "Rückfrage zurücknehmen". Je Buchung ein
`EntryFlagChangedInBb`, danach ein Sammel-`PATCH /budgets/{id}/transactions` mit
`{"transactions":[{"id","flag_color"}]}` (Muster `updateReviewMarkBatch` aus `ledger-nhwrt`,
ADR 0050). Auswahl wie bei "Geprüft": Seiten-Auswahl, Rückgängig mit Vorzustand je Buchung,
Fehlschlag je Buchung (`FlagWriteBackFailure`), nicht gespeicherte Ids als Fehlschlag.
Ohne gesetzte Flag-Farbe ist die Aktion gesperrt.

## Acceptance criteria
- [ ] Domain-Test: der Sammelbefehl schreibt je geänderter Buchung ein Ereignis, Buchungen
      ohne Änderung (schon `AskLena`) erzeugen keins und gehen nicht in den PATCH.
- [ ] YnabClient-Test: der Body trägt je Buchung nur `id` und `flag_color`.
- [ ] Domain-Test: nennt die Antwort Ids nicht als gespeichert, folgt für sie
      `EntryFlagWriteBackFailed`; ein Netzfehler trifft den ganzen Stapel.
- [ ] Reducer-Test: "Rückgängig" stellt je Buchung den Vorzustand her; bei aktivem Chip
      "Rückfrage Lena" bleiben zurückgenommene Buchungen ausgegraut stehen wie im Detail
      (ledger-xfjcp, Tafel 5.8, Roman 2026-10-25).
- [ ] Ansichts-Test: die Leiste zeigt die Aktion mit Anzahl; ohne Farbe deaktiviert mit Grund.
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes`,
      `npm run test:scripts` grün.
- [ ] Optik nur aus Design-System und Tokens (Styleguide §3, Vorrangregel in den Handoffs):
      keine Farb- oder Maßwerte aus den Design-Tafeln; ein neuer DS-Baustein erscheint in
      `/styleguide` (`src/Client/Components/Styleguide/View.fs`); `npm run guard:tokens` grün
      (Wächter aus `design-system-wfhxj`, sobald integriert).
- [ ] Das Neue fügt sich optisch in die gebauten Screens (Kassenbuch, Buchungs-Detail,
      `/styleguide`) ein und fällt daneben nicht auf. [human-eye]

## Notes
Dateien: wie `ledger-xfjcp`, dazu die Auswahl-Leiste in `Client/Components/Ledger/View.fs`.

Stil-Absicherung (2026-10-24): Tafeln und Handoffs legen nur den Ablauf fest; die Optik
kommt aus `standards/frontend/styleguide.md`, `src/Client/DesignSystem/` und `/styleguide`
(Vorrangregel in `docs/ux/kassenbuch/tafeln/README.md`).

## Outcome

"Lena fragen" und "Rückfrage zurücknehmen" stehen in der Auswahl-Leiste (ADR 0053 Punkt 2). Beide Aktionen tragen die Zahl der Auswahl im Namen, sind sekundär (Teal-Rand, Geprüft bleibt die einzige orange Aktion) und stehen am PC in einer Reihe vor "Kategorie setzen", am Handy in einer dritten Reihe der Leiste. Ohne gesetzte Flag-Farbe ist "Lena fragen" gesperrt und nennt den Grund (Tooltip, am Handy zusätzlich als Zeile unter den Knöpfen); Zurücknehmen braucht die Farbe nicht.

**Server.** `Ledger.CommandHandlers.setLenaFlagBatch` löst die Auswahl wie `setReviewMarkBatch` auf, wendet je Buchung die Regel des Einzelwegs an (`lenaFlagTargetOf`, auch von `setLenaFlag` benutzt: schon Lena oder Zurücknehmen bei anderem Flag ist unverändert) und schreibt je geänderter Buchung ein `EntryFlagChangedInBb`. Danach geht EIN `Ynab.updateFlagBatch` hinaus (PATCH `.../transactions`, Body je Buchung nur `id` und `flag_color`, `null` für kein Flag). Nicht verknüpfte Buchungen bekommen `EntryFlagWriteBackFailed` (Grund `flagNotLinkedReason`) und gehen nicht in den PATCH; nennt die Antwort Ids nicht als gespeichert, folgt für sie `EntryFlagWriteBackFailed` (`flagNotSavedReason`); ein Netzfehler trifft den ganzen Stapel. Erfolg bei offenem früheren Fehlschlag schreibt `EntryFlagWrittenBackToYnab`. Baseline wie im Einzelweg (`flagBaseline`). `AssignLena` ohne gültige Farbe lehnt den ganzen Befehl ab. `restoreFlagBatch` stellt Farbe und Name des Vorzustands je Buchung her (ein Ereignis je Buchung, ein PATCH). Beides unter der Schreib-Sperre. Neue Typen in `Shared.Domain`: `FlagBatchChange`, `FlagBatchOutcome` (`Changed`, `UnchangedCount`, `FailedCount` für gelöschte oder unbekannte), `FlagRestoreItem`; neue API `LedgerApi.setLenaFlagBatch` und `restoreFlagBatch`. Keine neuen Ereignisse.

**Client.** Modell `LenaBatchRunning` (sperrt alle drei Sammelaktionen, ist in `ledgerActive` eingetragen), Nachrichten `StartLenaBatch`, `RunLenaBatch`, `LenaBatchDone`, `StartUndoFlagBatch`, `RunUndoFlagBatch`, `FlagBatchRestored`, Toast-Art `UndoFlagBatch`. Der reine Reducer `applyFlagBatch` trägt das Flag in die geladenen Zeilen und ins offene Detail ein, schreibt den Fehlschlag je Zeile, lässt die Auswahl enden (bei der Rücknahme nicht) und führt den Zähler "Rückfrage Lena (n)" über die Vorzustände der Antwort nach, auch für nie geladene Buchungen. Bei aktivem Chip bleiben zurückgenommene Zeilen ausgegraut stehen (`applyFilterEffect`, wie im Detail, Tafel 5.8, Roman 2026-10-25); ein Rückgängig macht sie wieder zum Treffer. Toast: "n Buchungen an Lena gefragt" bzw. "Rückfrage bei n Buchungen zurückgenommen", mit Zählern "ohne Änderung" und "nicht in YNAB" und dem Hinweis "Flag nicht in YNAB: Grund". Das Rückgängig ist ein Aufruf für alle Buchungen.

**Design-System.** `MultiSelect.fs` bekommt den Typ `LenaAction` (Label, IsBusy, IsLocked, Reason, OnClick) und `lenaButton`. ACHTUNG Signaturänderung: `MultiSelect.barMobile` und `MultiSelect.barDesktop` nehmen nach `category` zwei neue Parameter `lena` und `withdraw`. Ruft `ledger-fa1fp` (Auswahl-Bausteine im Styleguide) die Leisten mit der alten Signatur auf, genügt beim Zusammenführen, zwei `MultiSelect.LenaAction` zu übergeben. Die PC-Leiste darf jetzt umbrechen (`flex-wrap`), der Tastenhinweis "Esc aufheben" erscheint erst ab `lg`, der Abstandshalter ist am Handy `h-52` (vorher `h-40`) und bei `md` `h-28` (ab `lg` `h-20`), weil die Leiste bei 768 px zweizeilig wird. Gemessen: 390 px alle sechs Knöpfe im Bild, Reihe 3 unter Reihe 2, Abstandshalter höher als die Leiste; 768 px Geprüft in der zweiten Zeile, nichts ragt über den Rand; 1440 px eine Reihe. Kein neuer DS-Baustein im eigenen Sinn (Erweiterung der Leiste), `/styleguide` unverändert.

**Tests.** 35 neue: `LedgerLenaFlagBatchTests.fs` (13: Domain je geänderter Buchung ein Ereignis, schon Lena ohne Ereignis und nicht im PATCH, Farbe fehlt, Zurücknehmen lässt anderes Flag stehen, nicht gespeicherte Ids, Netzfehler, nicht verknüpft, offener Fehlschlag endet, `BatchAllExcept`, Rückgängig inklusive Markiert; YnabClient-Body nur `id` und `flag_color`, PATCH, Fehlerzuordnung), `LedgerLenaFlagBatchClientTests.fs` (19: Reducer, Sperren, Chip-Verhalten, Rückgängig mit Vorzustand, Toast-Texte, Quelltext der Leiste), `scripts/ledger-lena-batch.test.mjs` (3 Playwright-Belege am gerenderten Element: PC-Ablauf mit Aufruf und Rückgängig, gesperrt ohne Farbe mit Tooltip, Handy-Reihe 3 mit Grund-Zeile und ohne seitliches Scrollen). Angepasst: `scripts/ledger-selection.test.mjs` (am Handy steht ohne Farbe der Grund mit dem Wort "Flag" in der Leiste, deshalb fehlt "Flag" in der Verbotsliste dieser einen Prüfung). Läufe aus dem Worktree: `dotnet build` 0 Warnungen, 0 Fehler; `dotnet test` 2203 bestanden, 7 übersprungen, 0 Fehler; `npm run build` ok; `npm run guard:classes` ok; `npm run guard:tokens` ok; `node --test --test-concurrency=1 scripts/*.test.mjs` 235 von 235 bestanden.

Offen für das Auge von Roman ([human-eye], Kriterium 8): Screenshots der Leiste bei 390, 768 und 1440 px liegen im Scratchpad unter `.../scratchpad/y54gr/bar-390.png`, `bar-768.png`, `bar-1440.png`; ich habe 390 und 768 angesehen und nichts Auffälliges gefunden, die endgültige Beurteilung bleibt bei ihm.

### README von Hand
Nicht nötig: beide Einträge laufen als `append` (das Gate `npm run check:readme-delta -- --bc ledger` hat sie angenommen). Kein neues Ereignis, daher keine Zeile im Ereignis-Katalog.
