---
id: ynab-t4n8p
title: Quick Add — letzte 3–5 Buchungen des Quick-Add-Kontos als Vorlagen
status: backlog
type: feature
context: ynab-sync
created: 2026-06-27
completed:
depends_on: [design-system-001]
blocks: []
tags: [quick-add, templates, ynab-read, ui]
related_adrs: [0004]
related_research: []
prior_art: [2026-06-11-quick-add-manual-entry, 2026-06-12-quick-add-feedback-round]
---

## Why
Romans Bar-Buchungen wiederholen sich häufig (ähnliche Payee/Betrag/Kategorie). Jede
Buchung von Grund auf neu zu tippen ist Reibung. Die letzten Buchungen des Quick-Add-
Kontos sind die beste Vorlagen-Quelle für genau diese wiederkehrenden Fälle.

## What
Im Quick-Add-Formular die letzten 3–5 Buchungen des Quick-Add-Kontos
(`ynab_quickadd_account_id`, ADR 0004) als auswählbare Vorlagen anzeigen. Tippt Roman
eine Vorlage an, wird das Formular **vollständig vorausgefüllt** (Betrag, Kategorie,
Payee, Memo) — mit **Datum = heute**. Es wird **nicht** automatisch gebucht: Roman prüft
die vorausgefüllten Werte, passt bei Bedarf an und drückt am Ende selbst auf Speichern.

## Acceptance criteria
- [ ] Quick Add zeigt die letzten 3–5 Buchungen des Quick-Add-Kontos als auswählbare
      Vorlagen an.
- [ ] Auswahl einer Vorlage füllt das Formular vollständig vor (Betrag, Kategorie, Payee,
      Memo soweit vorhanden).
- [ ] Das Datum der vorausgefüllten Vorlage ist immer **heute**, nicht das Datum der
      Original-Buchung.
- [ ] Es wird nichts automatisch gebucht — der Push passiert erst beim Klick auf
      Speichern; alle vorausgefüllten Werte bleiben vorher editierbar.

## Notes
Offen für die Refine-Phase:
- **Datenquelle:** YNAB `GET /budgets/{id}/accounts/{quickadd}/transactions` (jüngste
  zuerst). Conformist gegenüber der YNAB-API — ein **neuer Read-Pfad**: Quick Add schreibt
  bisher nur (ADR 0004), liest aber nichts zurück.
- **Dedup/Anzeige:** identische Vorlagen zusammenfassen, oder einfach die letzten N roh?
- **Anzahl & Darstellung:** 3 vs. 5; kompakt (Chips über dem Formular? Liste?).
- **Betrags-Rückmapping:** Milliunits → Anzeige-Betrag + Vorzeichen (Ausgabe/Einnahme)
  korrekt aus der YNAB-Buchung zurückrechnen (Gegenstück zu `manualTransactionMilliunits`,
  vgl. `QuickAddTests.fs`).
- **Performance/Rate-Limit:** ein zusätzlicher YNAB-Read beim Öffnen des Quick-Add-Sheets
  — Caching bedenken (YNAB ~200 req/h, s. README open question).
- Setzt UI voraus → hängt am Styleguide-Gate (`design-system-001`, done).
