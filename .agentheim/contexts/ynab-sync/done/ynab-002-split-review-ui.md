---
id: ynab-002
title: Split-Review-UI — Cashback-Shortcut + generischer Editor
status: done                 # backlog | todo | doing | done
type: feature                # feature | bug | refactor | chore | spike | decision
context: ynab-sync
created: 2026-06-13
completed: 2026-06-13
commit:
depends_on: [ynab-001, design-system-001]
blocks: []
tags: [split, transfer, review-ux, mobile, cashback, ui]
related_adrs: [0004, 0005, 0006]
related_research: []
prior_art: []
---

## Why
Das Domain- und Push-Fundament (`ynab-001`, ADR 0006) macht Transfer-Zeilen in Splits
*möglich*; gefühlt wird das Feature aber erst durch die UI. Roman will im Review-Flow eine
Buchung schnell aufteilen — zu ~80 % der Barabhebungs-Fall (ein Transfer + eine Kategorie).
Die UX-Richtung ist mit ihm abgestimmt (2026-06-13): **Cashback-Shortcut zuerst**, generischer
Editor darunter; Transfer-Ziel = bestehendes YNAB-Konto (nur Auswahl). Die drei offenen
UI-Fragen sind im Refinement (2026-06-13) geklärt — siehe Notes.

## What
Im Review-Flow aus einer einzelnen (auto-kategorisierten) Transaktion heraus ein Split-Sheet
öffnen, das (a) einen Ein-Tipp-"Barabhebung"-Shortcut für den 2-Zeilen-Fall und (b) einen
generischen N-Zeilen-Editor bietet. Die UI rechnet/validiert über die `ynab-001`-Domain-Helper
(`mkSplits`, `splitRemainder`, `buildCashbackSplit`) — **keine** Reimplementierung der Invariante
im Client. Muster werden wiederverwendet: visual-viewport-Sheet + Click-Commit (ADR 0005), der
Mobile-Category-Picker aus `categorization`, die Konto-Auswahl aus Quick Add (ADR 0004).

## Acceptance criteria
- [x] Aus einer einzelnen Transaktion im Review-Flow öffnet ein Split-Sheet (ADR 0005:
      visual-viewport-Anker + Click-Commit). **Stacking:** Kategorie-/Konto-Picker öffnen als
      `.layer-2` über dem Split-Form-Sheet — exakt das bewährte „Picker über Quick-Add-Formular"-
      Muster (ADR 0005 §4, *nur eine Ebene tief*). Das Split-Sheet ist ein **Formular** (explizit
      Speichern/Abbrechen), kein Click-Commit-Picker → Click-Commit gilt nur für die Picker-Items
      darin, kein Vorzeitig-Commit/Schließen des Split-Sheets durch Tap im Picker.
- [x] Cashback-Shortcut: Ein-Tipp "Barabhebung" füllt einen 2-Zeilen-Split vor (Transfer aufs
      Bargeld-Konto + Rest in der ursprünglichen Kategorie); nur der Transfer-Betrag wird
      eingegeben, der Rest rechnet sich live (`splitRemainder` über eine Auto-„Rest"-Kategorie-
      Zeile). Das Ziel-Bargeld-Konto ist **vor-ausgewählt = das konfigurierte Quick-Add-Konto**
      (`ynab_quickadd_account_id`, ADR 0004), im Picker überschreibbar. Ist kein Quick-Add-Konto
      konfiguriert, fällt der Shortcut auf manuelle Konto-Wahl zurück.
- [x] Generischer Editor: Zeilen hinzufügen/entfernen, je Zeile Kategorie ODER Transfer-Konto
      wählen, Beträge eingeben; UI zeigt laufend die Differenz zum Gesamt und sperrt Speichern,
      solange Summe ≠ Gesamt (nutzt `mkSplits`/`splitRemainder`).
- [x] Transfer-Ziel-Picker bietet **nur offene On-Budget-Konten** zur Auswahl (keine Konto-Anlage),
      via `openOnBudgetAccounts`. `YnabAccount` um `OnBudget`/`Closed` und `accountDecoder` um
      `on_budget`/`closed` erweitert.
- [x] Kategorie-Auswahl nutzt den geteilten Mobile-Category-Picker (`BottomSheet.categoryPickerLayered`).
- [x] Tests: Filter-Test (Off-Budget- *und* geschlossene Konten ausgeschlossen) + Pure-Logic-Tests
      für Live-Rest-Vorschau und Save-gesperrt-bei-Mismatch; `dotnet build` + `dotnet test` grün
      (569 passed); Diary aktualisiert.

## Notes

**Readiness: todo — alle offenen Fragen im Refinement aufgelöst (2026-06-13).** Promotet, weil
das Styleguide-Gate (`design-system-001`) geschlossen ist (s.u.) und die drei UI-Fragen
entschieden sind:

1. **Sheet-Stacking — gelöst durch ADR 0005 §4 (keine neue Entscheidung nötig).** Das Projekt hat
   das Muster bereits: *Sheet-Stacking nur über `.layer-2` (z-index 70/80) und nur eine Ebene tief
   (Picker über Quick-Add-Formular).* Das Split-Sheet ist strukturell das Form-Sheet (wie Quick
   Add); der Kategorie-/Konto-Picker stapelt als `.layer-2` darüber. Click-Commit betrifft die
   Picker-Items, nicht das Form-Sheet → kein Vorzeitig-Commit. → in AC 1 kodiert.
2. **Off-Budget-Konten — entschieden: nur On-Budget (Roman, 2026-06-13).** Deckt den 80 %-Cashback-
   Fall (Bar-Konto ist On-Budget) und hält die Liste kurz. Reine Scope-Begrenzung, kein technischer
   Verlust (jedes Konto hat eh eine Transfer-Payee). → in AC 4 kodiert, inkl. der Backend-
   Erweiterung, die der Filter voraussetzt.
