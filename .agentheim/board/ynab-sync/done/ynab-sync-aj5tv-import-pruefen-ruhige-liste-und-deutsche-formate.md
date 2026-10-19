---
id: ynab-sync-aj5tv
title: Import prüfen an ADR 0049 angleichen: nur die Kante farbig, deutsche Zahlen und Datumsformate
status: done
type: feature
context: ynab-sync
created: 2026-10-19
completed:
depends_on: [design-system-zahv9]
blocks: [ynab-sync-e4gm3]
tags: [frontend, ruhige-listen, formate]
related_adrs: [0049, 0005]
related_research: []
prior_art: [design-system-q7rkm, ledger-n4vte]
---

## Why
Bestandsaufnahme gegen ADR 0049 (Ruhige Listen) am 2026-10-19. Bericht und Vorher-Bilder im Repo:
`docs/ux/design-system/audit-2026-10-19/report.md` (Befunde S1 bis S5, S7) und
`pc-1440x900-import-pruefen*.png`, `handy-390x844-import-pruefen*.png`,
`handy-390x844-import-split-sheet.png` im selben Ordner.

Entscheidung Roman (2026-10-19): auch "Import prüfen" wird ruhiger, nicht nur die Formate.
Maßstab sind ADR 0049, Styleguide §2 "Ruhige Listen" und das Kassenbuch.

Die Duplikat-Details (S6) sind abgetrennt in `ynab-sync-e4gm3`.

## What
Die Zeilen bleiben `div.tx-row` mit eigenem CSS, kein Umbau auf `Table`. Zeilenangaben sind
ungefähr, per Symbol suchen.

1. **Deutsche Formate (S2).**
   - Neuer reiner Helfer in `SyncFlow/ViewHelpers.fs`: `formatGroupDate (today: DateTime) (d: DateTime)`
     liefert "Heute", "Gestern", sonst "10.10.2026" (rein numerisch wie `Ledger/View.fs` ~34-50, keine
     Monatsnamen). Ersetzt `date.ToString("dd. MMMM yyyy", ...)` in `TransactionList.fs` ~219.
     Nicht aus `Ledger/View.fs` importieren (keine Abhängigkeit zwischen den Seiten).
   - Tagessumme im Gruppenkopf über `Money.formatAmountText (sum > 0m) true sum "EUR"`, ersetzt
     `ToString("N2", ...)` in `TransactionList.fs` ~225.
   - `formatAmountForRow` in `TransactionRow.fs` fällt weg; der Betrag nutzt `Money.listAmount`
     (Eingang Teal mit "+", Ausgabe neutral). Die Dämpfung übersprungener Zeilen
     (`.status-skipped .tx-amount`) wird auf die neue Betragsklasse gelegt.
2. **Pro Zeile nur die Kante farbig (S1).**
   - `getCategoryBadgeClass` liefert für alle Fälle außer "ohne Kategorie" einen neutralen Chip
     (`text-secondary`, 12px, ohne Teal-Fläche). `.badge-ready` wird entfernt. "Kategorie…" bleibt
     Orange (`.badge-attention`).
   - Die gefüllte Teal-Checkbox (`.tx-toggle input:checked + .toggle-track`, `styles.css` ~1417)
     wird neutral. Den Zustand meldet die Statuskante.
   - Statuskanten bleiben, wie sie sind (Teal bereit, Orange offen).
3. **Datum nur im Gruppenkopf (S3).** `span.tx-date` mit `formatDateCompact` entfällt aus der Zeile;
   `formatDateCompact` wird gelöscht, wenn es keine weiteren Nutzer hat. `.date-header` folgt
   `ListRoles.groupHeader`, die Tagessumme der Rolle `ListRoles.date`.
4. **Volle Breite am PC (S4).** `src/Client/View.fs` ~49-52: die Import-Seite nutzt
   `Navigation.pageContentFull` und setzt ihre Ränder selbst (`px-4 md:px-6`), wie das Kassenbuch.
   Die Zeilen bleiben Karten-Zeilen.
