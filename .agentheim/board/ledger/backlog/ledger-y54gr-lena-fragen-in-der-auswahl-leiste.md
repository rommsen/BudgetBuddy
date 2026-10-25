---
id: ledger-y54gr
title: "Lena fragen in der Auswahl-Leiste: eine Sammelaktion für viele Buchungen, ein Sammel-PATCH mit flag_color"
status: backlog
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
Ziel-Lage: backlog (wartet auf ledger-w4vte und ledger-xfjcp).

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
