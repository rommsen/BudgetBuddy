---
id: budgeting-h3k9d
title: "Budget-Seite dichter: Kopf in einer Zeile, volle Breite, einzeilige Zeilen, Tastatur seitenweit, Filter Überzogen, Kategorie-Pfeile im Sheet"
status: doing
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
