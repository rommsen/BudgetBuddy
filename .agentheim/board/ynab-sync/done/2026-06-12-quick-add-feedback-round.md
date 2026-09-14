---
title: "Quick Add Feedback-Runde: eigenes Konto, echter Picker, kein FAB, Payee optional"
status: done
bc: ynab-sync
created: 2026-06-12
completed: 2026-06-12
captured: retroaktiv 2026-06-12 — Arbeit lief außerhalb von agentheim (Session feature/ux-wow)
related_adrs: [0004]
commits: [f642b8f]
branch: feature/ux-wow
---

# Quick Add Feedback-Runde: eigenes Konto, echter Picker, kein FAB, Payee optional

## Problem
Romans Feedback nach dem ersten Android-Test des deployten Quick Add:
1. Buchung muss aufs **Bar-Konto** gehen, nicht aufs Comdirect-Import-Konto —
   Konto in den Settings wählbar machen.
2. Kategorie-Auswahl war eine native Selectbox statt des neuen Pickers.
3. Der schwebende FAB passte nicht zur App-UX.
4. Payee war Pflichtfeld — eine Bar-Buchung braucht nur einen Betrag.

## Acceptance criteria
- [x] Settings → YNAB: zweites Konto-Select "Quick-Add-Konto (z. B. Bar)",
      persistiert als `ynab_quickadd_account_id`; Read-only-Anzeige inkl. Kontoname
- [x] Quick Add bucht ausschließlich aufs Quick-Add-Konto; ohne Konfiguration klare
      Fehlermeldung, kein Fallback aufs Import-Konto (ADR 0004)
- [x] Kategorie-Feld öffnet den vollwertigen Category Picker (Suche, Zuletzt verwendet)
      auf erhöhtem Sheet-Layer über dem Formular; ✕ zum Entfernen
- [x] FAB entfernt; Einstiege: Secondary-Button unter "Sync starten" + Plus-Icon im
      Review-Header (im .back-btn-Stil)
- [x] Payee optional — Validierung gelockert, `payee_name` im YNAB-JSON weggelassen
      wenn leer (sonst entstünde ein Payee namens "")
- [x] Tests an neue Semantik angepasst (accepts empty/whitespace payee, payee_name
      omitted when blank, overlong payee rejected)

## Outcome
18 Dateien über alle Schichten (Shared/Server/Settings/SyncFlow/DesignSystem/CSS/Tests).
Neue DesignSystem-Fähigkeit: `categoryPickerLayered` (Sheet-über-Sheet via `.layer-2`).

## Verification
- 516/516 Tests grün; Fable-Build sauber; deployed auf docker-host
  (Health-Check bestanden). Offen: Roman muss das Quick-Add-Konto einmalig in den
  Settings wählen (nach "Verbindung testen").
