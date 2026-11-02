---
id: budgeting-h3k9d
title: "Budget-Seite dichter: Kopf in einer Zeile, volle Breite, einzeilige Zeilen, Tastatur seitenweit, Filter Überzogen, Kategorie-Pfeile im Sheet"
status: done
type: feature
context: budgeting
created: 2026-11-02
completed:
depends_on: [budgeting-p4n6r]
blocks: [budgeting-c5w2n]
tags: [budget, inspektor, dichte, tastatur]
related_adrs: [0005, 0049, 0074, 0075]
related_research: []
prior_art: [budgeting-g8s3w, budgeting-p4n6r]
---

## Why
Romans erster Blick auf die neue Seite: Es geht viel Platz verloren, die erste Zeile beginnt erst bei etwa 44 % der Bildhöhe und es passen nur rund 8 Kategorien in den Blick. Nach dem Filtern geht die Tastatur nicht, "Rot" ist kein Fachbegriff, und `‹ ›` im Sheet sehen aus wie Monatsnavigation.

## What

### Dichte
1. **Kopf am PC eine sticky Zeile:** `‹ Monat ›`, Suche, Filter, Zu verteilen, `Targets anwenden`. Der Titel "Budget" entfällt, die Navigation nennt die Seite schon (`PageHeader.calm` fällt auf dieser Seite weg, die Seite braucht trotzdem eine `h1`, sichtbar nur für Screenreader). Ab 1024 px in einer Zeile, darunter bis 768 px zwei Zeilen. Kein seitliches Scrollen. Zu verteilen bleibt `data-testid="budget-to-assign"` wie bisher, falls so benannt, sonst unverändert.
2. **Am Handy:** Sticky bleiben Monat mit Zu verteilen und die Chips wie bisher. Die Suche scrollt mit, `Targets anwenden` steht in ihrer Zeile. Im vergangenen Monat bleibt der Platz des Knopfs frei, die Seite springt nicht.
3. **Volle Breite am PC:** Die Budget-Seite ignoriert die Maximalbreite des Seiten-Containers, mit dem üblichen Seitenrand. Der Zuwachs geht an die Spalte Kategorie, das Panel bleibt 340 px.
4. **Zeilen einzeilig am PC:** Zeilenhöhe höchstens 40 px, Target mit Rhythmus in einer Zeile. Die Trefferfläche der ganzen Zeile bleibt. Gruppenkopf höchstens 32 px.
5. **"erfüllt" entfällt in der Liste,** PC und Handy. `fehlt` und `pausiert` bleiben. Im Tab Target bleibt "erfüllt". Am Handy erscheint die dritte Zeile nur noch, wenn sie etwas sagt: Target ohne Status zeigt nur das Target, wie bisher, die Kürzungsregel bleibt.

### Tastatur
6. **Hoch und Runter gelten seitenweit,** ab 1024 px: nach einem Klick auf einen Filter, im Suchfeld, im Panel. Ein Handler an der Budget-Seite statt am Listen-Container, mit `preventDefault`. Gewechselt wird zwischen den sichtbaren Zeilen (`stepCategory` wie bisher). Ohne Auswahl wählt Runter die erste sichtbare Zeile, Hoch die letzte. Offene Dialoge (Verschieben, Targets anwenden, Picker) bekommen die Tasten nicht.
7. **Links und Rechts wechseln die Tabs** des Inspektors seitenweit, ab 1024 px. Der Fokus in Texteingaben behält seine Cursorfunktion: Ist ein `input` oder `textarea` fokussiert, wirken Links und Rechts dort. Das gilt für das Suchfeld, Hoch und Runter gelten dort trotzdem. Ohne Auswahl tun Links und Rechts nichts. Die Tasten aus `Tabs` (budgeting-p4n6r) doppeln das nicht: ein Druck wechselt genau einen Tab.
8. Spätere Eingabefelder (Zugewiesen in budgeting-c5w2n) sperren beide Richtungen selbst. Die Regel als reine Funktion anlegen, etwa `pageKeyAction (key) (target: TextField | SearchField | Other)`, damit Task 2 sie nur erweitert.

### Sprache
9. **Filter "Rot" heißt "Überzogen":** Chip, Typ `RedOnly` wird `OverdrawnOnly`, Texte, Wächter. Die Leiste in `docs/ux/budget/konzept.md` heißt "Überzogene decken" statt "rot decken". Die Farbe heißt weiter Rot.
10. Die Zahlen an den Filtern bleiben die des ganzen Monats, unabhängig von der Suche (Roman bestätigt). Ein Kommentar an der Stelle nennt die Regel, damit sie nicht geändert wird. Überzogen bleibt eine Teilmenge von Fehlt.

