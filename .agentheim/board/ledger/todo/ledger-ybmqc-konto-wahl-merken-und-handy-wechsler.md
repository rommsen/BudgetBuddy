---
id: ledger-ybmqc
title: "Konto-Wahl: das Kassenbuch öffnet im zuletzt gewählten Konto (auch nach Neustart), am Handy steht der Kontoname als Titel mit Chevron und öffnet ein BottomSheet mit allen Konten und Ständen"
status: todo
type: feature
context: ledger
created: 2026-10-25
completed:
depends_on: [design-system-001]
blocks: []
tags: [frontend, kassenbuch, konto, handy, navigation, tafeln]
related_adrs: [0049, 0052]
related_research: []
prior_art: [ledger-x0yga, ledger-qns4j]
---

## Why
Abgleich der Design-Tafeln (2026-10-25, Ablauf 6). Die ledger-README sagt "Das Kassenbuch
öffnet im zuletzt gewählten Konto", gebaut ist das nur innerhalb einer Sitzung: `State.fs`
hält die Wahl beim Neuladen über `model.SelectedFilter`, beim Neuöffnen der App gilt wieder
`accounts.DefaultAccountId`. Am Handy wechselt man das Konto heute nur über die
scrollbare Kartenleiste; Tafel 6.1 und 6.2 zeigen den Kontonamen als Seitentitel mit
Chevron und ein BottomSheet mit allen Konten.

## What
1. **Konto merken.** Die zuletzt gewählte Sicht (ein Konto oder "Alle Konten") wird je Gerät
   gespeichert (`localStorage`, per try/catch, ohne Speicher gilt der heutige Weg) und beim
   Öffnen des Kassenbuchs vorgewählt. Ist das gemerkte Konto nicht mehr in der Kontenliste
   (geschlossen, gelöscht), gilt das YNAB-Standardkonto.
2. **Handy-Wechsler.** Am Handy steht im Kopf des Kassenbuchs der Kontoname (bzw. "Alle
   Konten") als Titel mit Chevron. Ein Tipp öffnet ein BottomSheet aus dem Design-System
   mit "Alle Konten" zuoberst und jedem Konto mit Stand, das aktive markiert. Ein Tipp auf
   ein Konto wechselt und schließt das Sheet; der Wechsel leert die Auswahl wie jeder
   Kontowechsel (r1d2f).
3. Ob die Kartenleiste am Handy bleibt, entscheidet der Worker anhand der Tafel 6.1; fällt
   sie weg, im OUTCOME sagen.

## Acceptance criteria
- [ ] Test: Init mit gespeichertem, existierendem Konto wählt dieses Konto; mit gespeichertem, nicht mehr vorhandenem Konto das Standardkonto; ohne Speicher das Standardkonto; mit gespeicherter Gesamtsicht die Gesamtsicht.
- [ ] Ein Kontowechsel (Kartenleiste, PC-Kontenspalte, Handy-Sheet) schreibt die Wahl in den Speicher.
- [ ] Playwright: Konto wählen, Seite komplett neu laden, das Konto ist gewählt.
- [ ] Playwright (Handy-Breite): Titel zeigt den Kontonamen; Tipp öffnet das Sheet mit "Alle Konten" und allen Konten; Tipp auf ein anderes Konto wechselt die Liste und schließt das Sheet.
- [ ] Das Sheet nutzt den BottomSheet-Baustein des Design-Systems; `guard:tokens` und `guard:classes` grün.
- [ ] Die README-Aussage in "Einstieg und Navigation" stimmt danach mit dem Code überein (README_DELTA, falls der Satz präzisiert werden muss).
- [ ] Kopf und Sheet am Handy entsprechen Tafel 6.1 und 6.2 im Ablauf. [human-eye]

## Notes
- Tafeln: `docs/ux/kassenbuch/tafeln/` (Ablauf 6).
- Gap-Bericht Ablauf 5 bis 7, Punkte 2 und 7; Ablauf 1 und 2, Punkte 4 und 8.