3. **Cashback-Default-Ziel — entschieden: Quick-Add-Konto wiederverwenden (Roman, 2026-06-13).**
   Default = `ynab_quickadd_account_id` (ADR 0004), im Picker überschreibbar, null neue Config.
   **Bewusste Konflation:** dieses Konto dient dann manueller Eingabe *und* Cashback-Transfer-Ziel
   — bei Roman physisch dasselbe Bargeld. Edge: ist es nicht konfiguriert, kein Default →
   manuelle Wahl. → in AC 2 kodiert.

**Abhängigkeit — beide erfüllt:**
- `ynab-001` (Domain + Push) — **done** (688d81c). Liefert `mkSplits`, `splitRemainder`,
  `buildCashbackSplit`, die Transfer-Push-Auflösung.
- `design-system-001` (Styleguide-Gate) — **done** + von Roman gate-reviewt (Markdown +
  live `/styleguide`-Route, 2026-06-13). Das Hard-Enforcement-Gate, das diesen reinen UI-Task in
  Backlog hielt, ist damit **geschlossen** → Promotion frei.

**Reuse / Load-bearing Code:** Sheet-/Click-Commit-Mechanik (`src/Client/DesignSystem/Viewport.fs`,
`BottomSheet.fs`, ADR 0005); Konto-Picker-Muster aus Quick Add (ADR 0004);
`YnabAccount`/`accountDecoder` für den On-Budget-Filter (s. AC 4); Domain-Helper aus `ynab-001`
(`src/Shared/Domain.fs` — `mkSplits`/`splitRemainder`/`buildCashbackSplit`). Client-State-Einstieg:
`src/Client/Components/SyncFlow/State.fs` (`AddSplit` baut heute schon `ToCategory`-Zeilen).

## Outcome

Split-Review-UI gebaut und in den Review-Flow eingehängt. Aus einer Transaktion öffnen zwei
Action-Chips das Split-Form-Sheet: **„Barabhebung"** (Cashback-Shortcut, der häufige ~80%-Fall)
und **„Aufteilen"** (generischer N-Zeilen-Editor).

**Mechanik:**
- Split-Sheet ist ein **Form-Sheet** (explizit Speichern/Abbrechen) über `BottomSheet.view`;
  Kategorie-/Konto-Picker öffnen als `.layer-2` darüber (ADR 0005 §4, eine Ebene tief). Neuer
  `BottomSheet.accountPickerLayered` für die Transfer-Ziel-Wahl (Click-Commit auf den Items,
  Ghost-Click-Guard — kein Vorzeitig-Commit des Form-Sheets).
- **Cashback** = Transfer-Zeile (User tippt nur den Abhebungsbetrag) + Kategorie-**Auto-„Rest"-
  Zeile**, die den Restbetrag live aufnimmt. Default-Transfer-Ziel = konfiguriertes Quick-Add-
  Konto (`ynab_quickadd_account_id`, ADR 0004), im Picker überschreibbar; unkonfiguriert →
  kein Default, manuelle Wahl.
- **Generischer Editor**: Zeilen ±, je Zeile Kategorie ODER Transfer, Live-Rest-Banner; Speichern
  gesperrt solange `mkSplits` nicht `Ok` (Summe ≠ Gesamt / <2 Zeilen / Währungsmix) oder eine
  Zeile noch kein Ziel hat.
- Validierung/Arithmetik **ausschließlich** über die geteilten ynab-001-Helper (`splitRemainder`/
  `mkSplits`) — keine Reimplementierung der Invariante im Client (ADR 0006).

**Backend-Erweiterung (load-bearing):** `YnabAccount` += `OnBudget`/`Closed`, `accountDecoder`
dekodiert `on_budget`/`closed` (Optional, Conformist-sichere Defaults: on_budget=true, closed=false).
Neuer Filter `openOnBudgetAccounts` (`src/Shared/Domain.fs`) speist den Transfer-Ziel-Picker.

**Bewusste Umsetzungs-Entscheidung (keine ADR nötig):** AC 2 nennt `buildCashbackSplit`; dessen
Signatur rechnet aber den **Transfer** als Rest aus einem gegebenen Kategorie-Betrag, während die
gewünschte UX „nur den Transfer eingeben, Kategorie = Rest" verlangt. Umgesetzt über eine
Auto-„Rest"-Kategorie-Zeile, die `splitRemainder` der übrigen Zeilen aufnimmt — gleiche geteilte
Invariante (`mkSplits`/`splitRemainder`), nur die Rolle „Rest-Zeile" liegt auf der Kategorie statt
auf dem Transfer. Pure und voll getestet.

**Schlüsseldateien:**
- `src/Client/Components/SyncFlow/Views/SplitSheet.fs` (Sheet + Shortcut + layered Picker)
- `src/Client/Components/SyncFlow/Types.fs` (`SplitDraftLine`/`SplitEditState`, pure Editor-Helper)
- `src/Client/Components/SyncFlow/State.fs` (Handler, Cashback, Account-Loading)
- `src/Client/DesignSystem/BottomSheet.fs` (`accountPickerLayered`)
- `src/Shared/Domain.fs` (`YnabAccount`-Flags, `openOnBudgetAccounts`), `src/Server/YnabClient.fs` (Decoder)
- `src/Tests/SplitEditorTests.fs`, `SplitTransactionTests.fs`, `YnabClientTests.fs` (16 neue Tests)

**Verifikation:** `dotnet build` (Solution) grün, `dotnet test` 569 passed / 6 skipped, `npm run build`
(Fable) grün. Diary aktualisiert. Kein Supabase/Smoke in diesem Repo (stale `.claude/rules`).
