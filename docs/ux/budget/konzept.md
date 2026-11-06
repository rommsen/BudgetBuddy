# Budget-Seite: Gesamtkonzept

**Stand:** 2026-11-02, mit Roman abgestimmt.
**Prototyp:** `gesamt-prototyp.dc.html` (Verhalten, nicht Optik), lebend auf der Zeichenfläche
https://claude.ai/artifact/JfQDhhTLwdEadmQyJbbzkz (Seite "Archiv" enthält die verworfenen Varianten).
**Prüfskript:** `pruefe-prototyp.mjs` spielt die Abläufe ohne Browser durch:
`node docs/ux/budget/pruefe-prototyp.mjs docs/ux/budget/gesamt-prototyp.dc.html`.
Alle Beträge und Kategorien im Prototyp sind erfunden. Die Optik kommt aus Styleguide und echten
DS-Komponenten, nicht aus dem Prototyp. Ausnahme ist die Leiste Monatserster samt Vorschau Targets anwenden:
für sie ist der Prototyp die Vorlage (Board `Gesamt.dc.html`, Screenshots unter `skizze-monatserster/`,
budgeting-m3x7f), neue Bausteine dafür stehen im DS (`Button.tile`, `Button.quietText`).

## Grundidee

Eine Seite mit zwei Ebenen. Die **Liste** zeigt, wie es insgesamt steht und wo man hin muss. Der
**Inspektor** enthält alles zu einer Kategorie: am PC rechts mit fester Höhe, am Handy als
BottomSheet mit fester Höhe. Eine Zeile anklicken wählt die Kategorie, alle Aktionen liegen im
Inspektor. Pfeiltasten am PC und `˄ ˅` im Sheet wechseln die Kategorie, ohne zu schließen. Links
und Rechts wechseln am PC die Tabs.

| Tab | Inhalt |
|---|---|
| **Geld** | Zugewiesen, "12,30 zuweisen", Target pausieren, Geld holen oder abgeben |
| **Buchungen** | Buchungen des Monats mit Sprung ins Kassenbuch |
| **Historie** | Verfügbar-Aufbau, Bewegungen, Ausgaben der letzten 6 Monate |
| **Target** | Target anlegen, ändern, entfernen, pausieren |

Der Seitenkopf trägt Monat, die Icon-Knöpfe Historie und Monatserster und rechts Zu verteilen, am Handy in einer
Zeile mit kurzem Monatsnamen. Darunter steht die Leiste Monatserster, darunter Suche und Filter in eigener Zeile wie im
Prototyp. Targets anwenden und Targets prüfen stehen nicht im Kopf, beide erreicht man über die Leiste (budgeting-m3x7f).

## Die vier Abläufe

1. **Monatserster:** Eine Leiste über der Liste zeigt im aktuellen Monat vier Felder in dieser
   Reihenfolge: **Targets prüfen, Targets anwenden, Überzogene decken, verteilen**. Prüfen steht
   vorn, weil ein geändertes Target den Fehlbetrag ändert. Überzogene stehen hinter Anwenden, weil
   Targets sie meist schon ausgleichen. Die Leiste ist kein Modus: Targets prüfen führt auf die
   Seite Targets prüfen, Überzogene filtert die Liste, Anwenden öffnet die Vorschau mit abwählbaren
   Zeilen. Verteilen ist vorerst nur eine Anzeige. Sie steht von selbst im laufenden Monat und verschwindet,
   wenn die ersten drei Felder erledigt sind. "Ausblenden" gilt für den Monat auf allen Geräten. Der Kopfknopf
   Monatserster zeigt sie jederzeit (auch in kommenden Monaten), bis die Seite verlassen wird.
2. **Monatsmitte:** Zeile wählen, im Tab Geld handeln, weiter zur nächsten.
3. **Was ist passiert:** Tab Historie je Kategorie, Knopf Historie für alle, filterbar nach Art.
4. **Targets pflegen:** im Tab Target, und gesammelt auf der Seite Targets prüfen.

## Regeln

### Zuweisen

