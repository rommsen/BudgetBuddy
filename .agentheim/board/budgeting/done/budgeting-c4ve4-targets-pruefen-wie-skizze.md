---
id: budgeting-c4ve4
title: "Seite Targets prüfen an die freigegebene Skizze angleichen: kompakte Zeile mit Mini-Balken und Target-Linie, Gruppenfarben und Regelsatz, Kopf mit Erklärung und Zählern, Panel mit Satz und großen Balken"
status: done
type: feature
context: budgeting
created: 2026-11-06
completed:
depends_on: [budgeting-q4v8n]
blocks: [budgeting-m3x7f]
tags: [budget, target, pruefen, client, skizze]
related_adrs: [0049, 0075, 0082]
related_research: []
prior_art: [budgeting-q4v8n]
---

## Why
budgeting-q4v8n hat die Seite `#/budget/target-check` gebaut, aber nicht nach der Skizze, die Roman am 2026-11-05 freigegeben hat (https://claude.ai/artifact/Twwr9mKz6vFU9YK2waFNn3). Die Verfeinerung hatte die Skizze in DS-Bausteine übersetzt und ihre Form verloren, der Worker sah sie nie. Roman am 2026-11-06: "wieso sieht die seite überhaupt nicht aus wie das artifact? das war doch die grundlage". Ab jetzt gilt die Skizze (CLAUDE.md, Ablauf).

## What
Die Skizze ist die Vorlage für Form und Inhalt. Verhalten, Sperre, Herkunft und Antworten aus budgeting-q4v8n bleiben (`TargetCheck.fs`, `TargetCall`, `TargetOrigin`), geändert wird, was man sieht. Die Skizze besteht aus zwei Ansichten: `Main.dc.html` (PC, 1280 px, mit Panel) und `Handy.dc.html` (390 px, mit Bottom Sheet). Der Conductor gibt dem Worker die beiden Dateien und Screenshots (Skizze und Stand nach q4v8n) als Pfade mit. Zahlen und Namen in der Skizze sind erfunden. Bedienung, die nur die Skizze hat ("Neu starten", Daten im Skript), entfällt.

### Seitenkopf (Skizze: Karte oben)
- Eine Karte: links "← Budget" (Zurück wie heute) und "Targets prüfen", darunter klein: "November 2026. Verglichen mit dem Ø der vollen Monate aus dem Kassenbuch, höchstens 12, ab der letzten Änderung des Targets." (Monat aus `TargetCheckData.Month`).
- Rechts zwei Zähler, Beschriftung klein in Mono-Versalien: "Hinweise" (Zahl der Zeilen außerhalb von "Passt", orange) und "Passt" (Zahl der Zeilen in "Passt", teal). Die Zahlen als reine Funktion aus `targetCheckGroups`.
- Am Handy wie `Handy.dc.html`: "← Budget" oben, darunter der Titel groß, darunter "November 2026 · 7 Hinweise · 2 passen" (Hinweise orange, passen teal).

### Gruppen
- Jede Gruppe ist eine Karte mit Kopf: Titel in der Farbe der Gruppe, daneben die Zahl gedämpft in Mono, darunter der Regelsatz klein, rechts ein Chevron. Jede Gruppe klappt auf und zu (`Button.disclosureListRow`, `aria-expanded`), beim Betreten offen, "Passt" zu. Leere Gruppen fehlen, "Passt" steht immer.
- Farben (Nachtrag ADR 0075 vom 2026-11-06): "Gibst du mehr aus?" Orange (`Colors.neonOrange`), "Gibst du weniger aus?" ein ruhiges Blau (neues Token, die Skizze nutzt `#7ab8ff`; in `Tokens.fs`, `styles.css` und der Farbtabelle in Styleguide §2 eintragen, Kontrast auf `bg-card` mindestens 4,5:1 nachrechnen), "Gibst du nie aus?" Violett (`Colors.neonPurple`), "Kein Target" Textfarbe, "Passt" Teal (`Colors.neonTeal`).
- Regelsätze, an ADR 0082 Punkt 8 angeglichen (die Skizze verkürzt):
  - mehr: "Ø mehr als 10 % und 10 € über dem Target, bei monatlichem oder wöchentlichem Rhythmus in mindestens der Hälfte der Monate."
  - weniger: "Ø mehr als 30 % und 10 € unter dem Target."
  - nie: "Höchstens ein Monat mit Ausgaben, bei längerem Rhythmus keiner. Für eine Rücklage ist das richtig."
  - Kein Target: "Regelmäßige Ausgaben, aber kein Target."
  - Passt: "Keine Auffälligkeit. Übernommene und neu gesetzte Targets stehen hier."

### Zeile am PC (Skizze: `.row`, ein Raster aus vier Spalten, rund 56 px hoch)
- Spalte 1: Name, darunter klein die Target-Zeile: "Target 100 € monatlich", bei anderem Rhythmus "Target 50 € wöchentlich (217 € im Monat)", "Target 500 € jährlich (42 € im Monat)", ohne Target "Kein Target". Bei "Passt" mit Zusatz darunter der Zusatz in Teal ("neu gesetzt am 06.11.2026", "entfernt am …", "Sparziel, wird nicht geprüft", "wird nicht geprüft"). Die Kategoriegruppe steht nicht mehr in der Zeile.
- Spalte 2: Mini-Balken (neue DS-Variante, siehe unten), 12 Balken der gezählten Monate, 32 px hoch.
- Spalte 3: "Ø 175 € im Monat" in Mono, darunter klein "11 von 12 Monaten über dem Target", ohne Target "6 von 12 Monaten mit Ausgaben".
- Spalte 4, rechtsbündig: "<Betrag> übernehmen" und "Anpassen …". Beschriftung wie Skizze: "175 € übernehmen" (monatlich ohne Rhythmuswort), "35 € wöchentlich übernehmen", "1.260 € jährlich übernehmen", bei "Kein Target" "20 € monatlich anlegen". Beide Knöpfe neutral (`Button.secondary`, ADR 0075 Punkt 3 gilt weiter).
- Die Zeile mit offenem Panel ist hinterlegt (Skizze: `rowStyle`, Hover-Fläche aus den Tokens).
- Beträge in der Zeile in ganzen Euro (kaufmännisch über `roundHalfUp`), Cent nur im Formular.

### Zeile am Handy (Skizze: `Handy.dc.html`)
- Erste Zeile: Name links, "Ø 175 €" rechts in Mono. Zweite Zeile klein: Target-Text links ("Target 100 € monatlich"), "11 von 12 Mon. darüber" bzw. "6 von 12 Mon. mit Ausgaben" rechts. Darunter die Mini-Balken über die volle Breite, 28 px hoch. Darunter der Zusatz (teal), darunter die beiden Knöpfe nebeneinander, je halbe Breite, 44 px hoch mindestens (Styleguide §4: 48 px am Handy, das gilt).

### Mini-Balken (neue DS-Variante, z. B. `Stats.sparkBars`)
- 12 Balken ohne Beschriftung (`aria-hidden` an den Balken, die Reihe als ein `role "img"` mit zusammengefasstem `aria-label`, etwa "Ausgaben der letzten 12 Monate, 11 über dem Target"). Kein Tab-Stopp.
- Höhe je Balken relativ zum größten Wert aus Monatsausgaben und Monatswert des Targets, mindestens 2 px. Breite am PC 8 px mit 3 px Abstand, am Handy gleich verteilt.
- Farben: Monat über dem Monatswert (ADR 0082 Punkt 6: Netto-Ausgaben über dem Monatswert; die 5 % Spielraum der Skizze gelten nicht, damit Balken und "x von 12 Monaten über dem Target" übereinstimmen) orange, übrige gedämpft (Token für gedämpfte Fläche aus dem Styleguide), Monate vor der letzten Änderung (nicht gezählt) dunkler.
- Eine gestrichelte Linie auf Höhe des Monatswerts, wenn es ein Target gibt.
- Die Rechnung (Höhen, Farbe je Balken, Linienhöhe) als reine Funktion in Shared oder im DS, unter .NET getestet. Eintrag im Styleguide §2 Monatsbalken und auf `/styleguide`. `Stats.monthBarsCompact` aus q4v8n entfällt, wenn nichts anderes sie nutzt (mit Styleguide-Eintrag und Tests).

### Panel "Anpassen …" (Skizze: rechte Karte am PC, Bottom Sheet am Handy)
- Kopf: Name der Kategorie, rechts "Schließen" (schließt wie Abbrechen).
- Ein Satz je Gruppe (reine Funktion):
  - mehr: "Du gibst im Ø 175 € im Monat aus, das Target sieht 100 € vor. 11 von 12 Monaten lagen darüber."
  - weniger: "Du gibst im Ø 132 € im Monat aus, das Target sieht 217 € vor."
  - nie: "In 12 Monaten gab es hier keine Ausgabe. Für eine Rücklage ist das richtig, dann lässt du es so."
  - Kein Target: "Du gibst im Ø 17 € im Monat aus, ohne Target."
  - Passt: "Das Target passt zu den Ausgaben."
- Am PC darunter große Balken (72 px, Monatskürzel darunter in Mono klein) mit gestrichelter Target-Linie und der Legende "Balken: Ausgaben je Monat · orange: über dem Target · Linie: Target". Am Handy ohne große Balken (wie Skizze).
- Darunter das gemeinsame Target-Formular aus dem Tab Target in seiner Reihenfolge (Abweichung, siehe unten), vorbelegt wie heute. Unter dem Betragsfeld der Hinweis "Vorschlag aus dem Ø, auf volle 5 € gerundet: 175 € monatlich" (bei wöchentlich: "… auf volle Euro gerundet: 35 € wöchentlich", ADR 0082 Punkt 9). Ohne Vorschlag kein Hinweis.

### Rückmeldung nach dem Speichern
- Nach "übernehmen" und nach Speichern im Panel ein Erfolgs-Toast: "Essen auswärts: Target jetzt 175 € monatlich." Nach Entfernen: "Essen auswärts: Target entfernt." Die Zeile steht wie heute bei "Passt" mit "neu gesetzt am …" bzw. "entfernt am …".

### Abweichungen von der Skizze (von Roman am 2026-11-06 entschieden)
- "übernehmen" ist neutral statt teal gefüllt (ADR 0075 Punkt 3, sonst Dutzende Hauptaktionen).
- Das Panel nutzt das gemeinsame Target-Formular in dessen Reihenfolge (Rhythmus, Target, Fällig am, Speichern, Entfernen, Abbrechen), damit Tab und Seite ein Formular behalten. Satz, große Balken und Vorschlagshinweis kommen dazu.
- Die Zeile unter "Passt" zeigt "neu gesetzt am …" statt "Übernommen: 175 € monatlich", weil das auch nach dem Neuladen stimmt. Der Satz der Skizze steht im Toast.
- Die Regelsätze folgen ADR 0082 statt der verkürzten Sätze der Skizze; am Handy dieselben Sätze wie am PC.
- "Neu starten" entfällt (nur Bedienung der Skizze), der Hinweis "In YNAB gespeichert" im Toast entfällt (BB hält Targets selbst, ADR 0084).

## Acceptance criteria
Reine Funktionen und Client-`update` unter .NET:
- [ ] Zähler Hinweise und Passt aus `targetCheckGroups`, getestet mit allen fünf Gruppen.
- [ ] Texte der Zeile (Target-Text je Rhythmus mit Monatswert in Klammern, "Ø … im Monat", "x von N Monaten über dem Target" bzw. "mit Ausgaben", Handy-Kurzform "Mon. darüber") und der Knöpfe ("175 € übernehmen", "35 € wöchentlich übernehmen", "20 € monatlich anlegen") in ganzen Euro, je Fall ein Test.
- [ ] Regelsatz und Farbe je Gruppe aus einer reinen Funktion, getestet je Gruppe.
- [ ] Mini-Balken: Höhen, Farbe je Balken (über dem Monatswert, darunter, nicht gezählt) und Linienhöhe aus einer reinen Funktion, getestet mit Grenzen (genau auf dem Monatswert, ohne Target, alle 0).
- [ ] Satz im Panel je Gruppe aus einer reinen Funktion, getestet je Gruppe.
- [ ] Jede Gruppe klappt auf und zu, beim Betreten offen, "Passt" zu; ein neues Betreten setzt das zurück.
- [ ] Erfolgs-Toast nach übernehmen, Speichern und Entfernen mit dem Satz oben, getestet durch das echte `update`; die bestehenden Antwort-Tests aus q4v8n bleiben grün.

Browser-Wächter (`scripts/target-check.test.mjs`, API gemockt):
- [ ] `[layout]` 1280 px: eine Zeile mit Werten ist höchstens 72 px hoch, die vier Spalten stehen nebeneinander, das Panel steht rechts.
- [ ] `[layout]` 375 px: Zeile mit beiden Knöpfen nebeneinander ohne Abschneiden (auch "1.260 € jährlich übernehmen"), Trefferflächen 48 px, Kopf ohne Überlauf.
- [ ] `[layout]` berechnete Farben: Titel "Gibst du mehr aus?" orange, ein Balken über dem Target orange, einer darunter nicht.
- [ ] `[flow:targets-pruefen]` läuft weiter (Texte anpassen).
- [ ] Die `[tastatur]`-Prüfung der Balkenreihe entfällt oder prüft, dass die Mini-Balken keinen Tab-Stopp haben (Tab geht vom Namen zum Knopf).

Sonst:
- [ ] Neues Blau-Token in `Tokens.fs`, `styles.css` und Styleguide §2 mit nachgerechnetem Kontrast; Mini-Balken in Styleguide §2 und auf `/styleguide`.
- [ ] README budgeting: der Eintrag **Targets prüfen** an die neue Form angepasst, als README-Delta, geprüft mit `npm run check:readme-delta -- --bc budgeting`.
- [ ] Seite neben der Skizze verglichen: Screenshots bei 1280 px (mit Panel) und 390 px neben `skizze-pc-panel.png` und `skizze-handy.png`, Abweichungen nur die oben genannten, im Outcome je Ansicht ein Satz. [human-eye]
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Skizze: https://claude.ai/artifact/Twwr9mKz6vFU9YK2waFNn3 (`project/Main.dc.html`, `project/Handy.dc.html`). Worker und Verifier bekommen vom Conductor die Dateien und die Screenshots `skizze-pc.png`, `skizze-pc-panel.png`, `skizze-handy.png`, `skizze-handy-sheet.png` sowie den Stand nach q4v8n (`gebaut-pc-panel.png`, `gebaut-handy.png`) als Pfade im Scratchpad.
- Die Farben der Skizze sind Hexwerte aus einer eigenen Palette. Für jede Farbe zuerst das passende Token aus `Tokens.fs` nehmen (Orange, Teal, Violett, Textfarben, Flächen), nur das Blau ist neu.
- Kann-Zeilen aus q4v8n, die hiermit erledigt sind: Umbruch der Knöpfe am Handy (Knöpfe stehen nebeneinander), Skelett in falscher Form (das Skelett soll Karten mit kompakten Zeilen zeigen, `Stats` hat `monthBarsPlaceholder` als Vorbild). Beide Zeilen aus budgeting-h9c3w beim Integrieren streichen.
- Nach dem Lauf vorschlagen: `design-quality` (mit den Skizzen-Screenshots), `frontend-architecture`, `qa-milestone-reviewer`.

## Verifier note (iteration 1)
REASONS:
- Die Mini-Balken folgen der Skizze nicht: die Skizze zeichnet jeden Zeilenbalken mit `border-radius:2px` (Main.dc.html `width:8px;border-radius:2px`, Handy.dc.html `flex:1;border-radius:2px`). `Stats.sparkBar` gibt der Zeilenvariante `rounded-sm`, und `--radius-sm` ist hier 8 px (`src/Client/styles.css`). Am PC ist ein 8-px-Balken oben ganz rund, am Handy werden kurze Balken zu Ovalen, hohe zu Pillen (seite-handy.png neben skizze-handy.png). Die großen Panel-Balken (`rounded-[3px]`) passen.
- Am Handy ist der Abstand 3 px (`gap-[3px]`), die Skizze nutzt 4 px. Die Task deckt die Abweichung nicht.
- Hinweis: Im Sheet steht das X des BottomSheet statt "Schließen"; `BottomSheet.inspector` hat keinen Text-Schließknopf. Entscheidet der Owner, bis dahin Kann-Zeile.
SUGGESTED_FIX: Zeilenvariante von `Stats.sparkBar` auf 2 px Radius wie die Skizze (etwa `rounded-[2px]`), am Handy 4 px Abstand; Styleguide §2 Mini-Balken nachziehen, Screenshots neu, `./verify.sh`.
ITERATION_HINT: likely-fixable

## Outcome

Die Seite `#/budget/target-check` ist nach der Skizze umgebaut. Verhalten, Sperre, Herkunft und Antworten aus budgeting-q4v8n bleiben. Geändert ist, was man sieht, dazu kommen der Erfolgs-Toast und das Auf- und Zuklappen jeder Gruppe.

- Rein und unter .NET getestet: `checkBars` und `wholeEuroText` in `src/Shared/Budgeting.fs`. `TargetCheckRow` hat jetzt `Months`, die 12 vollen Monate, auch die vor der letzten Änderung. In `src/Client/Components/Budget/TargetCheck.fs` stehen `checkCounts`, `checkCountsText`, `checkIntroText`, `checkGroupRule`, `checkRowText` (`Wide`/`Narrow`), `checkBarsLabel`, `checkSparkBars`, `checkPanelSentence`, `checkSuggestionHint`, `takeText` (ganze Euro), `targetWrittenText`, `checkCategoryName` und `targetAnswerToast` mit Erfolgs-Toast. Neu sind außerdem `OpenGroups` und `ToggleCheckGroup` (ersetzen `FitsOpen` und `ToggleFits`). `checkGroupTitleClass` steht in `TargetCheckView.fs`, weil der Klassen-Wächter Klassen nur in derselben Datei auflöst.
- View `src/Client/Components/Budget/TargetCheckView.fs`: Ab 720 px Listenbreite hat die Zeile vier Spalten wie die Skizze, darunter den Handy-Aufbau. Das läuft über eine Tailwind-Container-Abfrage auf der Liste, damit eine schmale Liste neben dem Panel (1024 px) nicht überläuft. Das Panel ist 360 px breit (`.target-check-panel` in `styles.css`, so hoch wie der Inhalt). Das Skelett zeigt jetzt Karten mit kompakten Zeilen.
- DS `src/Client/DesignSystem/Stats.fs`: `Stats.sparkBars`, `Stats.sparkBarsLabelled` und `Stats.sparkBarsPlaceholder` sind neu. Die Zeilenbalken haben Ecken von 2 px (`rounded-[2px]`; `rounded-sm` ist hier 8 px), Abstand 4 px wie `Handy.dc.html` und ab einem Container von 720 px 3 px wie `Main.dc.html` (`gap-1 @min-[720px]:gap-[3px]`, dieselbe Grenze, an der die Zeile auf vier Spalten wechselt). Die großen Panel-Balken behalten 3 px Ecken. `Stats.monthBarsCompact`, `monthBarTabStop` und `monthBarKeyTarget` sind entfernt, ebenso ihre Tests und der Styleguide-Text. Neues Token `Colors.calmBlue` (`--color-calm-blue: #7ab8ff`), Kontrast 8,93:1 auf `bg-card`, 8,14:1 auf `bg-elevated`, 7,75:1 auf `bg-hover`. Steht in der Farbtabelle in Styleguide §2, Mini-Balken (mit Ecken und Abständen) in §2 Monatsbalken (`standards/frontend/styleguide.md`) und auf `/styleguide`. `TargetFormView` hat ein Feld `AmountNote` für den Vorschlagshinweis.
- Tests: `src/Tests/TargetCheckClientTests.fs` (Liste "Form nach der Skizze" sowie angepasste Toast- und Klapp-Tests). `scripts/target-check.test.mjs`: Flow, `[layout]` 375 px, `[layout]` 1280 px (Zeile höchstens 72 px, vier Spalten, Panel rechts, keine Tab-Stopps in den Balken), `[layout]` Mini-Balken wie die Skizze (berechnete Ecken 2 px, Abstand 3 px am PC und 4 px bei 375 px, Panel-Balken 3 px; vor dem Fix rot mit 8 px Radius), `[layout]` berechnete Farben und `[fable]` "Ø 85 € im Monat". Der `[tastatur]`-Test entfällt, die Mini-Balken haben keinen Tab-Stopp. `./verify.sh`: VERIFY: PASS (dotnet 4193, node 317).

Vergleich mit der Skizze. Screenshots mit Daten wie in der Skizze, API gemockt, nach dem Fix neu aufgenommen (Skript `shots/shoot.mjs`, Bilder `seite-pc-panel.png`, `seite-handy.png` und `seite-handy-sheet.png` im Scratchpad des Laufs):
- PC 1280 px mit Panel (neben `skizze-pc-panel.png`): Diese Teile stimmen mit der Skizze überein: Kopf-Karte mit "← Budget", Titel, Erklärung und den Zählern Hinweise (orange) und Passt (teal), Gruppen-Karten mit farbigem Titel, Zahl in Mono, Regelsatz und Chevron, Zeile in vier Spalten mit Mini-Balken, gestrichelter Linie, Ø und Monaten sowie zwei Knöpfen, die hinterlegte Zeile beim offenen Panel, Panel rechts mit Name, "Schließen", Satz, großen Balken mit Monatskürzeln und Legende. Mini-Balken geprüft: Ecken 2 px, oben kantig wie in der Skizze, Abstand 3 px, Balken etwa 8 px breit in der Spalte von 132 px (Skizze 8 px); die Panel-Balken mit 3 px Ecken. Diese Abweichungen stehen in der Task: "übernehmen" neutral, Formular in eigener Reihenfolge (Speichern orange, Entfernen, Abbrechen), Regelsätze nach ADR 0082, kein "Neu starten". Weitere Abweichungen: Der Chevron kommt aus `Button.disclosureListRow` (dreht sich, statt ▾/▸; die Task verlangt diesen Baustein). Violett ist `neonPurple` (#a855f7) statt #c9a7ff (die Task verlangt das Token). Die Zeilen sind etwa 63 px hoch statt 77, weil die dritte Spalte breiter ist und "11 von 12 Monaten über dem Target" in eine Zeile passt (Kriterium höchstens 72 px). Die Legende bricht in zwei Zeilen um. Es gilt die App-Schrift statt Inter. Die Vorschläge folgen ADR 0082 (Haushaltshilfe 31 € statt 35 € wöchentlich).
- Handy 390 px (neben `skizze-handy.png`): Diese Teile stimmen überein: "← Budget" oben, großer Titel, "November 2026 · 12 Hinweise · 5 passen" in Orange und Teal, Zeile mit Name und "Ø 175 €", darunter Target und "11 von 12 Mon. darüber", Mini-Balken über die volle Breite (28 px) mit Ecken 2 px und Abstand 4 px wie die Skizze (kurze Balken sind jetzt flache Rechtecke, keine Ovale mehr), beide Knöpfe nebeneinander je halbe Breite mit 48 px. Abweichungen: der neutrale Knopf und die Regelsätze wie oben, der DS-Chevron. Die längeren Regelsätze brechen in drei Zeilen um. Navigationsleiste und untere Leiste der App stehen auch hier.
- Sheet am Handy (neben `skizze-handy-sheet.png`): Name, Satz und Formular stimmen überein; Mini-Balken zeigt das Sheet wie die Skizze nicht. Abweichungen: Schließen ist das X des DS-`BottomSheet` statt des Worts "Schließen" (`BottomSheet.inspector` hat keinen Text-Schließknopf, bleibt so bis zur Entscheidung des Owners), Formular in der Reihenfolge des gemeinsamen Target-Formulars (Abweichung aus der Task).

Ergänzt, weil die Skizze den Fall nicht hat: "nie" mit genau einem Ausgabemonat ergibt "In 12 Monaten gab es hier nur in einem Monat eine Ausgabe. …". Der Satz für "Passt" ohne Target lautet "Ohne Target, die Ausgaben sind unauffällig.", denn "Das Target passt" wäre ohne Target falsch. Eine Zeile bei "Passt" mit Zusatz zeigt jetzt auch Balken und Werte, wie in der Skizze.

Kann-Sammlung budgeting (budgeting-h9c3w): Die beiden Zeilen aus q4v8n sind erledigt und können gestrichen werden: Umbruch der Knöpfe am Handy (die Knöpfe stehen nebeneinander) und Skelett in falscher Form (jetzt Karten mit kompakten Zeilen). Neue Kann-Zeilen: (1) Im Sheet steht das X des BottomSheet statt "Schließen" wie in der Skizze, der Owner entscheidet. (2) Der Test "Budget: Übernahme im Hintergrund.Dienst.die Wartefunktion des Servers wartet wirklich mindestens 50 ms" (`src/Tests/BudgetTakeoverTests.fs`) war im ersten Gate-Lauf dieser Iteration einmal rot mit 49,82 ms (Timer-Auflösung), im zweiten Lauf grün; nicht von dieser Task berührt.

Nach dem Lauf vorschlagen: `design-quality` (mit den Skizzen-Screenshots), `frontend-architecture`, `qa-milestone-reviewer`.
