---
id: categorization-wnxd6
title: Regeln-Seite als ruhige Liste (schlanker Kopf, volle Breite, Tabelle statt Karten)
status: todo
type: feature
context: categorization
created: 2026-10-19
completed:
depends_on: [design-system-zahv9]
blocks: []
tags: [frontend, ruhige-listen]
related_adrs: [0049]
related_research: []
prior_art: [design-system-q7rkm, ledger-n4vte]
---

## Why
Bestandsaufnahme gegen ADR 0049 (Ruhige Listen) am 2026-10-19. Bericht und Vorher-Bilder im Repo:
`docs/ux/design-system/audit-2026-10-19/report.md` (Befunde R1 bis R4) und
`pc-1440x900-rules*.png`, `handy-390x844-rules*.png` im selben Ordner. R5 ist mit
`design-system-zahv9` erledigt; übrig blieben englische Feldnamen im Pflichtfeld-Hinweis
("Bitte ausfüllen: Rule Name, Pattern, Category"), die von dieser Seite kommen.

Referenz für Kopf, Breite und Liste ist das Kassenbuch (`Ledger/View.fs`).

## What
Betroffen sind `src/Client/Components/Rules/View.fs` und eine Weiche in `src/Client/View.fs`.
Zeilenangaben ungefähr, per Symbol suchen.

- **Kopf und Breite (R1, R2).**
  - `PageHeader.gradientWithActions` (~532) wird `PageHeader.calm "Kategorisierungsregeln"` mit
    gedämpftem Untertitel aus der reinen Funktion `summaryText` ("12 Regeln, 11 aktiv").
  - Seitenrahmen `Primitives.Container.full`, wie `Ledger/View.fs` ~1283. In `src/Client/View.fs`
    ~49-52 bekommt `Rules` `Navigation.pageContentFull`; den Kommentar dort anpassen.
  - Aktualisieren am Handy als Symbolknopf in der Kopfzeile, am PC als sekundärer Knopf, Titel
    "Regeln neu laden". "Neue Regel" bleibt die eine primäre Aktion, das Menü ⋮ bleibt.
- **Liste statt Karten (R2).** `ruleRow` (~76-190) wird ersetzt.
  - PC: `<table>` mit `Table.calmHeaderCell` und `Table.calmRow`. Spalten: Reihenfolge (▲/▼ und
    `#n`), Muster (Mono, gedämpft, mit Typ), Name, Kategorie (`text-secondary`, nicht gekürzt),
    Status, keine Aktionsspalte.
  - Handy: zweizeilige Liste, Muster wie im Kassenbuch (`md:hidden` neben `hidden md:table`).
    Zeile 1 Name und Kategorie, Zeile 2 Muster und Typ (12px, gedämpft). Die Kategorie bekommt die
    Restbreite und bricht bei Bedarf um, statt auf drei Zeichen zu kürzen (R4).
  - Ladezustand `Loading.tableSkeleton` statt `Loading.cardSkeleton`; `emptyState` bleibt.
- **Zeile öffnet den Dialog.** Ein Klick auf die Zeile öffnet "Regel bearbeiten"; das Stift-Symbol
  entfällt. ▲/▼ bleiben als kleine Knöpfe (am Handy links neben dem Namen) mit
  `stopPropagation`.
- **Aktiv ist still (R3).** Die Zeilen-Checkbox (~120-126) entfällt. Aktive Regeln tragen kein
  Zeichen; inaktive zeigen das Wort "inaktiv" in `text-muted` und einen gedämpften Namen (ohne
  `opacity-50` auf der ganzen Zeile). Umschalten nur im Dialog über den bestehenden Schalter
  "Regel aktiv" (~373).
- **Löschen nur im Dialog (R4, Entscheidung Roman).** In der Liste gibt es kein Löschen-Symbol.
  Der Dialog bekommt im Bearbeiten-Modus einen Knopf "Regel löschen" (unten links, `Button.ghost`
  oder `danger`-Text ohne Fläche) mit der bestehenden zweistufigen Bestätigung
  (`ConfirmDeleteRule`/`DeleteRule`, `State.fs` ~80-104). Nach dem Löschen schließt der Dialog.
- **Deutsch.**
  - Pflichtfeld-Hinweis über eine reine Funktion `requiredFields form` mit den Namen
    "Regelname", "Muster", "Kategorie", passend zu den Labels im Dialog.
  - Anzeige der Mustertypen "Regex", "Enthält", "Exakt" über `patternTypeLabel`; die technischen
    Select-Werte `"Regex"/"Contains"/"Exact"` (~247-250) bleiben unverändert.
  - Titel "Refresh rules" und die Titel in `patternTypeIcon` werden deutsch; Tooltip "Pattern:"
    wird "Muster:".
  - Prioritätshinweis ohne Gedankenstrich, am Handy höchstens zwei Zeilen: "Die Regeln werden von
    oben nach unten geprüft. Die erste passende, aktive Regel gewinnt."