### Sheet
11. **Kategorie-Pfeile:** `˄ ˅` oben rechts neben dem Schließen-Knopf, je 48 px Trefferfläche, Titel und Unterzeile links. Der Monat ist nur noch Text. Vorlage: Kopf der Varianten auf der Zeichenfläche, Seite "Geld bewegen". Die `‹ ›`-Zeile im Sheet entfällt, das spart eine Zeile. Am Rand fehlt der Pfeil, sein Platz bleibt frei. Die `aria-label` bleiben "Vorherige Kategorie" und "Nächste Kategorie" oder werden so gesetzt.

## Acceptance criteria
Tests zuerst: jeden Punkt erst rot sehen, dann bauen. Ausnahme nur rein optische Maße, die erst mit dem Layout messbar sind, dort Gegenprobe per Mutante. Im Outcome ehrlich nennen, was rot vor grün entstand.
- [ ] (Client, rein) `pageKeyAction`: Hoch und Runter im Suchfeld wechseln, Links und Rechts im Suchfeld nichts, im übrigen Fokus Tabs, ohne Auswahl Runter erste und Hoch letzte Zeile, Links und Rechts nichts.
- [ ] (Client-`update`) GIVEN Filter aktiv, keine Auswahl WHEN Runter THEN die erste sichtbare Zeile ist gewählt.
- [ ] (Client-`update`) GIVEN Tab Geld WHEN Rechts, Rechts, Links THEN Buchungen, und `Target` als letzter Tab bleibt beim weiteren Rechts stehen.
- [ ] (Browser) Bei 1440 × 900 stehen mindestens 16 Kategorien im Blick, die erste Zeile beginnt in den oberen 25 % der Höhe, eine Zeile misst höchstens 40 px.
- [ ] (Browser) Kein seitliches Scrollen bei 1024, 1280, 1440, 1920, 390 und 320 px, die Tabelle läuft nicht aus ihrer Karte.
- [ ] (Browser) Bei 1920 px ist die Liste breiter als bei 1280 px, das Panel bleibt 340 px.
- [ ] (Browser) Nach Klick auf einen Filter wechselt Pfeil runter die Kategorie. Im Suchfeld ebenso, der Text bleibt, der Cursor bleibt beim Links-Pfeil im Feld. Bei offenem Dialog passiert nichts.
- [ ] (Browser) Pfeil rechts wechselt den Tab, ein Druck genau einen Tab, auch mit Fokus auf einem Tab.
- [ ] (Browser) "erfüllt" fehlt in der Liste, steht im Tab Target.
- [ ] (Browser) Der Filter heißt Überzogen, der Text "Rot" kommt auf der Seite nicht vor.
- [ ] (Browser) Sheet: `˄ ˅` wechseln die Kategorie, je 48 px, im Sheet steht keine `‹ ›`-Zeile.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Die Seite wirkt dicht, aber ruhig. [human-eye]

## Notes
Fixtures und bestehende Wächter im selben Schritt mitziehen, vor allem "Keine Kategorie in dieser Ansicht.", Trefferflächen, Sticky-Wächter und `--budget-month-bar-height`. Den gemessenen Variablen aus budgeting-p4n6r folgen, keine neuen Handwerte. Flackernde Wächter gezielt mit `npm run test:scripts -- <datei>` mehrfach prüfen, nicht das ganze Gate.

README_DELTA: **Budget-Seite** (Kopf, Filter Überzogen, kein "erfüllt"), **Inspektor** (`˄ ˅`, Pfeiltasten). Vor dem Melden prüfen: `npm run check:readme-delta -- --bc budgeting --delta <datei>`.

## Verifier note (iteration 1)
REASONS:
- Regression: Ab 768 px sind Suche und Filter-Chips beim Monatswechsel nicht gedimmt. `wideHead` rendert sie außerhalb von `listArea`, nur `listArea` und `assignFigure` tragen `monthChangeClass`.
- Der README-Eintrag **Monatswechsel** sagt "Liste, Suche, Filter, Zu verteilen und Inspektor des alten Monats gedimmt".
- Testlücke: Der Monatswechsel-Wächter misst die Opazität nur an `budget-list-area`.
- Sonst ohne Befund, `./verify.sh` zeigt `VERIFY: PASS`.

