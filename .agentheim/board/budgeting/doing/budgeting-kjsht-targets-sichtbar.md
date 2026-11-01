---
id: budgeting-kjsht
title: "Targets sichtbar: Budget-Seite und Vorschau zeigen je Kategorie den Target-Betrag mit Rhythmus, die Budget-Seite auch den Fehlbetrag"
status: doing
type: feature
context: budgeting
created: 2026-11-01
completed:
depends_on: [design-system-001]
blocks: []
tags: [budgeting, ui, targets]
related_adrs: [0065]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
prior_art: [budgeting-k7wmd, budgeting-h2zfn]
---

## Why
Vor "Targets anwenden" will Roman sehen, welche Targets und Beträge er zuordnet. BB liest von YNAB nur den Fehlbetrag. Der Target-Betrag fehlt überall, und die Budget-Seite zeigt weder Target noch Fehlbetrag.

## What
**Entscheidung von Roman (2026-11-01):** Budget-Seite und Vorschau.

**Lesen:** `YnabClient` dekodiert zusätzlich `goal_type`, `goal_target`, `goal_cadence` und `goal_cadence_frequency`. Daraus entsteht ein `Target` (Betrag und Rhythmus) als Optionsfeld an `YnabMonthCategory`, `BudgetCategory` und `TargetLine`. BB rechnet weiterhin keine Targets (ADR 0065), es zeigt nur an, was YNAB liefert. Der Fehlbetrag bleibt `goal_under_funded`.

**Rhythmus-Text** als reine Funktion (Kadenz laut Research, Abschnitt 1):
- wöchentlich: "50,00 €/Woche"
- monatlich: "1.600,00 €/Monat"
- jährlich: "330,00 €/Jahr"
- alle n Monate oder alle 2 Jahre: "120,00 € alle 3 Monate", "120,00 € alle 2 Jahre"
- ohne Kadenz bei MF (ältere Monatsziele): "/Monat"; sonst nur der Betrag

**Budget-Seite, Handy:** eine dritte Zeile unter Zugewiesen und Aktivität, nur bei Kategorien mit Target: "Target 1.600,00 €/Monat · fehlt 1.600,00 €". Bei Fehlbetrag 0 steht "erfüllt" statt "fehlt …", bei pausiertem Target "pausiert".

**Budget-Seite, PC:** neue Spalte "Target" direkt hinter Kategorie, rechtsbündig, zweizeilig (Betrag mit Rhythmus, darunter derselbe Zusatz). Die Gruppenzeile spannt dann fünf Spalten.

**Vorschau:** die Zeile unter dem neuen Betrag wird zu "Target 1.600,00 €/Monat, Zugewiesen 0,00 €, Fehlbetrag 1.600,00 €".

## Acceptance criteria
- [ ] Decoder-Test: Kategorie mit `goal_target`, Kadenz und Häufigkeit ergibt das Target; ohne Target (`goal_target` null oder `goal_type` null) ergibt `None`; fehlende Kadenz-Felder lassen den Monat nicht scheitern.
- [ ] Reine Funktion Rhythmus-Text: Unit-Test je Fall oben, auch Kadenz 0 mit und ohne `MF`.
- [ ] `planTargetApplication` trägt das Target in die `TargetLine`; `previewsMatch` bleibt unverändert, ein geändertes Target allein löst kein "Stand hat sich geändert" aus (Test).
- [ ] Browser-Wächter (Handy, 390 px): Kategorie mit Target zeigt "Target … · fehlt …", erfüllte zeigt "erfüllt", pausierte "pausiert", Kategorie ohne Target zeigt keine dritte Zeile; kein horizontales Scrollen.
- [ ] Browser-Wächter (PC): Spalte "Target" zeigt denselben Inhalt.
- [ ] Browser-Wächter in `scripts/targets-apply.test.mjs`: eine Vorschau-Zeile zeigt Target, Zugewiesen und Fehlbetrag.
- [ ] Fixtures in `scripts/*.test.mjs` im selben Schritt mitgezogen (Fable.Remoting, CLAUDE.md): `budget-page`, `move-money`, `category-detail`, `targets-apply`.
- [ ] Am Handy bleibt die Vorschau-Zeile mit Target lesbar, die Budget-Liste wirkt mit der dritten Zeile nicht überladen. [human-eye]
- [ ] README budgeting: Bullet Budget-Seite, Begriff Target und Vorschau beschreiben die Anzeige; der Kommentar an `BudgetCategory` ("die Seite zeigt sie nicht") ist angepasst.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Nur DS-Komponenten und `ListRoles`; Beträge über `Money.formatAmountText`. Die Fehlbetragsangabe bleibt neutral eingefärbt, nur Verfügbar färbt ein Minus.
- Große Liste (rund 170 Kategorien): keine teuren Werte je Zeile neu berechnen.
- `goal_target_date` wird nicht angezeigt: bei MF steht dort teils nur ein Startdatum aus 2019 (Research, Abschnitt 1). Die Art des Targets (bis Datum, auffüllen) ist nicht Teil dieser Task.
- `goal_under_funded` gilt für den abgefragten Monat und rechnet Wochen selbst (Haushaltshilfe: 200,00 € im Oktober, 250,00 € im November). Deshalb kann "Target 50,00 €/Woche · fehlt 250,00 €" korrekt sein.
- Auf das Kategorie-Detail erstreckt sich die Task nicht.
- Unabhängig von budgeting-et7z8. Beide fassen `src/Client/Components/Budget/View.fs` an, aber an verschiedenen Stellen.