- Das Feld rechnet: `+20`, `-20`, `*2`, `/2` (auch `x`, `:`) relativ zum Stand, `900+40` ersetzt
  ihn. Punkt vor Strich, deutsche Beträge. Eine ungültige Rechnung wird rot gemeldet, nicht gespeichert.
- Das Feld markiert beim Fokus seinen Inhalt.
- Sobald der Wert abweicht, stehen darunter **Verfügbar danach** (fett; rot unter 0, grün bei
  genau 0) und, klein, Ergebnis und Zu verteilen danach.
- Am Handy fehlen Plus und Minus auf der Zifferntastatur, deshalb gibt es die Knöpfe
  **-50, -20, -10, +10, +20, +50**. Sie ändern nur den Entwurf im Feld.
- **Gespeichert wird beim Verlassen:** Enter, Fokus weg, Tab-, Kategorie- oder Monatswechsel,
  Sheet schließen. Esc verwirft. Mehrere Knopfdrücke sind ein Schreibvorgang und ein Historie-Eintrag.
- Zu verteilen darf negativ werden. Rot warnt, nichts sperrt.
- **Fehlbetrag-Knopf:** `fehlt` ist das Größere aus negativem Verfügbar und Fehlbetrag des Targets
  (YNAB `goal_under_funded`). Pausierte Targets zählen nicht. Der orange Knopf heißt
  "12,30 zuweisen". Reicht Zu verteilen nicht: "Nur noch 60,20 zu verteilen", ohne Wirkung.

### Geld holen und abgeben

- Der Tab Geld hat oben einen Umschalter mit drei Modi: **Zuweisen, Holen, Abgeben** (Variante E auf der
  Zeichenfläche, Seite "Geld bewegen"). Das ersetzt einen eigenen Decken-Pfad, das Sheet bleibt gleich hoch.
  Standard ist Zuweisen, rote Kategorien starten auf Holen mit vorbelegtem Betrag. "Target pausieren" steht
  im Modus Zuweisen.
- Aufbau wie Variante D auf der Zeichenfläche (Seite "Geld bewegen"): großer Betrag, darunter `von`
  und `nach` untereinander in voller Breite, je mit dem Stand danach. Der orange Knopf nennt die
  Handlung ("20,00 € aus Kleidung holen", "20,00 € an Kleidung abgeben").
- Die Gegenseite wählt man im vorhandenen `CategoryPicker` (am PC Popover, am Handy `sheetLayered`),
  oben mit "Zuletzt verwendet" aus den Budget-Bewegungen. Im Tab selbst stehen keine Vorschläge,
  die Gegenseite ist nie vorbelegt.
  **Frei** ist Verfügbar minus offener Fehlbetrag, pausierte Targets sperren nichts.
- Das Betragsfeld rechnet wie Zugewiesen.
- Über den freien Betrag hinaus holen geht, die Quelle wird rot, es gibt eine Warnung und keine Sperre.

### Targets

- YNAB führt die Targets weiter, BB bearbeitet sie und zieht YNAB nach. Es gibt keine Art, die
  YNAB nicht kennt. Grenzen der API: Research `ynab-api-targets-zuweisung-2026-10-25`, Abschnitt 5.
- **Pausieren** gilt je Kategorie und Monat. YNAB lässt `goal_snoozed_at` nicht schreiben, deshalb
  liegt die Pause in BB. Sie gilt, wenn sie auf einer der beiden Seiten steht. Eine BB-Pause sieht
  man in YNAB nicht. Sie endet mit dem Monat.
- **"Passt das Target noch?"** beantwortet die eigene Seite **Targets prüfen** (`#/budget/target-check`,
  ADR 0082). Man ruft sie gelegentlich über einen Knopf im Seitenkopf auf, wie Historie. Sie gilt
  immer für den laufenden Monat. An der Kategorie steht kein Hinweis, der Tab Target bleibt `Target`,
  es gibt kein "So lassen": jeder Aufruf rechnet neu aus dem Kassenbuch.
