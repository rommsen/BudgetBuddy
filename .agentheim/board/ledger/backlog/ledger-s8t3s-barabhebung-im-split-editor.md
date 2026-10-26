---
id: ledger-s8t3s
title: "Barabhebung im Split-Editor des Kassenbuchs: ein Knopf legt die bisherige Kategorie und einen Übertrag aufs Bar-Konto an, Roman trägt nur den Bargeld-Betrag ein"
status: backlog
type: feature
context: ledger
created: 2026-10-26
completed:
depends_on: [design-system-001]
blocks: []
tags: [kassenbuch, split, barabhebung, komfort]
related_adrs: [0004, 0006]
related_research: []
prior_art: [ledger-xa7jf, ledger-pafkp, ledger-kxme4]
---

## Why
Eine EC-Zahlung mit Bargeld-Anteil (Einkauf plus Cashback an der Kasse) kommt bei Roman
häufiger vor (Roman, 2026-10-26). Im Kassenbuch muss er sie heute im allgemeinen
Split-Editor Zeile für Zeile aufteilen. Import prüfen hat dafür den Chip "Barabhebung"
(Spike ledger-xa7jf, Lücke 2).

## What
Entscheidung Roman, 2026-10-26: **Variante (a)**, ein Knopf "Barabhebung" im Split-Editor
des Kassenbuchs (`src/Client/Components/Ledger/SplitEditor.fs`, ledger-pafkp) als
Startvorlage. Kein eigener Knopf im Buchungs-Detail.

Der Knopf legt zwei Zeilen an:
1. die bisherige Kategorie der Buchung (fehlt sie, bleibt das Ziel leer),
2. einen Übertrag auf das Bar-Konto, das Quick-Add-Konto (ADR 0004, Übertrag im Split nach
   ADR 0006); ist keins eingestellt, bleibt das Ziel leer.

Roman trägt nur den Bargeld-Betrag ein, der Rest der Buchung landet auf der Kategorie-Zeile.

Vorbild Import: Chip "Barabhebung" in `src/Client/Components/SyncFlow/Views/TransactionRow.fs`,
Nachricht `StartCashbackSplit` in `src/Client/Components/SyncFlow/State.fs`. Dort bleiben
beide Beträge leer und der Nutzer gleicht mit "Rest" aus.

## Acceptance criteria
- [ ] Der Split-Editor im Kassenbuch zeigt einen Knopf "Barabhebung".
- [ ] Der Knopf legt zwei Zeilen an: Kategorie der Buchung und Übertrag auf das Quick-Add-Konto.
- [ ] Nach Eingabe des Bargeld-Betrags geht die Aufteilung ohne weiteren Schritt auf die Gesamtsumme auf.
- [ ] Ohne Kategorie an der Buchung oder ohne Quick-Add-Konto bleibt das jeweilige Ziel leer und ist wählbar.
- [ ] Gespeichert wird über den bestehenden Split-Schreibweg (ledger-kxme4), ohne neuen Befehl.

## Notes
Offen für die Verfeinerung:
- Rechnet sich der Rest automatisch auf die Kategorie-Zeile (Romans Wunsch "der Rest
  rechnet sich"), oder wie im Import über "Rest"? Das Kriterium oben geht vom automatischen
  Ausgleich aus.
- Wann ist der Knopf sichtbar: nur im leeren Editor, oder auch über eine schon begonnene
  Aufteilung (ersetzen oder nicht)?
- Vorzeichen: eine Ausgabe mit Bargeld ist zweimal negativ (Kategorie und Übertrag).
- Am Handy und am PC.

Herkunft: Spike ledger-xa7jf, Outcome-Tabelle Lücke 2.
