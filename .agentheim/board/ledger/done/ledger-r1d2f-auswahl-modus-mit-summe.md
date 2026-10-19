---
id: ledger-r1d2f
title: "Auswahl-Modus mit Summe der Auswahl (Handy und PC)"
status: done
type: feature
context: ledger
created: 2026-09-12
completed:
depends_on: [ledger-vgfw3, design-system-001]
blocks: [ledger-nhwrt]
tags: [frontend, kassenbuch, scheibe]
related_adrs: []
related_research: []
prior_art: [ledger-vgfw3]
---

## Why
Dritte lesende Scheibe. Roman markiert heute in YNAB-Web einzelne Zeilen, um ihre Summe zu
sehen (Beispiel Steuererklärung: Kategorie wählen, die letzten zwölf Buchungen markieren,
Gesamtbetrag ablesen). Am Handy geht das gar nicht, er hätte es aber gern.

Der häufige Fall ist mit der Summenzeile aus `ledger-vgfw3` bereits abgedeckt. Diese
Scheibe ergänzt die Teilmenge.

## What
Maße, Farben und Texte: `docs/ux/kassenbuch/handoff-ablauf-3-mehrfachauswahl-mit-summe.md`
(Screens 3.1 bis 3.7, verbindlich). Dieser Abschnitt legt das Verhalten fest.

### Auswahl als Menge über den Treffer
- Zustand im Client (`src/Client/Components/Ledger/State.fs`):
  `Selection = NoSelection | Picked of Set<Guid> | AllExcept of Set<Guid>` plus
  `SelectionAnchor: Guid option` und am Handy `SelectionMode: bool`.
- `Picked` enthält nur geladene Zeilen (man kann nur antippen, was man sieht).
  `AllExcept` entsteht durch "Alle" bzw. das Kopfzeilen-Kästchen; abwählen kann man wieder
  nur geladene Zeilen, also sind auch die Ausnahmen immer geladen.
- **Anzahl und Summe der Auswahl rechnet der Client**, ohne neuen Server-Aufruf:
  - `Picked ids`: Anzahl und Summe der geladenen Zeilen in `ids`.
  - `AllExcept ex`: `TotalCount - |ex|` und `TotalAmount - Summe(ex)`.
  - Bei Summen gilt dieselbe Regel wie für `TotalAmount`: in YNAB gelöschte Buchungen
    zählen nicht zur Summe (siehe `applyReviewMarkResult`).
  - Eine reine Funktion (`selectionTotals`) liefert beides; Summenzeile und Leiste lesen
    nur sie.
- Nachladen unter `AllExcept`: neu geladene Zeilen erscheinen gewählt.
- Verlässt eine Zeile die Liste (Prüfvermerk bei aktivem Chip, `ledger-r777c`), fällt sie
  aus `Picked` bzw. aus den Ausnahmen; Anzahl und Summe bleiben stimmig, weil `TotalCount`
  und `TotalAmount` schon nachgeführt werden.
- **Konto-, Filter- oder Chipwechsel leert die Auswahl** und beendet den Modus. Die Auswahl
  bezieht sich auf einen Treffer.

### Handy
- Langes Drücken auf eine Zeile startet den Modus mit dieser Zeile gewählt.
- Im Modus wählt ein Tipp auf die Zeile oder ihr Kästchen aus bzw. ab. Ein Detail gibt es
  im Kassenbuch noch nicht (`ledger-q1hw2`); wenn es kommt, entscheidet jene Task, wie man
  aus dem Modus ins Detail gelangt.
- Die Auswahl-Leiste ersetzt die Tab-Bar: Reihe 1 "n ausgewählt" und Summe, Reihe 2 "Alle"
  und "Auswahl aufheben". Abwählen der letzten Zeile beendet den Modus nicht.
- "Alle" setzt `AllExcept ∅` und trägt dann den Teal-Aktivzustand; "Alle" nochmal leert die
  Auswahl, der Modus bleibt.
- "Auswahl aufheben" beendet den Modus, die Tab-Bar kommt zurück.

