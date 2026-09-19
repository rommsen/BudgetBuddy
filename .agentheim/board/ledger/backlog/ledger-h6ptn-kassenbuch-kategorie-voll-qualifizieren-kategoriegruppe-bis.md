---
id: ledger-h6ptn
title: "Kassenbuch-Kategorie voll qualifizieren: Kategoriegruppe bis ins ReadModel durchreichen"
status: backlog
type: feature
context: ledger
created: 2026-09-19
completed:
depends_on: []
blocks: []
tags: [frontend, kassenbuch, styleguide, backend]
related_adrs: []
related_research: []
prior_art: [ledger-w9skm]
---

## Why

`ledger-w9skm` hat die Kategorie in der Kassenbuch-Liste in die Farbrolle Violett
(`#a855f7`) gebracht, aber bewusst nur den Blattnamen gezeigt ("Lebensmittel"),
nicht die volle Qualifizierung mit Gruppe ("Alltag: Lebensmittel"), die der
Entwurf an beiden Stellen vorsieht (Handoff, Abschnitt 1.1 Zeile 2 und 1.5
Kategorie-Feld: "Mobilität: Bahn").

Grund: `CategoryRef` (`src/Server/Ledger/Events.fs`) trägt nur
`CategoryId: YnabCategoryId` und `CategoryName: string` — keine Gruppe. Die
Gruppe existiert im System (`YnabCategory.GroupName`,
`src/Shared/Domain.fs:426`ff.), kommt aber nie bis in ein `Entry`-Ereignis oder
den Kassenbuch-Lesepfad (`ReadModel.toEntryView`,
`src/Server/Ledger/QueryHandlers.fs:113`).

## What

Die Kategoriegruppe muss von der YNAB-Kategorie bis in `LedgerEntryView`
durchgereicht werden, damit die Kassenbuch-Liste "Gruppe: Name" zeigen kann.
Betroffen mindestens:
- `CategoryRef` (`Server/Ledger/Events.fs`) — zusätzliches Feld für die Gruppe,
  inklusive Encode/Decode (JSON-Ereignisformat, Migrationsfrage für bereits
  gespeicherte Ereignisse ohne dieses Feld klären).
- Jede Stelle, die `CategoryRef` baut (`Domain.fs:369` `categoryRefOf`, ggf.
  Persistence/Sync-Pfad, der die Kategorie aus YNAB übernimmt).
- `LedgerEntryView.CategoryName` bzw. ein neues Feld für die Gruppe
  (`Shared/Domain.fs`), `ReadModel.toEntryView`.
- `Components/Ledger/View.fs`s `categoryLabel` (aus `ledger-w9skm`): Anzeige auf
  "Gruppe: Name" umstellen, wenn die Gruppe vorhanden ist.

**Achtung Datei-Überschneidung:** `Server/Ledger/Events.fs` ist zum Zeitpunkt
dieses Items Gegenstand von `ledger-t7v2q` (paralleler Task). Diese Aufgabe darf
erst starten, wenn `ledger-t7v2q` in `done/` liegt, um denselben Merge-Konflikt
zu vermeiden, den `ledger-w9skm`s Notes für `ledger-qns4j` beschrieben.

## Acceptance criteria

- [ ] `CategoryRef` trägt die Kategoriegruppe (Name, nicht nur Id).
- [ ] Bestehende, bereits gespeicherte Ereignisse ohne dieses Feld lassen sich
      weiterhin decodieren (Migrationsstrategie benannt und getestet: z. B.
      optionales Feld mit Fallback "ohne Gruppe" statt hartem Decode-Fehler).
- [ ] `LedgerEntryView` trägt die Gruppe, `categoryLabel` zeigt
      "Gruppe: Name" wenn beides vorhanden ist, sonst weiterhin den bisherigen
      Text (Split-Zeilen / "ohne Kategorie").
- [ ] Screenshots (Handy + PC) zeigen die volle Qualifizierung gegen den
      Entwurf (`docs/ux/kassenbuch/handoff-ablauf-1-durchsehen-und-pruefen.md`,
      1.1/1.9). [human-eye]
- [ ] `dotnet build`, `dotnet test`, `npm run build` grün.

## Notes

Aus `ledger-w9skm`s Notes übernommen: "Kommt die Gruppe nicht mit, ist das ein
eigener kleiner Zug durch `ReadModel.toEntryView` und gehört benannt, statt
still weggelassen zu werden." Der eigentliche Zug reicht weiter als nur
`ReadModel.toEntryView` — bis ins Event-Schema — deshalb ein eigenes Item statt
eines Nachtrags in `ledger-w9skm` selbst.