SUGGESTED_FIX: In `wideHead` Suche und Chips während `monthChanging` mit `monthChangeClass` dimmen, Wächter um ihre Opazität bei 1440 px erweitern, vorher rot.

ITERATION_HINT: likely-fixable

Entscheidung des Conductors: Links und Rechts bleiben auch bei Fokus auf einem Tab am Rand stehen, `Tabs` läuft nicht mehr im Kreis.

## Verifier note (iteration 2)
Inhalt ohne Befund, alle Fragen bestätigt. FAIL nur, weil das Gate auf 372d34c rot war: `getAllRules (/rules) übersteht einen Abbruch` (`scripts/ledger-read-retry.test.mjs:223`), 5-s-Timer, einzeln dreimal grün, Task-Diff berührt diesen Code nicht. Zweites Auftreten, siehe infrastructure-h4q8v.

Der Conductor hat `./verify.sh` auf demselben Stand einmal wiederholt: `VERIFY: PASS` (dotnet 3339, node 464), bei laufendem Dev-Server. Das belegt keine Behebung des Flackerns.

## Outcome

Alle elf Punkte sind umgesetzt. `./verify.sh` zeigt `VERIFY: PASS` (dotnet 3339 grün, 10 opt-in übersprungen, node 464 grün). `npm run check:readme-delta -- --bc budgeting` Exit 0. Offen bleibt nur `[human-eye]`: Die Seite wirkt dicht, aber ruhig. Das entscheidet Roman.

**Wichtige Stellen:**
- Tasten: `KeyFocus`, `PageKeyAction`, `pageKeyAction`, `pageKeyActionFor`, `applyPageKey` in `Components/Budget/Types.fs`, `PageKeys` und `keyFocus` in `Budget/View.fs`.
- Kopf: `wideHead` ab 768 px, `phoneHead` und `phoneSearch` darunter, zur Renderzeit genau einer (ADR 0043). `--budget-filter-bar-height` und `--budget-filter-bottom` sind weg, es bleibt die gemessene `--budget-month-bar-height`.
- Dichte: `ListRoles.denseRowHeight`, `targetCell` einzeilig, `listStatusText` statt `targetStatusText` in der Liste, `PageHeader.screenReaderTitle`.
- Sheet: `BottomSheet.inspector` nimmt `headerActions`, `Button.sheetArrow`, `Button.sheetArrowGap`. Die Unterzeile im Sheet ist jetzt ein `p`, damit greift die vorhandene Regel `.sheet-header p`. Das gilt für alle Sheets mit Unterzeile, auch Verschieben.
- Styleguide, `/styleguide` und `docs/ux/budget/konzept.md` sind nachgezogen.

**Rot vor Grün, ehrlich:**
- .NET: Mit Stummeln liefen 7 von 11 neuen Tests rot. Die übrigen 4 prüfen `LeaveKey` oder "nichts wählen" und waren mit dem Stummel zwangsläufig grün. Sie sind über Mutanten belegt, soweit unten genannt.
- Browser: Vor dem Bau waren 15 Tests rot, davon 12 neue und 3 mitgezogene (dritte Zeile, Target-Spalte, Sticky am PC).
- Nicht rot vor grün: Die Chip-Beschriftung Überzogen habe ich zusammen mit dem Typ-Umbau geändert, weil der Client sonst nicht baute. "Sprache" sah deshalb nie Rot vor dem Bau, belegt ist es per Mutante. "Kein seitliches Scrollen" und "Unter 1024 px" waren vor dem Bau grün, weil sie Verbotenes ausschließen. Auch sie sind per Mutante belegt.
- Vier neue Wächter hatten zuerst eigene Fehler (Karte statt Tabelle gemessen, Übergang von `visibility`, Fehler-Toast über den Pfeilen, Seiteneintritt). Das waren Testfehler, keine Produktfehler. Echte Fehler fanden sie zwei: Zeilen maßen 41 px, und `Tabs` nahm Alt und Pfeil dem Browser weg.