### PC
- Kästchen je Zeile; ein Klick wählt aus bzw. ab und setzt den Anker.
- Shift-Klick wählt alle geladenen Zeilen zwischen Anker und Klick (Listenreihenfolge)
  dazu; unter `AllExcept` nimmt er sie aus den Ausnahmen. Der Anker trägt
  `box-shadow:inset 2px 0 0 #00d4aa` und bleibt bis zum nächsten einfachen Klick.
- Kopfzeilen-Kästchen, Zustand abgeleitet aus Anzahl der Auswahl gegen `TotalCount`:
  leer / Teal-Strich / Haken. Klick bei leer oder Strich wählt den ganzen Treffer
  (`AllExcept ∅`), Klick bei Haken leert die Auswahl.
- Die Auswahl-Leiste erscheint, solange die Auswahl nicht leer ist, und ersetzt den
  Tastatur-Hinweis: "n ausgewählt", Summe, "Auswahl aufheben". Kein "Alle".

### Beide Geräte
- Solange die Auswahl nicht leer ist, zeigt die Summenzeile "Auswahl: n von N Buchungen"
  mit der Summe der Auswahl und der Teal-Kante. Die Treffer-Form der Summenzeile am PC bleibt,
  wie `ledger-vgfw3` sie gebaut hat.
- Der Ausstieg heißt überall "Auswahl aufheben".
- **Keine schreibenden Aktionen in der Leiste** (Entscheidung Roman 2026-10-19): "Geprüft"
  kommt mit `ledger-nhwrt`, "Kategorie ändern" und "Flag" mit ihren eigenen Tasks. Bis dahin
  sind sie weder sichtbar noch deaktiviert gezeigt. Die Handy-Leiste hat deshalb zwei
  Reihen statt drei.
- Tastaturkürzel (Leertaste, `G`) sind nicht Teil dieser Task.

## Acceptance criteria
- [ ] `selectionTotals`: für `Picked` Anzahl und Summe der gewählten geladenen Zeilen; für
      `AllExcept ex` `TotalCount - |ex|` und `TotalAmount - Summe(ex)`; in YNAB gelöschte
      Buchungen zählen in keinem Fall zur Summe (Unit-Tests, je ein Fall).
- [ ] Reducer-Test: langes Drücken startet den Modus mit genau dieser Zeile; ein Tipp im
      Modus wählt ab, ein zweiter wieder aus; Abwählen der letzten Zeile lässt den Modus an.
- [ ] Reducer-Test: "Alle" ergibt `AllExcept ∅` mit Anzahl `TotalCount` und Summe
      `TotalAmount`, auch wenn nur eine Seite geladen ist; "Alle" nochmal leert die Auswahl.
- [ ] Reducer-Test: unter `AllExcept` lädt eine weitere Seite nach, ihre Zeilen gelten als
      gewählt; eine abgewählte Zeile senkt Anzahl und Summe um genau ihren Betrag.
- [ ] Reducer-Test: Shift-Klick vom Anker (Zeile 1) auf Zeile 12 wählt zwölf Zeilen, der
      Anker bleibt Zeile 1; ein einfacher Klick setzt den Anker neu.
- [ ] Reducer-Test: Kopfzeilen-Kästchen leer → alle; Strich → alle; Haken → leer. Der
      abgeleitete Zustand (leer / Strich / Haken) ist für alle drei Fälle getestet.
- [ ] Reducer-Test: Konto-, Filter- und Chipwechsel leeren die Auswahl und beenden den Modus.
- [ ] Reducer-Test: mit aktivem Chip "Zu prüfen" verlässt eine gewählte Zeile nach einem
      Prüfvermerk die Liste; sie ist danach nicht mehr in der Auswahl, Anzahl und Summe
      stimmen (`Picked` und `AllExcept`, je ein Test).
- [ ] Ansichts-Test: Summenzeile zeigt bei leerer Auswahl den Treffer, sonst
      "Auswahl: n von N Buchungen" mit der Summe der Auswahl.
- [ ] Ansichts-Test: Handy-Leiste zeigt "n ausgewählt", Summe, "Alle" und
      "Auswahl aufheben"; PC-Leiste dasselbe ohne "Alle". Keine der beiden zeigt
      "Geprüft", "Kategorie ändern" oder "Flag".