## Acceptance criteria
- [ ] `Rules/View.fs` enthält weder `gradientWithActions` noch `PageHeader.gradient`, aber `PageHeader.calm` und `Primitives.Container.full` (Quelltext-Zusicherung nach dem Muster `RuhigeListenTests`).
- [ ] `Rules/View.fs` enthält `Table.calmHeaderCell` und `Table.calmRow`; die Listenzeile enthält kein `bg-surface-card`, kein `rounded-lg` und kein `Card.` (der Leerzustand `Card.emptyState` bleibt erlaubt).
- [ ] `src/Client/View.fs` wählt `Navigation.pageContentFull` für `Rules` (Quelltext-Zusicherung).
- [ ] Die Listenzeile enthält kein `Input.toggle`, kein `Icons.trash` und kein `Icons.edit`; im Dialog bleibt `Input.toggle` für "Regel aktiv" (Quelltext-Zusicherung).
- [ ] Der Dialog im Bearbeiten-Modus enthält "Regel löschen" und dispatcht `ConfirmDeleteRule`; im Anlegen-Modus erscheint der Knopf nicht. State-Test: `ConfirmDeleteRule id` gefolgt von `DeleteRule id` löst den Löschaufruf aus und schließt den Dialog.
- [ ] `statusLabel true` = `""`, `statusLabel false` = `"inaktiv"` (Test).
- [ ] `summaryText`: 0 Regeln → `""`, 1 Regel → "1 Regel, 1 aktiv", 12 Regeln davon 11 aktiv → "12 Regeln, 11 aktiv" (Test).
- [ ] `patternTypeLabel` liefert "Regex", "Enthält", "Exakt"; der Rundlauf zum technischen Schlüssel ist für alle drei Fälle verlustfrei (Test).
- [ ] `rowClass false` ist gedämpft, enthält aber weder `opacity-50` noch eine `neon-`-Klasse (Test).
- [ ] Pflichtfeld-Hinweis bei leerem Formular: `Form.missingFieldsText (requiredFields form)` = "Bitte ausfüllen: Regelname, Muster, Kategorie" (Regressionstest, Kommentar nennt den englischen Vorzustand).
- [ ] `Rules/View.fs` enthält keines der Literale "Rule Name", "Refresh rules", "Regex pattern", "Contains substring", "Exact match", "Pattern:" und keinen Gedankenstrich (— oder –) im Prioritätshinweis (Quelltext-Zusicherung).
- [ ] Bestehende Tests (`RulesReorderTests`, `RulesEngineTests`, DS-Tests) bleiben unverändert grün. `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes`, `npm run test:scripts` grün.
- [ ] Nachher-Bilder unter `docs/ux/categorization/regeln-ruhige-liste/`: `{pc-1440x900,handy-390x844}-rules.png`, `-rules-modal.png` (Bearbeiten mit Löschen-Knopf) und eine Ansicht mit einer inaktiven Regel. Vorher sind die Audit-Bilder (nur verweisen).
- [ ] Bei 1440x900 ist die Regeltabelle so breit wie der Viewport minus 2 mal 24px (Playwright misst, Wert im Diary).
- [ ] Am Handy sind Name und Kategorie ohne Abkürzung lesbar, "inaktiv" ist als Wort erkennbar. [human-eye]

## Notes
- **Risiken:** Die Tabelle braucht eine feste Breite für die Spalte mit ▲/▼, sonst springen die Spalten; zwei Knöpfe in 44px Zeilenhöhe sind knapp (kleine Symbolknöpfe oder etwas höhere Zeile, per Screenshot belegen). Die Select-Schlüssel nicht mit umbenennen, sonst bricht das Formular still. Die Tastatur-Bedienung des Menüs ⋮ (`group-focus-within`) nur anfassen, wenn der Umbau sie bricht. Ganze Zeile klickbar: per Tastatur erreichbar machen (`tabIndex`, Enter).
- **Konflikt mit `ynab-sync-aj5tv`:** beide ändern die Weiche in `src/Client/View.fs` ~49-52. Nacheinander integrieren.
- `PageHeader` Calm bekommt in `ledger-m23nz` einen Fußabstand `mb-4`; gewollt auch hier.
- **Wiederverwendung:** `PageHeader.calm`, `Primitives.Container.full`, `Navigation.pageContentFull`, `Table.calmHeaderCell`/`calmRow`/`calmRowClass`, `ListRoles`, `Loading.tableSkeleton`, Testhelfer aus `RuhigeListenTests`. Keine neuen DS-Bausteine.
- **Nicht in dieser Aufgabe:** Regel-Engine, Reihenfolge-Logik, Persistenz und API; Drag-and-drop; Suche oder Filter über der Liste; Export/Import im Menü ⋮.
- Größe M, eine Aufgabe. R1 bis R4 und die Übersetzungen liegen in derselben Datei; ein Zwischenstand mit neuem Kopf und alten Karten wäre ein Rückschritt.
