---
id: ynab-002
title: Split-Review-UI — Cashback-Shortcut + generischer Editor
status: backlog              # backlog | todo | doing | done
type: feature                # feature | bug | refactor | chore | spike | decision
context: ynab-sync
created: 2026-06-13
completed:
commit:
depends_on: [ynab-001]
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
Editor darunter; Transfer-Ziel = bestehendes YNAB-Konto (nur Auswahl).

## What
Im Review-Flow aus einer einzelnen (auto-kategorisierten) Transaktion heraus ein Split-Sheet
öffnen, das (a) einen Ein-Tipp-"Barabhebung"-Shortcut für den 2-Zeilen-Fall und (b) einen
generischen N-Zeilen-Editor bietet. Die UI rechnet/validiert über die `ynab-001`-Domain-Helper
(`mkSplits`, `splitRemainder`, `buildCashbackSplit`) — **keine** Reimplementierung der Invariante
im Client. Muster werden wiederverwendet: visual-viewport-Sheet + Click-Commit (ADR 0005), der
Mobile-Category-Picker aus `categorization`, die Konto-Auswahl aus Quick Add (ADR 0004).

## Acceptance criteria
- [ ] Aus einer einzelnen Transaktion im Review-Flow öffnet ein Split-Sheet (ADR 0005:
      visual-viewport-Anker + Click-Commit).
- [ ] Cashback-Shortcut: Ein-Tipp "Barabhebung" füllt via `buildCashbackSplit` einen 2-Zeilen-
      Split vor (Transfer aufs gewählte Bargeld-Konto + Rest in der ursprünglichen Kategorie);
      nur der Transfer-Betrag wird eingegeben, der Rest rechnet sich live (`splitRemainder`).
- [ ] Generischer Editor: Zeilen hinzufügen/entfernen, je Zeile Kategorie ODER Transfer-Konto
      wählen, Beträge eingeben; UI zeigt laufend die Differenz zum Gesamt und sperrt Speichern,
      solange Summe ≠ Gesamt (nutzt `mkSplits`/`splitRemainder`).
- [ ] Transfer-Ziel-Picker bietet bestehende YNAB-Konten zur Auswahl (keine Konto-Anlage);
      wiederverwendung des Quick-Add-Konto-Auswahl-Musters (ADR 0004). Der Picker bietet nur
      Konten an, die eine Transfer-Payee haben (Prevent-at-Construction statt Reject-at-Push).
- [ ] Kategorie-Auswahl nutzt den Mobile-Category-Picker aus dem `categorization`-Context.
- [ ] Elmish update/view-Tests für die Live-Rest-Vorschau und das Save-gesperrt-bei-Mismatch-
      Verhalten; `dotnet build` + `dotnet test` grün; Diary aktualisiert.

## Notes

**Readiness: backlog — leichte Restklärung nötig** (vor PROMOTE auflösen):
1. **Sheet-Stacking:** Wie verhält sich das Stacking, wenn der Transfer-Konto-Picker *über* dem
   Split-Sheet öffnet (ADR-0005-Stacking-Semantik)? Interaktion durchdenken.
2. **Off-Budget-Konten:** Soll der Transfer-Ziel-Picker Off-Budget-Konten herausfiltern? Empfehlung
   aus dem Refinement: nur On-Budget-Konten mit vorhandener Transfer-Payee anbieten.

Beide Fragen sind UI-lokal und blockieren `ynab-001` nicht. Die Domain-Helper, auf die diese UI
baut (`mkSplits`, `splitRemainder`, `buildCashbackSplit`), sind in `ynab-001` spezifiziert.

**Abhängigkeit:** hängt an `ynab-001` (Domain + Push). Erst ziehen, wenn `ynab-001` done ist.