- [ ] Ansichts-Test: der Anker der Spanne trägt die Teal-Kante, andere gewählte Zeilen nicht.
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes` grün.
- [ ] Handy 390x844 und PC 1440x900 entsprechen den Screens 3.1 bis 3.7 bis auf die
      weggelassenen schreibenden Aktionen; die Leiste verdeckt am Handy keine gewählte Zeile
      dauerhaft. [human-eye]

## Notes
Geschnitten in `ledger-s8m3w` am 2026-09-12. Brief: `docs/ux/kassenbuch/briefs.md`,
Ablauf 3. Handoff: `docs/ux/kassenbuch/handoff-ablauf-3-mehrfachauswahl-mit-summe.md`.

Am 2026-09-18 aus den fertigen Screens nachgetragen (Kopfzeilen-Kästchen über den ganzen
Treffer, Anker der Shift-Spanne, Beschriftung des Ausstiegs, Höhen der Handy-Leiste). Die
Aktion "Kopieren" aus dem ursprünglichen Brief ist gestrichen (Entscheidung Roman
2026-09-14, war eine Erfindung im Brief).

Verfeinert am 2026-10-19. Entscheidungen Roman: schreibende Aktionen weglassen statt
deaktiviert zeigen; Kopfzeilen-Kästchen bei Teilauswahl erweitert auf alle. Als Standard
gesetzt (Vorschlag aus dem Handoff, offene Punkte 1, 3, 4): Wechsel leert die Auswahl, Tipp im
Modus wählt aus, Summenzeile am PC bleibt in der Form von `ledger-vgfw3`. Offene Punkte 5
und 6 des Handoffs (Chips "Flag"/"Gelöschte zeigen", Reihenfolge der Konto-Karten)
betreffen Ablauf 2 und sind nicht Teil dieser Task.

Die Auswahl-Summe braucht keinen Server: abwählbar sind nur geladene Zeilen, deshalb lässt
sich `AllExcept` aus den Server-Summen des Treffers minus den Ausnahmen rechnen. Das Feld
`selectionTotal` aus dem Handoff ("vom Server") ist damit abgeleitet.

## Verifier note (iteration 1)

**REASONS:**
- Check 5 (BC-README-Abgleich): das README_DELTA hängt nur den Begriff "Auswahl" an. Im Abschnitt "Oberfläche (ledger-xbrht, 2026-09-12)", Absatz "**Einstieg und Navigation.**" (Ledger-README, etwa Zeile 417) steht weiter das Gegenteil: "der Wechsel zwischen Gesamtsicht und Konto lässt alle anderen Filter und eine laufende Auswahl stehen". Der Code leert die Auswahl bei jedem Wechsel, wie die Task es verlangt. Mit dem Delta allein widerspräche das README sich selbst und dem Code.
- Alle Prüfungen davor bestanden (Kriterien je Test belegt, dotnet test 1448/0, test:scripts 137/137, guard:classes, 0 Warnungen, Schreibregel). Abweichungen (neutrale Summe nach ADR 0049, Spaltenbreiten, Leiste im Root-View) sind tragfähig. Randfall Rückgängig unter AllExcept widerspricht keinem Kriterium.

**SUGGESTED_FIX:** Den Absatz "Einstieg und Navigation" so fassen, dass der Wechsel zwischen Gesamtsicht und Konto die übrigen Filter stehen lässt, eine laufende Auswahl aber leert. Mit `npm run check:readme-delta -- --bc ledger --delta -` prüfen; erreicht das Gate den Prosa-Absatz nicht, setzt der Conductor den Text von Hand.

**ITERATION_HINT:** likely-fixable

## Outcome

Dritte lesende Scheibe des Kassenbuchs ist gebaut. Handy: langes Drücken (500 ms, nur Touch, Toleranz 10 px) startet den Modus mit genau dieser Zeile, ein Tipp auf Zeile oder Kästchen wählt aus bzw. ab, die letzte Zeile abzuwählen lässt den Modus an. Die zweireihige Leiste (Reihe 1 "n ausgewählt" und Summe, Reihe 2 "Alle" als Chip mit Teal-Aktivzustand und "Auswahl aufheben") ersetzt die Tab-Bar. PC: Kästchen-Spalte (44 px), Kopfzeilen-Kästchen (leer, Teal-Strich, Haken, `aria-checked="mixed"`), Shift-Klick wählt die Spanne zwischen Anker und Klick, der Anker trägt die Teal-Kante, einreihige Leiste ohne "Alle". Die Summenzeile zeigt "Auswahl: n von N Buchungen" mit Teal-Kante, solange die Auswahl nicht leer ist (auch ohne aktiven Filter), sonst wie bisher den Treffer.

Entscheidungen und Abweichungen:
- **Modell** (`Components/Ledger/Types.fs`): `Selection = NoSelection | Picked | AllExcept`, `SelectionAnchor`, `SelectionMode`. Reine Funktionen: `selectionTotals` (Anzahl und Summe; gelöschte Buchungen zählen nicht zur Summe), `headerCheckState`, `toggleSelected`, `addToSelection`, `rangeBetween`, `removeFromSelection`, `isSelected`. Der Reducer (`State.fs`) hat sechs neue Nachrichten (`StartSelectionMode`, `ToggleRowSelected`, `ClickRowCheckbox`, `ToggleSelectAll`, `ToggleHeaderCheckbox`, `ClearSelection`), kein Proxy-Aufruf.
- **Wechsel leert die Auswahl** über `resetEntries`: alle Konto-, Filter- und Chipwechsel laufen dort durch, ebenso ein Neuladen (auch nach einem Spiegel-Lauf, die Auswahl bezog sich auf den alten Stand).
- **Zeile verlässt die Liste** (`applyReviewMarkResult`, Chip "Zu prüfen"): sie fällt aus `Picked` bzw. aus den Ausnahmen, ebenso der Anker. `LocallyRemoved` bleibt unberührt (ein Test prüft, dass der Zähler weiter hochzählt). Randfall: macht der Nutzer eine Zeile unter `AllExcept` rückgängig, die als Ausnahme (abgewählt) die Liste verließ, erscheint sie nach "Rückgängig" gewählt (die Ausnahme ist weg, `TotalCount` steigt wieder). Bei einer gewählten Zeile ist das Verhalten stimmig. Nicht weiter behandelt.
- **Wisch und langes Drücken**: im Auswahl-Modus ist die Wisch-Geste aus (`mobileEntryRow` nimmt dann die Zeile ohne `SwipeableRowRight`), auch die Schaltfläche Geprüft/Ungeprüft an der Zeile entfällt dort. Die Hülle `LongPressRow` bleibt im Modus stehen (nur `enabled` fällt), damit der Klick nach dem langen Drücken dieselbe Hülle trifft und verschluckt wird (sonst würde die eben gewählte Zeile gleich wieder abgewählt). Bewegt sich der Finger über 10 px, fällt der Zeitgeber, ein Wisch bleibt also ein Wisch.
- **Fixe Leiste außerhalb der Seitenhülle**: `animate-page-enter` setzt einen Transform, in dem `fixed` an der Hülle statt am Fenster haftet (im Screenshot lag die Leiste zuerst am Seitenende). `Components.Ledger.View.selectionBars` wird deshalb im Haupt-`View.fs` neben den Toasts gerendert; die Tab-Bar weicht über das vorhandene `hideBottomNav`. In der Spalte stehen Abstandshalter in Höhe der Leiste (Handy h-40, PC h-20), die letzte Zeile bleibt erreichbar (im Playwright-Wächter gemessen: letzte Zeile endet über der Leiste).
- **Neuer DS-Baustein** `src/Client/DesignSystem/MultiSelect.fs` (Kästchen mit drei Zuständen 20 px Handy und 17 px PC, `boxButton` mit `role=checkbox`, `summaryRow`, `barMobile`, `barDesktop`), eingehängt nach `ChipBar.fs`. "Alle" nutzt `ChipBar.chip` (Aktivzustand = Chip-Farbe wie im Handoff). `Table.calmHeaderContentCell` ergänzt für die Kopfzelle mit Inhalt. Zwei CSS-Klassen am Dateiende von `styles.css` (`.selection-bar` Schatten, `.row-longpress` gegen Textmarkierung und System-Menü), nicht neben `.input-field`. Der Baustein heißt `MultiSelect`, weil `Selection` im Ledger-Modul der Typ der Auswahl ist und der Name sonst mehrdeutig wäre.
- **Farbe der Summe**: der Handoff zeigt die Summe in Rot (`#ff3b5c`). Nach ADR 0049 sind Beträge in ruhigen Listen neutral (nur ein Eingang Teal), deshalb folgt die Summe in Zeile und Leiste derselben Regel wie `Money.listAmount`.
- **Spaltenraster der PC-Tabelle**: die Kästchen-Spalte (44 px) kostet Breite. Damit Memo mindestens 120 px (PC 1440) bzw. 16 % (Alle Konten) behält, geben Payee und Kategorie Prozent ab: einzelnes Konto 30/26 auf 28/24, Alle Konten 22/22 auf 20/20. Die zugehörigen Tests (`LedgerViewTests.fs`: Spaltenzahl 7/8, Prozentwerte; `scripts/ledger-status-column.test.mjs`: Kopf mit leerer Kästchen-Spalte, 44 px, Prozentwerte) sind angepasst.
- **Nicht gebaut**: ein Galerie-Beispiel für `MultiSelect` in `/styleguide` (nicht Teil der Kriterien). Tastaturkürzel, Detail-Zugang aus dem Modus (`ledger-q1hw2`) und schreibende Aktionen sind wie in der Task ausgenommen. Auf dem PC steht ein Toast (unten rechts, z-50) über der Leiste, solange er sichtbar ist.

