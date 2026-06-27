---
id: ynab-t4n8p
title: Quick Add — letzte 5 Buchungen des Quick-Add-Kontos als Vorlagen (dedupliziert)
status: backlog
type: feature
context: ynab-sync
created: 2026-06-27
completed:
depends_on: [design-system-001, ynab-q7k3m]
blocks: []
tags: [quick-add, templates, ynab-read, ui]
related_adrs: [0004]
related_research: []
prior_art: [2026-06-11-quick-add-manual-entry, 2026-06-12-quick-add-feedback-round]
---

## Why
Romans Bar-Buchungen wiederholen sich häufig (ähnliche Payee/Betrag/Kategorie). Jede von
Grund auf neu zu tippen ist Reibung. Die letzten Buchungen des Quick-Add-Kontos sind die
beste Vorlagen-Quelle für genau diese wiederkehrenden Fälle.

## What
Auf der Quick-Add-Seite die letzten **5 deduplizierten** Buchungen des Quick-Add-Kontos
(`ynab_quickadd_account_id`, ADR 0004) als auswählbare Vorlagen anzeigen — gleiche
Kombination aus Payee + Betrag + Kategorie wird zu *einer* Vorlage zusammengefasst, sodass
bis zu 5 *verschiedene* wiederkehrende Buchungen erscheinen. Tippt Roman eine Vorlage an,
wird das Formular **vollständig vorausgefüllt** (Betrag, Kategorie, Payee, Memo) mit
**Datum = heute**. Es wird **nichts** automatisch gebucht: Roman prüft, passt ggf. an und
drückt selbst auf Speichern.

## Acceptance criteria
- [ ] Die Quick-Add-Seite zeigt bis zu 5 deduplizierte Vorlagen aus den jüngsten Buchungen
      des Quick-Add-Kontos (Dedup-Schlüssel: Payee + Betrag + Kategorie).
- [ ] Auswahl einer Vorlage füllt das Formular vollständig vor — Betrag (inkl. Ausgabe/
      Einnahme), Kategorie, Payee, Memo soweit vorhanden — via Prefill des
      `QuickAddFormState` (ein `UpdateQuickAdd` bzw. neues `PrefillQuickAdd`).
- [ ] Das Datum der vorausgefüllten Vorlage ist immer **heute**, nicht das Datum der
      Original-Buchung.
- [ ] Es wird nichts automatisch gebucht — Push erst beim Klick auf Speichern; alle Werte
      vorher editierbar.
- [ ] Vertrags-/Domain-Tests fürs Reverse-Milliunits-Mapping und das Dedup.
- [ ] Folgt dem Styleguide (`design-system-001`).

## Notes
- **Datenquelle steht schon:** `YnabClient.getAccountTransactions`
  (`src/Server/YnabClient.fs:730-771`) macht bereits
  `GET /budgets/{id}/accounts/{account_id}/transactions?since_date=` (heute für die
  Duplikat-Erkennung genutzt). Kein neuer YNAB-Integrationspfad — nur ein neuer
  API-/Remoting-Call „letzte Quick-Add-Buchungen", der ihn fürs Quick-Add-Konto
  (`QuickAddAccountId` + `DefaultBudgetId` aus `YnabSettings`, `Shared/Domain.fs:440-447`)
  aufruft (`since_date` z. B. 60–90 Tage zurück).
- **Reverse-Milliunits:** YNAB liefert signed milliunits; Rückmapping `milliunits/1000m`,
  `< 0 ⇒ IsOutflow=true`, Betrag = `abs`. Gegenstück zu `manualTransactionMilliunits`
  (`Shared/Domain.fs:551-553`); im Domain-Layer (pur) implementieren + testen.
- **Kategorie:** YNAB-Buchung trägt `CategoryId` → direkt in `QuickAddFormState.CategoryId`.
- **Prefill-Mechanik:** `QuickAddFormState` (`SyncFlow/Types.fs:77-93`) hält exakt die
  Felder; `UpdateQuickAdd` ersetzt den ganzen Record in einem Msg (`SyncFlow/State.fs:1103`).
  Ein dediziertes `PrefillQuickAdd` ist optional, aber sauberer.
- **Performance:** ein zusätzlicher YNAB-Read beim Öffnen der Quick-Add-Seite — Caching /
  Rate-Limit (YNAB ~200 req/h, s. README) bedenken; ggf. einmal pro Seitenbesuch laden.
- **Darstellung:** kompakte Chips/Liste über dem Formular (Styleguide); Label z. B.
  Payee + Betrag.
- **Sequencing:** hängt an `ynab-q7k3m` — die Vorlagen rendern in der dort entstehenden
  eigenen Quick-Add-Seite und füllen deren (ins Top-Level gehobenen) Form-State. Erst die
  Seite, dann die Vorlagen.
- Hängt am Styleguide-Gate (`design-system-001`, done).
