---
id: budgeting-m3x7f
title: "Monatserster: Leiste nach der Skizze mit Targets prüfen, Targets anwenden, Überzogene decken und verteilen; Kopfknopf Monatserster statt Targets anwenden und Prüfen-Icon; Vorschau mit abwählbaren Zeilen; Ausblenden je Monat auf dem Server"
status: backlog
type: feature
context: budgeting
created: 2026-11-02
completed:
depends_on: [design-system-001, budgeting-t6p8z, budgeting-q4v8n, budgeting-eh8xe, budgeting-hbhzf, budgeting-c4ve4, budgeting-k7r2w]
blocks: []
tags: [budget, monatserster, targets, skizze]
related_adrs: [0065, 0075, 0082, 0083]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
prior_art: [budgeting-h2zfn, budgeting-c4ve4, budgeting-q4v8n]
---

## Why
Task 5 von 5 des Gesamtkonzepts. Der Monatserste ist Romans wichtigster Ablauf. Die Leiste führt durch ihn, ohne ihn in einem Modus festzuhalten. Roman am 2026-11-06: Targets anwenden und Targets prüfen gehören in diese Leiste, und die Leiste soll sich auch zwischendurch öffnen lassen. Er betont ausdrücklich, dass die Skizze die Vorlage ist und diesmal nicht verloren gehen darf (Lehre aus budgeting-q4v8n, CLAUDE.md Ablauf).

## Vorlage: die Skizze
Freigegeben von Roman: der Gesamt-Prototyp, Artifact https://claude.ai/artifact/JfQDhhTLwdEadmQyJbbzkz, Seite „Gesamtkonzept“, Board `Gesamt.dc.html`. Die Boards auf der Seite „Archiv“ (`Monatserster.dc.html` mit „geführt oder Leiste“, `Main.dc.html`) sind verworfen und gelten nicht.

Für Worker und Verifier im Repo, ohne Artifact-Zugang:
- Quelltext: `docs/ux/budget/gesamt-prototyp.dc.html` (am 2026-11-06 gegen das Artifact verglichen, gleich). Leiste Zeile 45 bis 52, Vorschau Zeile 183 bis 196, Felder und Regeln Zeile 309 bis 313 und 361.
- Screenshots (`docs/ux/budget/skizze-monatserster/`):
  - `leiste-pc.png`: 2. November, PC, Leiste offen
  - `leiste-pc-rot.png`: nach Klick auf das dritte Feld, Filter aktiv, erste Zeile im Tab Geld
  - `leiste-pc-vorschau.png`: Vorschau Targets anwenden am PC
  - `leiste-handy.png`: Handy mit Leiste
  - `leiste-handy-vorschau.png`: Vorschau am Handy
  - `mitte-pc.png`: 17. November, alles erledigt, keine Leiste, kein Targets-anwenden-Knopf im Kopf
- Neu rendern: Artifact lesen, `artifact-type/dc-runtime.js` als `project/support.js` daneben legen, „2. November“ und „PC“ oder „Handy“ klicken.

Der Prototyp-Kopf („Prototyp“, Tage, PC/Handy, Neu starten) und die erfundenen Beträge gehören nicht zur Seite. Alles andere an Leiste und Vorschau ist ein Kriterium unten. Abweichungen stehen nur im Abschnitt „Abweichungen von der Skizze“.

## What

