---
id: ledger-ybmqc
title: "Konto-Wahl: das Kassenbuch öffnet im zuletzt gewählten Konto (auch nach Neustart), am Handy steht der Kontoname als Titel mit Chevron und öffnet ein BottomSheet mit allen Konten und Ständen"
status: done
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

## Verifier note (iteration 1)

**Reasons:** README_DELTA-Bullet "Konto-Wechsler" enthielt einen Gedankenstrich nach dem fetten Kopf (Romans Schreibregel). Code, Tests (dotnet 2068 grün, test:scripts 218/218), Guards und die Aussage in "Einstieg und Navigation" waren in Ordnung.
**Suggested fix:** Bullet im Format "**Konto-Wechsler** (ledger-ybmqc): ..." schreiben, kein Code-Eingriff.
**Iteration hint:** likely-fixable
**Erledigt vom Conductor:** Die Korrektur betrifft nur Buchhaltung, die der Conductor ohnehin auf main schreibt. Der Delta-Text wurde dort angepasst, der Code blieb unverändert, kein erneuter Worker-Lauf.

## Outcome

Konto-Wahl wird je Gerät gemerkt und das Kassenbuch öffnet damit; am Handy gibt es den Konto-Wechsler nach Tafel 6.1 und 6.2.

- **Merken.** `State.fs`: `encodeAccountFilter`/`decodeAccountFilter` (Speicherwert `all` oder `account:<Guid>`, Schlüssel `ledger.selectedAccountFilter`), `resolveInitialFilter` (laufende Wahl, sonst gemerkte Sicht falls das Konto noch existiert, sonst `DefaultAccountId`). Neue Modellfelder `RememberedFilter` (bei `init` aus dem Speicher gelesen, try/catch, ohne Speicher `None`) und `AccountSheetOpen`. `SelectFilter` schreibt die Wahl per `Cmd.ofEffect` in den Speicher (try/catch) und schließt das Sheet. Er leert die Auswahl weiterhin über `resetEntries` (r1d2f).
- **Wechsler.** `View.fs` `accountSwitcher` (nur Handy, `md:hidden`): Titelknopf mit `Icons.chevronDown`, `BottomSheet.view` aus dem Design-System, Zeilen über `Money.accountListRow` (aktives Konto markiert). Neue Msgs `OpenAccountSheet`/`CloseAccountSheet`. Kartenleiste bleibt am Handy stehen (Tafel 6.1 zeigt sie unter dem Titel). Die Seitenüberschrift "Kassenbuch" bleibt als h1 unverändert, der Wechsler steht als eigene Zeile darunter (kein Umbau von `PageHeader`, wegen der Geschwister-Tasks); Marker für Tests sind `data-testid`, weil `guard:classes` undefinierte Marker-Klassen ablehnt.
- **Tests.** `src/Tests/LedgerAccountChoiceTests.fs` (11 Tests: Speicherwert hin und zurück, Kaputtes, vier Init-Fälle gemerkt/verschwunden/ohne/Gesamtsicht, laufende Wahl schlägt gemerkte, SelectFilter merkt und schließt, Auswahl geleert, Sheet auf/zu, Quelltext). `scripts/ledger-account-choice.test.mjs` (2 Playwright-Tests am echten Client: PC wählt Konto, volles Neuladen, Konto aktiv; Handy 390x844: Titel mit Kontoname und Chevron, Sheet mit Alle Konten zuoberst und allen Konten, Tipp wechselt, Sheet schließt, Speicher gesetzt, Neuladen behält das Konto).
- **Läufe.** `dotnet build` 0 Warnungen, 0 Fehler. `dotnet test` 2068 grün, 7 übersprungen, 0 fehlgeschlagen (ein früherer Durchlauf hatte einmal 1 Fehler, der nächste unveränderte Lauf war grün; unklar ob Flake). `npm run build` ok, `npm run guard:classes` ok, `npm run guard:tokens` ok, `npm run test:scripts` 218 von 218 grün.
- **[human-eye]** Kriterium (Kopf und Sheet am Handy gegen Tafel 6.1/6.2) wurde nicht per Screenshot belegt; Roman bitte kurz ansehen.

### README von Hand
Der Satz in "Einstieg und Navigation" ("Das Kassenbuch öffnet im zuletzt gewählten Konto.") stimmt jetzt mit dem Code überein und bleibt. Zur Präzisierung kann direkt dahinter stehen:
Alt: `**Einstieg und Navigation.** Das Kassenbuch öffnet im zuletzt gewählten Konto. Konto ist`
Neu: `**Einstieg und Navigation.** Das Kassenbuch öffnet im zuletzt gewählten Konto (je Gerät gemerkt, auch nach Neustart; ein verschwundenes Konto fällt auf das YNAB-Standardkonto zurück). Konto ist`
Am Handy wechselt der Titel mit Chevron (Konto-Wechsler) die Sicht, zusätzlich zur Kartenleiste.
