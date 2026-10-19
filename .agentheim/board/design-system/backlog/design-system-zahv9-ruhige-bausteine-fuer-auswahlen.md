---
id: design-system-zahv9
title: Ruhige Bausteine für Auswahlen: Picker-Zeile, Money.available, Suchfeld, Footer, searchableSelect und Form auf Deutsch
status: backlog
type: feature
context: design-system
created: 2026-10-19
completed:
depends_on: []
blocks: [ledger-m23nz, ynab-sync-aj5tv, categorization-wnxd6]
tags: [captured, frontend, ruhige-listen]
related_adrs: [0049, 0005]
related_research: []
prior_art: [design-system-q7rkm, ledger-n4vte]
---

## Why
Bestandsaufnahme gegen ADR 0049 (Ruhige Listen) am 2026-10-19. Die Kategorie-Auswahlen
(Kassenbuch-Filter, Import prüfen, Aufteilen, Quick Add, Regel-Dialog, Styleguide) zeigen
eine "grüne Wand" aus Verfügbar-Beträgen, 16px-Zeilen mit Umbruch, große Suchfelder, einen
leuchtenden "Übernehmen (0)"-Knopf und englische Texte. Die Ursachen liegen in den
DS-Bausteinen, nicht in den Seiten, deshalb wird es einmal im DS behoben.

Bericht und Aufnahmen im Repo: `docs/ux/design-system/audit-2026-10-19/` (`report.md`,
Befund-Nummern K, P, R; Bilder `pc-1440x900-*.png` und `handy-390x844-*.png`, dort ohne
`shots/`-Unterordner).

Drei Aufgaben warten auf diese: `ledger-m23nz` und `ynab-sync-aj5tv` (Picker),
`categorization-wnxd6` (searchableSelect und Form).

## What
Werte aus Styleguide §2 "Ruhige Listen" und `Tokens.ListRoles` (aus design-system-q7rkm).
Arbiträre Größen nur in `Tokens.fs`.

1. **`Money.available` ruhig** (K2, P1). An Ort und Stelle ändern, es wird nur von den zwei
   Pickern benutzt (`CategoryMultiSelect.fs:41`, `BottomSheet.fs:395`). Mono 500,
   `tabular-nums`, Größe über `ListRoles.primarySize`, kein Glow. Positiv `text-primary`,
   0 `text-muted`, negativ Red. Kein Grün, kein Teal. Als reine Klassenfunktion
   (Muster `Money.listAmountClass`).
2. **Gemeinsame Picker-Bausteine** (P4). Ein DS-Modul (Name frei, z. B.
   `DesignSystem/PickerParts.fs`) liefert Picker-Zeile, Gruppenkopf und Suchfeld.
   `CategoryMultiSelect` (Popover, Mehrfachauswahl) und `BottomSheet.categoryPicker`
   (Sheet, Einfachauswahl) nutzen beide diese Bausteine. Verhalten bleibt getrennt:
   Checkbox und Übernehmen im Popover, Tippen und Schließen im Sheet. Die ADR-0005-Mechanik
   (Click-Commit, `onMouseDown preventDefault`, Swallow-Guard, Scroll-Lock) bleibt
   unverändert.
3. **Picker-Zeile** (K1, P2). Name in `ListRoles.primary` (15px Handy, 14px PC, 500) mit
   `truncate`, Betrag rechts in fester Breite, Zeile einzeilig. `Input.checkbox` bekommt eine
   Variante, deren Label diese Größe trägt und kürzt (heute ohne Größe, also 16px).
4. **Gruppenkopf** (P2) im Stil `ListRoles.groupHeader`. Im Bottom-Sheet erscheint
   "Alle Kategorien" nur, wenn darüber "Vorgeschlagen" oder "Zuletzt verwendet" steht;
   sonst führen allein die Gruppenköpfe.
5. **Kompaktes Suchfeld** (K4, P3, K9). `Input.Small` wird `text-base md:text-sm`, 36px
   hoch; das Inline-`style.fontSize 16` in `Input.text` (`Input.fs:81`) gilt nur noch für
   die anderen Größen. `Input.Small` wird heute nirgends benutzt, es färbt also nichts um.
   Das Picker-Suchfeld nutzt dieselben Werte; `.sheet-search input` (`styles.css`) verliert
   den doppelten Teal-Ring (ein Fokusrand, kein zweiter Ring).