### Leiste (Skizze Zeile 45 bis 52)
- Eine Karte zwischen Seitenkopf und Suche/Filtern, über die volle Breite der Liste: Fläche `--bg-card` (#111128), Rand `--border-subtle` (#1c1c38), Radius 12 px, Innenabstand 6/10/10 px, Abstand 6 px zwischen Kopfzeile und Raster. Unten 10 px Abstand zur Suchzeile.
- Kopfzeile: links „Monatserster“ (13 px, 600), rechts der Textknopf „Ausblenden“ (12 px, gedämpfte Textfarbe, Trefferfläche mindestens 36 px hoch, am Handy 48 px nach ADR/Styleguide).
- Raster aus vier gleich breiten Feldern mit 6 px Abstand, an PC und Handy gleich (vier Spalten auch bei 390 px, siehe `leiste-handy.png`). Jedes Feld ein Knopf: mindestens 52 px hoch, Radius 10 px, Rand `--border` (#2a2a4a), zentriert, oben der Wert in Mono 13 px 600, darunter die Bezeichnung 11 px gedämpft. Am Handy bricht die Bezeichnung zweizeilig um („Targets / prüfen“), das Feld wächst mit.
- Zustände eines Felds wie die Skizze: aktiv (Filter oder Vorschau gerade offen) Rand `--color-neon-teal` (#00d4aa); erledigt Rand `--border-subtle` (#1c1c38), Text gedämpft, Wert „✓“; sonst Standard.
- Die vier Felder in dieser Reihenfolge:
  1. **Targets prüfen:** Wert = Zahl der Hinweise der Seite Targets prüfen (alle Zeilen außerhalb von „Passt“, dieselbe reine Funktion wie die Zähler der Kopf-Karte aus budgeting-c4ve4, Daten aus `getTargetCheck`). Erledigt bei 0 Hinweisen (Roman 2026-11-06). Klick öffnet `#/budget/target-check`.
  2. **Targets anwenden:** Wert = Summe, die Targets anwenden zuweisen würde, als Betrag; erledigt bei 0. Die Summe muss mit der Summe der Vorschau übereinstimmen (eine Quelle, Test). Klick öffnet die Vorschau.
  3. **Überzogene decken:** Wert = Zahl der überzogenen Kategorien; erledigt bei 0. Klick setzt den Filter „Überzogen“ und wählt am PC die erste Zeile mit Tab Geld (`leiste-pc-rot.png`), am Handy nur den Filter.
  4. **verteilen:** Wert = Zu verteilen, solange positiv; sonst „✓“. Nur Anzeige: Klick setzt den Filter „Alle“. Zählt nicht für das Ende der Leiste.

### Wann die Leiste steht
- Von selbst im laufenden Monat, solange eines der Felder 1 bis 3 offen ist und sie für diesen Monat nicht ausgeblendet ist (Skizze Zeile 361, Roman 2026-11-06). Sind 1 bis 3 erledigt, verschwindet sie (`mitte-pc.png`).
- „Ausblenden“ blendet sie für den Monat aus und setzt den Filter auf „Alle“ (wie die Skizze). Das gilt auf allen Geräten: gespeichert auf dem Server (Roman 2026-11-06), als Zustand, kein Ereignis (ADR 0083, kein Geld). Ein neuer Monat zeigt sie wieder.
- **Kopfknopf „Monatserster“** (Roman 2026-11-06): ein Icon-Knopf wie Historie (`Button.Ghost`, `Small`, Icon `Icons.calendar`, Title und AriaLabel „Monatserster“), daneben im Kopf, am Handy auch. Er zeigt die Leiste jederzeit, auch ausgeblendet oder fertig, bis die Seite verlassen wird oder „Ausblenden“ gedrückt wird. Ist sie offen, schließt er sie wieder. Er schreibt nichts auf den Server.
- Leiste und Kopfknopf gibt es im laufenden Monat und in kommenden Monaten (wie heute `targetsAllowed`), im vergangenen nicht. Von selbst auf geht sie nur im laufenden. Das Feld Targets prüfen zählt immer für den laufenden Monat (die Seite gilt nur für ihn).

### Kopf der Budget-Seite
- Der Knopf „Targets anwenden“ (`targetsButton`, `MonthView.fs`) und das Icon „Targets prüfen“ (`targetCheckButton`, `View.fs`) entfallen. Beide Wege führen über die Leiste (Roman 2026-11-06, Skizze: im Kopf nur Monat, Historie, Zu verteilen).
- Was an diesen Knöpfen hängt, zieht mit: Tastenkürzel, Sperre beim Monatswechsel (`monthChanging`), Browser-Wächter und Messungen der Kopfbreite (`budget-month-bar`, 1024 px in einer Zeile).

### Vorschau Targets anwenden (Skizze Zeile 183 bis 196)
- Je Zeile eine Checkbox, links vor dem Namen, Betrag rechts in Mono, alle beim Öffnen an. Abgewählte Zeilen werden nicht geschrieben.
- Unter den Zeilen „Summe“ (600) und „Zu verteilen danach“ (gedämpft), beide folgen der Auswahl. Ganz unten zwei gleich breite Knöpfe „Abbrechen“ und „Anwenden“.
- Ist keine Zeile gewählt, ist „Anwenden“ gesperrt.
- Hinweis aus dem Review von budgeting-g8s3w: `PreviewReady` und `ApplyDone` (`Types.fs`) werden Records, `confirmTargets` schickt nur die gewählten Zeilen.
- Schreibweg: Targets als Ereignisse in BB (ADR 0084 Punkt 7, budgeting-eh8xe), kein Zwei-Schritt-Weg nach YNAB.

### Abweichungen von der Skizze (nur diese)
1. Feld 3 heißt „Überzogene decken“, der Filter „Überzogen“ (Roman, budgeting-h3k9d). Die Skizze sagt „rot decken“ und „Rot“.
2. Feld 1 öffnet die Seite Targets prüfen. Die Skizze filtert die Liste (ADR 0082, Roman 2026-11-05: eigene Seite). Der Filter-Chip „Targets prüfen (n)“ der Skizze entfällt deshalb.
3. Kopfknopf „Monatserster“ gibt es in der Skizze nicht (Roman 2026-11-06).
4. Filter-Chips heißen wie heute „Alle, Fehlt, Überzogen“, nicht wie in der Skizze.
5. Vorschau: Stand-Prüfung und Bericht je Zeile bleiben (Konzept, „Bleibt unverändert“). Die Knöpfe folgen ADR 0075 Punkt 3 (neutral). Der orange Anwenden-Knopf der Skizze gilt also nicht, solange der Worker im Styleguide nichts anderes findet. Am Handy bleibt der heutige Dialograhmen (Modal oder Sheet), Inhalt und Reihenfolge wie die Skizze.
6. Farben als Tokens aus `src/Client/DesignSystem/`. Die Hex-Werte oben sind die der Skizze. Gibt es den Token nicht, wird er nach dem Styleguide angelegt, nicht hart verdrahtet.

## Acceptance criteria
- [ ] Reine Funktion für die vier Felder (Wert, Bezeichnung, Zustand offen/erledigt/aktiv) mit Tests je Feld: offen, erledigt (Wert „✓“), aktiv. Feld 1 zählt wie die Kopf-Karte von Targets prüfen (gleiche Funktion, Test mit einer Zeile unter Passt und einer außerhalb).
- [ ] Reine Funktion „steht die Leiste?“ mit Tests: laufender Monat offen; ausgeblendet für diesen Monat; ausgeblendet im Vormonat (steht wieder); Felder 1 bis 3 erledigt bei offenem Feld 4 (steht nicht); kommender Monat (nicht von selbst); vergangener Monat (nie); über den Kopfknopf geöffnet trotz Ausblenden oder fertig (steht).
- [ ] Die Summe von Feld 2 und die Summe der Vorschau kommen aus einer Quelle (Test: gleiche Daten, gleiche Summe; ein pausiertes Target zählt nicht).
- [ ] Klick auf Feld 3 am PC: Filter Überzogen, erste überzogene Zeile gewählt, Tab Geld (Client-`update`-Test). Ohne überzogene Zeile: leere Ansicht des Filters wie heute.
- [ ] Abgewählte Zeilen werden nicht geschrieben (Handler- oder `update`-Test auf das, was `confirmTargets` schickt). Summe und Zu verteilen danach folgen der Auswahl. Ohne Auswahl ist Anwenden gesperrt. Abläufe über mehrere Nachrichten (öffnen, abwählen, anwenden, Monatswechsel) als Aufzählung der Reihenfolgen (Doktrin Punkt 20).
- [ ] Ausblenden je Monat auf dem Server: Endpoint und Lesen über den vorhandenen Weg der Budget-Seite; Handler-Test mit In-Memory-SQLite (setzen, lesen, anderer Monat gilt nicht, wiederholtes Setzen); Zeile in `src/Tests/RemotingEndpointNegativeTests.fs` (ungültiger Monat); Schema per `initializeDatabase`, wiederholbar.
- [ ] Der Knopf „Targets anwenden“ und das Icon „Targets prüfen“ sind aus dem Kopf entfernt. Der Kopfknopf „Monatserster“ steht neben Historie, öffnet und schließt die Leiste und schreibt nichts.
- [ ] Fixtures in `scripts/*.test.mjs` tragen neue Felder (Fable.Remoting-Falle). Browser-Tests, die den alten Kopfknopf nutzen, sind umgestellt oder entfernt.
- [ ] `[layout]`: die vier Felder sind gleich breit und mindestens 52 px hoch, PC 1280 px und Handy 390 px; der Kopf passt bei 1024 px weiter in eine Zeile.
- [ ] Der vorhandene `[flow:targets-anwenden]` (`FLOWS` in `scripts/browser-usage.mjs`) startet an der Leiste: am Handy über die Vorschau mit einer abgewählten Zeile bis zum Bericht. Kein zweiter Flow, der Satz in `FLOWS` wird angepasst.
- [ ] `docs/ux/budget/konzept.md`: der Satz „Die Optik kommt aus Styleguide und echten DS-Komponenten, nicht aus dem Prototyp“ gilt für die Leiste nicht mehr; Kopf ohne Targets anwenden und Targets prüfen, mit Monatserster.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten. Screenshots der Seite in denselben vier Zuständen wie `leiste-pc.png`, `leiste-pc-rot.png`, `leiste-pc-vorschau.png`, `leiste-handy.png` im Outcome, je mit einer Zeile zum Vergleich.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Leiste und Vorschau sehen in Prod aus wie die Skizze. [human-eye]

## Notes
- Hängt an budgeting-k7r2w, weil Feld 1 die Hinweise zählt. Ohne k7r2w stünde etwa Strom fälschlich als Hinweis da.
- Feld 1 braucht `getTargetCheck` auf der Budget-Seite. Laden im Hintergrund, ein Lesefehler macht nur das Feld leer („–“), die Leiste bleibt bedienbar. Nach einem Schreiben auf der Seite Targets prüfen und der Rückkehr stimmt die Zahl (neu laden beim Betreten der Budget-Seite).
- Kopfknopf-Icon: `Icons.calendar` ist ein Vorschlag des Conductors, Roman hat nur den Knopf entschieden.
- Wo der Ausblenden-Zustand liegt (Settings-Schlüssel oder eigene kleine Tabelle), entscheidet der Worker nach ADR 0083 und begründet es im Outcome.
- Konzept: `docs/ux/budget/konzept.md`, Abschnitt „Die vier Abläufe“ und „Am Handy“.

## Verlauf
- 2026-11-05 (budgeting-p2zr6): „Targets prüfen“ ist eine eigene Seite (ADR 0082), kein Prüf-Filter. `CheckOnly` in `CategoryFilter` entfällt.
- 2026-11-05 (budgeting-zkbbd): erst nach budgeting-eh8xe bauen, Targets als Ereignisse in BB.
- 2026-11-06 (Roman, AskUserQuestion): Kopfknopf Monatserster statt Targets anwenden und Prüfen-Icon; Leiste im laufenden Monat bis erledigt; Feld 1 zeigt Hinweise, erledigt bei 0; Ausblenden auf dem Server.