Tests: 35 neue Expecto-Tests in `src/Tests/LedgerSelectionTests.fs` (selectionTotals je Fall, Reducer für langes Drücken, Tippen, Alle, Nachladen unter AllExcept, Shift-Spanne, Anker, Kopfzeilen-Kästchen, Wechsel leert, Zeile verlässt die Liste für Picked und AllExcept, Ansichtsfunktionen und Quelltext-Zusicherungen). Gegenprobe: ohne `removeFromSelection` in `applyReviewMarkResult` werden die beiden Verlassen-Tests rot. Zwei Playwright-Wächter in `scripts/ledger-selection.test.mjs` (Handy mit Touch-Zeigerereignissen, PC mit Shift-Klick, gemessene Teal-Kante am Anker, 17 px Kästchen, Leiste ohne Geprüft/Kategorie ändern/Flag, Tab-Bar weg und zurück).

Iteration 2: kein Code geändert, nur die README-Abstimmung (siehe "README von Hand"). Die Zahlen unten stammen aus Iteration 1.

Läufe im Worktree: `dotnet build` 0 Warnungen, 0 Fehler; `dotnet test` 1448 bestanden, 7 übersprungen, 0 Fehler (Basis 1413); `npm run build` grün; `npm run guard:classes` "Keine undefinierten Klassen gefunden"; `npm run test:scripts` 136 Tests, nach Anpassung von `ledger-status-column.test.mjs` alle grün (die Datei einzeln: 9 von 9, die neue Datei: 2 von 2). Die Prüfung [human-eye] der Screens 3.1 bis 3.7 steht aus; Bilder liegen im Scratchpad unter `.../scratchpad/ledger-r1d2f/`.

### README von Hand

Das Gate (`npm run check:readme-delta`) lehnt den Prosa-Absatz ab (Prüfung 6, Anker nicht erreichbar, Exit 1). Deshalb von Hand im Abschnitt "Oberfläche (ledger-xbrht, 2026-09-12)", Absatz "Einstieg und Navigation", einen Satz ersetzen.

Alter Satz (Zeilen 416 bis 417 des Ledger-READMEs):
"Konto ist fachlich ein **Filter**, nicht ein eigener Modus: der Wechsel zwischen Gesamtsicht und Konto lässt alle anderen Filter und eine laufende Auswahl stehen."

Neuer Satz:
"Konto ist fachlich ein **Filter**, nicht ein eigener Modus: der Wechsel zwischen Gesamtsicht und Konto lässt alle anderen Filter stehen, eine laufende Auswahl leert er, weil sie sich auf einen Treffer bezieht."

Der Rest des Absatzes (Kontostände, Filterleiste, Liste, erster Chip) bleibt unverändert.