- Die Seite zeigt alle sichtbaren Kategorien in fünf Gruppen: "Gibst du mehr aus?", "Gibst du weniger
  aus?", "Gibst du nie aus?", "Kein Target" und "Passt" (eingeklappt). Verglichen wird das Target als
  Monatswert mit dem Durchschnitt der Ausgaben der vollen Monate, höchstens 12, ab der letzten
  Änderung in BB. Sparziele werden nicht geprüft.
- Eine Zeile zeigt Target als Monatswert, Ø, die Zahl der Monate über dem Target und die Monate als
  Balken. **"50,00 €/Woche übernehmen"** schreibt den Vorschlag (Ø auf 5 € aufgerundet, im Rhythmus des
  Targets) zu YNAB, ohne Target legt es ein monatliches an; danach steht die Zeile bei "Passt" mit
  "neu gesetzt am …". **"Anpassen …"** öffnet das Target-Formular, am PC als Panel, am Handy als
  BottomSheet, vorbelegt mit dem Vorschlag.
- Nur "Targets prüfen" heißt prüfen, es gibt kein allgemeines Prüfen.

### Fehlbetrag-Knopf

Der Knopf verwirft eine offene Eingabe im Feld Zugewiesen und meldet das per Warn-Toast. Nach einer
Bewegung ist er gesperrt, bis der Monat neu gelesen ist, sonst buchte ein zweiter Tipp denselben
Fehlbetrag noch einmal. Liste und Feld bleiben dabei bedienbar.

### Monate

Freies Blättern mit `‹ Monat ›`, der Monatsname führt zum aktuellen Monat zurück und wird teal,
wenn man woanders ist.

| Fähigkeit | Vergangen | Aktuell | Künftig |
|---|---|---|---|
| Ansehen, Buchungen, Historie | ja | ja | ja |
| Zuweisen, Geld holen und abgeben | ja | ja | ja |
| Targets anwenden | nein | ja | ja |
| Target ändern, anlegen, entfernen | nein | ja | nein |
| Target pausieren | nein | ja | ja |

Die Grenzen nach vorn und hinten kommen aus YNAB. Am Rand fehlt der Pfeil. Laut Probe vom
2026-10-26 lieferte YNAB für zwei Monate voraus 404, die Probe `budgeting-q7m2c` klärt das verbindlich.

### Farben

- **Orange:** die eine Hauptaktion. **Teal:** Ausgewähltes und Navigation.
- Normale Knöpfe sind neutral mit grauem Rand, das ist `Button.secondary` (ADR 0077).
- **Rot:** nur Beträge im Minus. Wer über den freien Betrag holt, sieht nur den gelben Hinweis.
  **Gelb** (dezent, Prototyp `#cdb27a`): offen, aber kein Fehler, also `fehlt`. Gelb ist ein neuer Token. Eine Zeile darf Rot und Gelb
  zugleich tragen, das ist eine Ausnahme von "ein Akzent pro Zeile" für die Budget-Seite.
- Auf der Budget-Seite gibt es kein Pink mehr. Das Kassenbuch behält Pink für "zu prüfen".

### Am Handy

- Das Sheet ist 500 px hoch, Kopf und Tabs stehen fest, nur der Inhalt scrollt. Mit offener
  Tastatur folgt es dem Visual-Viewport-Muster (ADR 0005) und wird kleiner.
- Trefferflächen 48 px, Felder 16 px wegen des iOS-Zooms.
- Die Leiste ist ein Raster aus vier gleich breiten Feldern, oben Zahl, unten Bezeichnung.

## Bleibt unverändert

Offene Verschiebung mit "Erneut senden", Vorschau mit Stand-Prüfung und Bericht je Zeile,
Suche über Kategorie und Gruppe, ausgeblendete Kategorien fehlen.

## Backlog, nicht Teil dieses Baus

Mehrfachauswahl mit Summen, Gruppe auffüllen, "Alle auffüllen" im Filter Überzogen, Entwurf,
Verteilregeln (Deckel, Rest nach Gewichten, Auffüllen), Reihenfolge in BB bearbeiten, Undo.