6. **Picker-Footer** (K3). "Übernehmen" als kompakter Primärknopf ohne Glow, einzeilig
   (`whitespace-nowrap`). Zahl nur bei Auswahl: "Übernehmen" bzw. "Übernehmen (3)".
   "Zurücksetzen" bleibt Ghost.
7. **`searchableSelect`** (R5, `Input.fs:705`). Optionen 14px PC / 15px Handy wie die
   Picker-Zeile. Labels der Form "Gruppe: Name" werden nach Gruppen gegliedert
   (Gruppenkopf wie Punkt 4, Zeile zeigt nur den Namen), Suche findet weiter Gruppe und
   Name. Deutsche Texte: Platzhalter der Suche "Suchen…", leere Trefferliste
   "Keine Treffer". Suchfeld kompakt wie Punkt 5. Im Regel-Dialog
   (`Rules/View.fs:312`) ist die Liste bis zur letzten Option erreichbar
   (heute vom Modal abgeschnitten, siehe `pc-1440x900-rules-modal-category.png`).
8. **`Form.fs` auf Deutsch** (R5). "Please fill in: …" wird "Bitte ausfüllen: …"
   (`Form.fs:45,77` und jede weitere Stelle mit demselben Text).
9. **Styleguide.** `standards/frontend/styleguide.md` §7.1 Punkt 3 präzisieren:
   Suchfelder ≥ 16px unterhalb `md`, ab `md` 14px erlaubt (Grund bleibt der iOS-Zoom).
   §2 bzw. 8a um "Auswahl" ergänzen (Zeile, Gruppenkopf, Verfügbar-Betrag, Suchfeld,
   Footer). `/styleguide`-Route: Abschnitt 8a zeigt die Auswahl; die bestehenden
   Picker-Demos (`Styleguide/View.fs:630,822,1020`) folgen automatisch.

Nicht Teil dieser Aufgabe: Popover-Breite und Filterfelder im Kassenbuch (`ledger-m23nz`),
interne YNAB-Kategorien oben (K6, `ledger-d218j`), Seitenumbau der Regeln
(`categorization-wnxd6`).

## Acceptance criteria
- [ ] Die Klassenfunktion von `Money.available` liefert für positiv `text-text-primary`
      (über `Tokens.Colors.textPrimary`), für 0 die `text-muted`-Klasse, für negativ
      `text-neon-red`; keine Ausgabe enthält `neon-green`, `neon-teal`, `font-semibold`,
      `font-bold` oder eine Glow-Klasse; alle enthalten `font-mono`, `font-medium`,
      `tabular-nums`. Getestet über eine reine Funktion.
- [ ] Ein DS-Modul für Picker-Bausteine existiert; `CategoryMultiSelect.fs` und
      `BottomSheet.fs` rufen beide dessen Zeile und Gruppenkopf auf
      (Quelltext-Zusicherung wie in `LedgerViewTests`), und keine der beiden Dateien
      ruft `Money.available` noch direkt.
- [ ] Die Klassen der Picker-Zeile enthalten `ListRoles.primary` und `truncate`; der
      Betragsbereich hat eine feste Breite oder `shrink-0`. Die neue `Input.checkbox`-Variante
      setzt am Label dieselbe Größe und `truncate`. Getestet über reine Klassenfunktionen.
- [ ] Der Gruppenkopf beider Picker nutzt `ListRoles.groupHeader`. Eine reine Funktion
      entscheidet, ob "Alle Kategorien" erscheint: nein ohne Vorschläge und ohne Zuletzt
      verwendet (oder bei aktiver Suche), ja sonst. Je ein Test.
- [ ] `Input.Small` liefert `text-base md:text-sm` und 36px Mindesthöhe; `Input.text` mit
      `Size = Small` setzt kein Inline-`fontSize`; `Medium` und `Large` setzen es weiter
      (Regressionstest mit Kommentar: Inline-Style schlug die Tailwind-Größe).
- [ ] `.sheet-search input:focus` in `styles.css` erzeugt höchstens einen Rand
      (kein zusätzlicher `box-shadow`-Ring); das Suchfeld des Sheets hat ab `md` 14px,
      darunter 16px.