**Gegenprobe aus Iteration 1, je Mutante einzeln rot gesehen und zurückgenommen.** Diese Läufe waren vor der Gate-Korrektur von Iteration 1 (`headBar` ohne dynamische Klassen, Seite in `Container.full`, Reihenfolge in `pageShell`). Gegen den Endstand wiederholt sind nur die vier aus dem Abschnitt Iteration 2.
| Mutante | Rot in |
|---|---|
| Zeile `h-11` | "PC: 1440 × 900": 15 statt 16 Kategorien |
| Status als Block | "Zeilen mit Target": höher als 40 px |
| ohne `basis-20` am Namen | "Zeilen mit Target". Überlebte zuerst, Fixture mit längerem Namen ergänzt, dann rot |
| Budget nicht in `pageContentFull` | "Volle Breite" |
| Suche `min-w-96` | "Kopf": Zu verteilen rutscht aus der Zeile |
| Zu verteilen und Targets anwenden vertauscht | "Kopf": Reihenfolge |
| kein `addEventListener` | "Tastatur seitenweit" |
| `keyFocus` immer `OtherFocus` | "Tastatur im Suchfeld": Cursor |
| Dialoge nicht geprüft | "bei offenem Dialog". Überlebte zuerst: Hin und Zurück hoben sich auf, und der Fokus stand im Feld. Jetzt nach jeder Taste geprüft und mit Fokus auf einem Knopf, dann rot |
| `defaultPrevented` entfernt | "genau einen Tab" |
| Zusatztasten entfernt | "genau einen Tab": Alt |
| `PageKeys` auch unter 1024 px | "Unter 1024 px" |
| `listStatusText` liefert erfüllt | "erfüllt fehlt" |
| Chip wieder Rot | "Sprache" |
| Pfeil im Sheet 36 px | "Sheet: ˄ ˅" |
| `sheetArrowGap` versteckt | "Sheet: ˄ ˅". Überlebte zuerst, weil ich den rechten Pfeil maß. Jetzt die Titelbreite, dann rot |
| Sheet ohne Kürzen | "ein langer Name" |
| Knopf `hidden` statt `invisible` | "Handy: Targets anwenden": die Seite springt |
| Knopf `w-96` | "Kein seitliches Scrollen": 320 px |
| Spaltenkopf unter der Navbar | "PC: nach dem Scrollen" |
| .NET: Textfeld-Regel weg, Tabs im Suchfeld, Tabs laufen im Kreis, Zeilen ohne Filter | je rot in "Pfeiltasten am PC" |

**Iteration 2 (Verifier-FAIL, behoben):**
- Suche und Chips stehen am PC außerhalb der Liste und dimmten beim Monatswechsel nicht mit. In `wideHead` trägt jetzt eine gemeinsame Gruppe `monthChangeClass`, mit `min-w-fit` bricht sie als Ganzes um. Sie bleiben bedienbar.
- `Tabs` läuft nicht mehr im Kreis, Links und Rechts bleiben am Rand stehen (Entscheidung des Conductors). Das gilt für den Baustein, der Styleguide nennt es. Home und End bleiben.
- Rot vor Grün: Beide Wächter waren vor dem Fix rot. "Blättern am PC" misst die Opazität von Suche und Chips samt Vorfahren, "Barrierefreiheit" die Ränder der Tabs.

| Mutante gegen den Endstand | Rot in |
|---|---|
| Budget nicht in `pageContentFull` | "Volle Breite" |
| kein `addEventListener` | "Tastatur seitenweit" (Timeout) |
| Gruppe im Kopf ohne `monthChangeClass` | "Blättern am PC" (Timeout auf die Opazität) |
| `Tabs` rechts wieder im Kreis | "Barrierefreiheit" (Timeout auf Target) |

`budget-page.test.mjs` danach zweimal einzeln grün, `./verify.sh` einmal `VERIFY: PASS`.

**Über den Wortlaut hinaus:**
- Der Handler hängt am Dokument, nicht am Seiten-`div`. Nach einem Klick auf eine Tabellenzeile steht der Fokus auf `body`, ein Handler an der Seite bekäme die Taste nicht.
- Der Strukturtest in `LedgerKeyboardTests.fs` verlangte, dass nur das Kassenbuch am Dokument lauscht. Er nennt jetzt beide Seiten. Sie stehen nie zugleich im DOM.
- Mit Alt, Strg, Cmd oder Umschalt bleibt die Taste beim Browser oder Feld, auf der Seite wie in `Tabs`. In `Tabs` ist das eine kleine Änderung außerhalb des Wortlauts.
- Der Name bekommt `basis-20`: er kürzt zuerst, `fehlt` bleibt neben ihm. Vorher rückte `fehlt` bei jedem langen Namen in eine zweite Zeile.

**Bekannte Grenzen:**
- Bei 1024 px bleiben neben fünf Spalten und Panel unter 80 px für den Namen neben `fehlt`. Dort rückt `fehlt` in eine zweite Zeile, die Zeile wird höher als 40 px. Gemessen ist die Grenze von 40 px nur bei 1440 px.
- Die Abstände am Handy (Suche, Chips) sind wie bisher.
