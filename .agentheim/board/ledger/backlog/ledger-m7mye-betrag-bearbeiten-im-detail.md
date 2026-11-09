---
id: ledger-m7mye
title: "Betrag einer Buchung im Detail bearbeiten, auch in YNAB"
status: backlog
type: feature
context: ledger
created: 2026-11-09
completed:
depends_on: []
blocks: []
tags: [etappe-2, ynab, detail, betrag]
related_adrs: [0053, 0058, 0006]
related_research: []
prior_art: []
---

## Why
Etappe 2 des Fahrplans (Lücken schließen, ADR 0086). Einen Betrag korrigieren (Tippfehler bei einer Barbuchung,
Trinkgeld nachtragen) geht heute nur in YNAB; in BB lässt sich der Betrag einer Buchung nicht ändern. Im
Prototyp ist das ab Version 15 vorgesehen (Roman, 2026-11-08).

## What
Im Buchungs-Detail (ADR 0053) den Betrag bearbeiten; BB hält die Änderung fest und schreibt sie nach YNAB.
Offen für die Verfeinerung:
- Welche Buchungen: wohl nur eigene Barbuchungen und Buchungen ohne Bank-Herkunft; ein Comdirect-Import trägt
  den Bankbetrag, eine Änderung würde beim Abgleich auseinanderlaufen. Abgeglichene Buchungen.
- Split-Buchungen: YNAB ignoriert einen geänderten Betrag einer Split-Buchung still (ADR 0058, Context). Also
  entweder gesperrt oder über Löschen und Neuanlegen (ADR 0058 Punkt 2, Ersatz-`import_id`), und die Zeilen
  müssen den neuen Betrag tragen (ADR 0006).
- Übertrag: der Gegenbetrag auf dem anderen Konto.
- Eingabe nach den Regeln in CLAUDE.md: Betragsfeld ohne Vorzeichen (`inputmode="decimal"`), die Richtung kommt
  aus der Buchung; geprüft mit Touch bei 412 px.
- Wirkung auf Budget-Seite und Kontostand, Ereignis der Entscheidungs-Familie (ADR 0083), Historie-Satz.
- Skizze: Prototyp ab Version 15, Abgleich mit der heutigen Seite.

## Acceptance criteria
Noch offen, kommt in der Verfeinerung.

## Notes
- Beträge sind `Money` (decimal), an der YNAB-Grenze Milliunits als JSON-Zahl (`Encode.int`), sonst verwirft
  YNAB still (CLAUDE.md).
- Erfordert eine Entscheidung zum Schreibweg (eigenes ADR oder Nachtrag zu ADR 0058).