- [ ] Footer-Beschriftung als reine Funktion: 0 ausgewählt ergibt "Übernehmen",
      3 ergibt "Übernehmen (3)". Der Knopf trägt `whitespace-nowrap` und keine Glow- oder
      `shadow-glow`-Klasse.
- [ ] `searchableSelect`: eine reine Funktion gruppiert `(value, "Gruppe: Name")`-Optionen
      in Gruppen mit Namen ohne Präfix; Labels ohne ": " landen in einer Gruppe ohne Kopf;
      die Suche findet eine Option sowohl über den Gruppennamen als auch über den Namen.
- [ ] `Input.fs` und `Form.fs` enthalten weder "Type to search", "No matches found" noch
      "Please fill in" (Quelltext-Zusicherung); die deutschen Texte "Suchen…",
      "Keine Treffer" und "Bitte ausfüllen:" stehen dort.
- [ ] Im Regel-Dialog ist bei 1440 × 900 die letzte Kategorie der `searchableSelect`-Liste
      per Scrollen sichtbar zu machen (Playwright-Prüfung oder Screenshot nach Scrollen ans
      Listenende unter `docs/ux/design-system/screenshots/`).
- [ ] `standards/frontend/styleguide.md` §7.1 nennt "≥ 16px unterhalb `md`" statt
      pauschal "≥ 16px"; der Styleguide beschreibt die Auswahl mit Zeile, Gruppenkopf,
      Verfügbar-Betrag, Suchfeld und Footer.
- [ ] `/styleguide` zeigt im Abschnitt 8a die Auswahl. Screenshots in 1440 × 900 und
      390 × 844 liegen unter `docs/ux/design-system/screenshots/`
      (`npm run screenshot:route -- '#/styleguide' --out docs/ux/design-system/screenshots/`).
- [ ] Keine neue arbiträre Schriftgröße außerhalb von `Tokens.fs`, keine Hex-Werte, keine
      Standard-Tailwind-Farben, kein neues `style.fontSize` (Grep-Checks aus
      `.claude/rules/design-tokens.md`); `npm run guard:classes` grün.
- [ ] Kategorie-Filter im Kassenbuch und Kategorie-Sheet in Quick Add wirken neben der
      ruhigen Tabelle (Styleguide 8a) gleich ruhig: Namen hell und einzeilig, Beträge
      neutral, Rot nur bei negativem Verfügbar, kein Leuchten im Footer. Vergleich mit
      `docs/ux/design-system/audit-2026-10-19/pc-1440x900-ledger-filter-Kategorie.png` und
      `handy-390x844-quickadd-category.png`. [human-eye]
- [ ] `dotnet build`, `dotnet test` und `npm run build` sind grün.

## Notes
- Romans Entscheidungen vom 2026-10-19 (Verfeinerung): eine Aufgabe statt zwei;
  Suchfeld 16px Handy / 14px PC mit Präzisierung von §7.1; "Übernehmen" kompakt ohne Glow,
  Zahl nur bei Auswahl; gemeinsame Bausteine statt einer zusammengelegten Komponente.
- `Money.available` darf an Ort und Stelle geändert werden (nur zwei Aufrufer, beide
  Picker). Für Einzelbeträge mit Gewicht gilt ADR 0049 weiter mit Grün und Rot; ein
  Verfügbar-Betrag in einer Auswahlliste ist eine wiederkehrende Zeile.
- `Button.Small` (`Button.fs:64`) ist `text-xs`; für den Footer eher eine eigene kompakte
  Klasse ohne Glow als `Button.primary` mit Ausnahme-Klassen. Darauf achten, dass
  `Button.view` seit design-system-qz7b6 die Display-Klasse aus `ClassName` respektiert.
- Zum abgeschnittenen Dropdown im Modal: Ursache im Worker klären (vermutlich
  `overflow-hidden` am Modal-Körper oder absolute Positionierung ohne Platz nach unten).
  Wenn die Lösung `Modal.fs` berührt, nur additiv, damit andere Dialoge gleich bleiben.
- Prüfen, ob `Rules/View.fs:312` die Optionen bereits als "Gruppe: Name" baut; die
  Gruppierung soll im DS liegen, nicht in der Seite.