5. **Importieren-Knopf (S5).** Beide Knöpfe (`btn-import-sm` oben, `btn-import` unten,
   `TransactionList.fs` ~100-110 und ~295-305) werden `Button.primary` (Orange, ohne Leuchten).
   `.btn-import`, `.btn-import-sm` und ihre `.ready`-Varianten samt Glow-Animation entfallen, soweit
   sonst unbenutzt. Hinweis "N ohne Kategorie" und Fortschrittsbalken bleiben.
6. **Aufteilen-Sheet (S7, `SplitSheet.fs` ~176 und ~205).** Gesamt und Rest über
   `Money.formatAmountText` in einer neutralen Klasse (`splitAmountClass`: Mono, 500, tabular-nums,
   `text-text-primary`). `.split-remainder` verliert die rote Fläche, `.balanced` das Grün; der
   Rahmen ist neutral, nur die Beschriftung wechselt "Rest"/"Stimmt". Rot nur als Text, wenn
   Speichern wegen eines Rests gesperrt ist (vorher `saveEnabled` lesen).
7. **Styleguide.** `standards/frontend/styleguide.md` §2: den Satz "Vorbild ist die Ansicht Import
   prüfen" durch einen Verweis auf Kassenbuch und Styleguide 8a ersetzen.

## Acceptance criteria
- [ ] `formatGroupDate today d`: gleicher Tag → "Heute", Vortag → "Gestern", sonst "10.10.2026"; Datum mit Uhrzeit zählt als dieser Tag; Jahreswechsel (02.01. sieht 01.01. als "Gestern"). Expecto-Tests in `SyncFlowViewTests.fs`; keine Ausgabe enthält einen Monatsnamen.
- [ ] Tagessumme: `-116.00m` → "-116,00 €", `3113.30m` → "+3.113,30 €" (Test über die Funktion, die den Gruppenkopf-Text baut). `TransactionList.fs` enthält weder `"N2"` noch `MMMM` (Quelltext-Zusicherung).
- [ ] `TransactionRow.fs` enthält weder `formatAmountForRow` noch `ToString("0.00")`; die Betragsanzeige ruft `Money.listAmount` (Quelltext-Zusicherung).
- [ ] `getCategoryBadgeClass` hat je einen Test für Kategorie gesetzt, aufgeteilt, übersprungen, Duplikat und ohne Kategorie: nur "ohne Kategorie" liefert `badge-attention`, kein Fall liefert `badge-ready` oder eine Teal-Klasse.
- [ ] `badge-ready` kommt weder in `styles.css` noch im F#-Code vor (Grep-Wächter als Test).
- [ ] `styles.css`: `.toggle-track` im Zustand `:checked` enthält kein `neon-teal`; `.split-remainder` und `.split-remainder.balanced` enthalten weder `rgba(255, 59, 92` noch `rgba(0, 255, 136` (Grep-Wächter).
- [ ] `TransactionRow.fs` enthält weder `tx-date` noch `formatDateCompact`; `.tx-date` ist aus `styles.css` entfernt.
- [ ] `src/Client/View.fs` wählt `Navigation.pageContentFull` für die Import-Seite (Quelltext-Zusicherung). Bei 1440x900 ist die Liste breiter als 1280px (Playwright misst `.tx-list`, Wert im Diary).
- [ ] Beide Importieren-Knöpfe nutzen `Button.primary`; `"btn-import"` und `"btn-import-sm"` kommen im F#-Code nicht mehr vor, keine Glow-Animation am Knopf (Quelltext-Zusicherung).
- [ ] `SplitSheet.fs` ruft für Gesamt und Rest kein `Money.simple`; `splitAmountClass` enthält `font-mono`, `font-medium`, `tabular-nums` und weder `neon-green`, `neon-red`, `font-bold` noch eine Glow-Klasse (Test).
- [ ] `styleguide.md` §2 enthält "Vorbild ist die Ansicht Import prüfen" nicht mehr.
- [ ] Nachher-Bilder mit denselben 8 gemockten Buchungen wie im Audit (Playwright-Route auf `getCurrentSession` und `getTransactions`) unter `docs/ux/ynab-sync/import-pruefen-ruhig/`: `{pc-1440x900,handy-390x844}-import-pruefen.png`, `-import-pruefen-aufgeklappt.png`, `handy-390x844-import-split-sheet.png`. Vorher sind die Audit-Bilder (nur verweisen).
- [ ] Playwright-Textprüfung am gemockten Lauf: kein Monatsname in Großbuchstaben, kein Betrag mit Punkt als Dezimaltrenner im sichtbaren Text. Ergebnis im Diary.
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes`, `npm run test:scripts` grün.
- [ ] Die Zeilen in Import prüfen wirken so ruhig wie im Kassenbuch (`pc-1440x900-ledger.png`). [human-eye]

## Notes
- **Risiken:** `Swipe.SwipeableRow`, die Dämpfung übersprungener Zeilen und die Einblend-Verzögerungen (`styles.css` ~1954-1971, nth-child) hängen an Klassen und Struktur der Zeile. Beim Entfernen von `.tx-amount`/`.tx-date` nachziehen. Der sticky `.date-header` (`top: calc(108px ...)`) hängt an der Höhe der Kopfzeile; nach dem Umbau nachmessen. ADR-0005-Mechanik des Pickers nicht anfassen.
- **Orange doppelt:** Orange steht auf dieser Seite auch für "ohne Kategorie". Roman hat sich trotzdem für `Button.primary` entschieden; falls das im Ergebnis konkurriert, im Blick-Kriterium vermerken.
- **Konflikt mit `categorization-wnxd6`:** beide ändern dieselbe Weiche in `src/Client/View.fs` ~49-52. Nacheinander integrieren; der zweite Worker ergänzt die Weiche.
- **Nicht in dieser Aufgabe:** Duplikat-Details und englischer Debug-Block (`ynab-sync-e4gm3`), Umbau auf `Table.calmRow`, interne YNAB-Kategorien (`ledger-d218j`), Picker-Optik (zahv9), Pink als Statusfarbe, TAN- und Fertig-Ansicht.
- Größe M.

## Merge-conflict note (iteration 1)

- Sibling: categorization-wnxd6 (Regeln-Seite als ruhige Liste), integriert als fd8c469.
- Neue Basis: fd8c469 (feature/kassenbuch).
- Allow-list: src/Client/View.fs (pageShell-Weiche; die Regeln-Seite bekommt dort ebenfalls pageContentFull).
- Salvage: /Users/romansachse/src/BudgetBuddy/.agentheim/salvage/ynab-sync-aj5tv-merge-conflict.patch

## Outcome

Import prüfen ist an ADR 0049 angeglichen. Die Zeilen bleiben `div.tx-row` mit eigenem CSS.

- Formate: `ViewHelpers.formatGroupDate today d` ("Heute", "Gestern", sonst TT.MM.JJJJ, ohne Monatsnamen) und `ViewHelpers.formatGroupTotal` (über `Money.formatAmountText`, "+3.113,30 €") ersetzen `MMMM` und `N2` im Gruppenkopf (`TransactionList.fs`). `formatAmountForRow` ist weg, der Betrag ist `Money.listAmount` (Eingang Teal mit "+", Ausgabe neutral) in einem `.tx-amount`-Wrapper.
- Nur die Kante farbig: `getCategoryBadgeClass` liefert `badge-neutral` (text-secondary, 12px, ohne Fläche) für Kategorie, aufgeteilt, übersprungen; Duplikat bleibt `badge-duplicate`, "ohne Kategorie" bleibt als einziger Fall `badge-attention`. `.badge-ready` ist entfernt. Die gefüllte Teal-Checkbox ist neutral (text-secondary). Payee ist `text-primary`, 15px am Handy, 14px ab md.
- Datum nur im Gruppenkopf: `tx-date`, `.tx-date` und `formatDateCompact` sind gelöscht. `.date-header` folgt `ListRoles.groupHeader` (Mono 10px, 0.16em), `.date-total` der Rolle `ListRoles.date` (Mono 12px).
- Volle Breite: `View.fs` wählt für `SyncFlow` `Navigation.pageContentFull` (eine Zeile in der pageShell-Weiche plus Kommentar). `SyncFlow/View.fs` setzt die Ränder selbst: Review-Ansicht in `px-4 md:px-6`, die Zustände ohne Liste (Start, TAN, Laden) behalten `container mx-auto px-4`.
- Importieren: beide Knöpfe (oben Small, unten Medium) sind `Button.view` mit `Variant = Button.Primary`. Auch der Speichern-Knopf des Regel-Sheets (`InlineRuleForm.fs`) nutzte `btn-import` und ist jetzt `Button.Primary` mit `flex-1`. `.btn-import*`, `.ready`-Varianten, `importReady` und `sfGlowPulse` sind aus `styles.css` entfernt. Hinweis: `Button.Primary` trägt im DS ein statisches Orange-Leuchten; ich habe es nicht per Klasse überstimmt (Tailwind-Reihenfolge), nur die Pulsanimation ist weg.
- Aufteilen-Sheet: `SplitSheet.splitAmountClass warn` (font-mono, font-medium, tabular-nums, text-text-primary; mit `warn` nur text-neon-red) und `splitAmount` ersetzen `Money.simple` für Gesamt und Rest. `warn` gilt, wenn Rest ungleich 0 und Speichern gesperrt ist. `.split-remainder` hat neutralen Rahmen, `.balanced` ist entfernt (nur "Rest"/"Stimmt" wechselt).
- Styleguide §2: "Vorbild ist die Ansicht Import prüfen" ersetzt durch Verweis auf Kassenbuch und Abschnitt 8a in /styleguide.
- Tests (`src/Tests/SyncFlowViewTests.fs`, keine neue Datei): `formatGroupDate` (Heute, Uhrzeit, Gestern, älter, Jahreswechsel, kein Monatsname), `formatGroupTotal`, fünf `getCategoryBadgeClass`-Fälle (die vier `formatDateCompact`-Tests und die alten `badge-ready`-Tests sind ersetzt), `splitAmountClass` und Quelltext-Wächter (kein N2/MMMM/btn-import, Money.listAmount, kein tx-date/formatDateCompact/badge-ready, Checkbox und Rest-Zeile in styles.css, pageContentFull, zwei Button.Primary, kein Money.simple im SplitSheet, Styleguide-Satz). Die Wächter habe ich nicht einzeln durch Zurückdrehen rot geprüft; die alten Symbole sind im Code gelöscht, die Tests prüfen deren Abwesenheit.
- Läufe aus dem Worktree: `dotnet build` 0 Warnungen (die FS0025 in Ledger/State.fs taucht hier nicht mehr auf), `dotnet test` 1241 grün, 7 übersprungen (ein früherer Lauf hatte einmal einen roten, nicht reproduzierbaren Ledger-Persistenztest in LedgerTests.fs:520, der Folgelauf und der Abschlusslauf waren grün), `npm run build` grün, `npm run guard:classes` grün, `npm run test:scripts` 120 von 120 grün.
- Bilder: `docs/ux/ynab-sync/import-pruefen-ruhig/` (PC 1440x900 und Handy 390x844: Liste, aufgeklappt, Handy Aufteilen-Sheet), mit 8 erfundenen Buchungen und gemockten Routen. Ich habe PC-Liste, Handy-Liste und Aufteilen-Sheet angesehen. Das Mock-Skript liegt im Scratchpad (`scratchpad/ynab-sync-aj5tv/shots.mjs`), nicht im Repo. Wire-Form: Fable.Remoting verpackt Ein-Fall-Unions als `{"TransactionId": "..."}`, Ergebnisse als `{"Ok": ...}`.
- Messungen (gemockter Lauf): `.tx-list` bei 1440 Breite 1392px (vorher durch container auf 1280px begrenzt), am Handy 358px. Gruppenköpfe: "HEUTE | +2.997,68 €", "GESTERN | -66,90 €", "16.10.2026 | -1.329,50 €", "10.10.2026 | -23,40 €". Playwright-Textprüfung der Liste: kein Monatsname in Großbuchstaben, kein Betrag mit Punkt als Dezimaltrenner, in beiden Viewports. Alle acht Beträge deutsch ("-48,12 €", "+3.113,30 €", "-1.234,50 €"). Kein horizontaler Überlauf.
- Offen: Kriterium [human-eye] (Zeilen so ruhig wie im Kassenbuch) braucht Romans Blick. Orange konkurriert auf der Seite: "Kategorie…" (offen), Importieren und die orange Kante stehen im selben Bild; im Screenshot wirkt das tragbar, die Entscheidung bleibt bei Roman. Nicht angefasst: Duplikat-Details (ynab-sync-e4gm3), Umbau auf Table.calmRow, Swipe, Picker.
