# Fahrplan: 95 % BB, aus einer Hand

Stand 2026-11-07. Grundlage: ADR 0086, `vision.md` Abschnitt „What success looks like“.

## Stand
Zuletzt aktualisiert: 2026-11-07, nach work und Review von nx14s und bcdqy.

- **Etappe 1 · Bedeutungen:** läuft, 3 von 5 erledigt (Skizze und Prototyp, Layout-Sweep,
  Muster im Styleguide). Offen bei bcdqy: Freigabe der Styleguide-Abschnitte §7.4 bis §7.15
  durch Roman, dazu seine Entscheidung, ob das Panel ab 840 px (Styleguide) oder ab 1024 px
  (Prototyp) kommt.
- **Etappe 2 · Lücken schließen:** noch nicht verfeinert.
- **Etappe 3 · Ein Produkt:** noch nicht verfeinert, Zeilen wachsen.
- **Nächster Schritt:** `infrastructure-jc1y1` promoten, `/agentheim:work`, Review.
- **Danach:** `design-system-dak16`, dann Etappe 2 verfeinern.

## Ablauf je Schritt
1. Nächsten Schritt aus „Stand“ nennen.
2. Verfeinern, nur Tasks der laufenden Etappe; promoten.
3. `/agentheim:work` (Worker, Verifier).
4. `/review-agents`; Funde unter der Muss-Schwelle in die Kann-Sammlung, nicht ins Backlog.
5. Roman prüft die `[human-eye]`-Kriterien (Seite gegen Prototyp).
6. Haken setzen, „Stand“ aktualisieren, committen.
7. Wird der Kontext knapp: „Stand“ ist die Übergabe; nach `/compact` zuerst diese Datei lesen.

Regeln:
- Verfeinert wird nur die nächste Etappe. Spätere Etappen haben Zeilen, keine Tickets.
- Neue Ideen für spätere Etappen kommen hier als Zeile dazu.
- Bestehende Tasks tragen einen Tag: `etappe-1`, `etappe-2`, `etappe-3`, `laufend`, `danach`.
- Funde ohne Geld-, Daten-, Sicherheits- oder Alltagsfehler gehen in die Kann-Sammlung des BC.

## Etappe 1 · Bedeutungen festlegen (läuft)
Ziel: Jede Farbe und jedes Muster heißt überall dasselbe, und die drei Orte sind als Skizze
freigegeben. Klein halten.

Entschieden in ADR 0087 (Palette 2, Lena als Flag, drei Orte, Panel statt Dialog, Tastenregeln).
Vorlage für alles Weitere: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 14,
Handy und PC), Skizze https://claude.ai/artifact/HPpe24Y1owgpSZLWvtdGWd (Version 2).

Reihenfolge: `infrastructure-nx14s` → `infrastructure-jc1y1` → `design-system-dak16`;
`design-system-bcdqy` läuft unabhängig. `jc1y1` und `dak16` sind verfeinert und warten in
`backlog/`, bis ihr Vorgänger erledigt ist; dann promoten.

- [x] Skizze und Prototyp der drei Orte, von Roman freigegeben (2026-11-07)
- [x] `infrastructure-nx14s` Layout-Invarianten als ein Sweep (2026-11-07)
- `infrastructure-jc1y1` Designwert-Tests aufräumen (bereit zum Promoten)
- `design-system-dak16` Palette 2 im Token-Layer, Kontrast-Wächter (wartet auf jc1y1)
- [x] `design-system-bcdqy` Muster im Styleguide (2026-11-07, Freigabe Roman offen)

## Etappe 2 · Lücken schließen
Ziel: Nichts zwingt mehr zu YNAB außer Reports.

- Buchung erfassen im Kassenbuch für jedes Konto, wenige Klicks, kluge Vorbelegung
  (das Quick-Add-Versprechen für alle Konten; ING-Tagesgeld).
- Kategorien verwalten: anlegen, umbenennen, Gruppe wechseln, deaktivieren (nie löschen).
  Neuer Schreibweg nach YNAB.
- `design-system-taytp` Kategorie-Picker vereinheitlichen
- `ledger-p3vq8` Push gelungen, Verknüpfung nicht geschrieben
- `ledger-nb4tv` ETF-Konto und Abgleich
- Buchung löschen im Detail (auch in YNAB), mit Rückgängig.
- Kategorie deaktivieren nur ohne verfügbares Geld; Gruppe als Auswahlfeld mit „Neue Gruppe“.

## Etappe 3 · Ein Produkt
Ziel: Drei Orte, eine Sprache, Seite für Seite an der Skizze.

- Übersicht als Einstieg statt Sync, Variante A: Zu tun (eine Hauptaktion), Monat mit Balken und
  Marke „heute“, Angepinnt, am Handy Konten.
- Angepinnte Kategorien (wählbar auf der Übersicht und im Kategorie-Detail, Zustand, kein Ereignis).
- Import ins Kassenbuch: direkt in Kassenbuch und YNAB als „zu prüfen“, nur unsichere Duplikate
  warten; automatisch und jederzeit von Hand anstoßbar.
- Durchgehen neuer Buchungen, Karte für Karte bestätigen, mit Amazon-Link und Duplikat-Karte.
- „+ Buchung“ auch in der Übersicht.
- PC: Seitenleiste mit Konten, Liste plus Panel überall (auch Regeln, Erfassen, Targets prüfen,
  Kategorien), Mehrfachauswahl mit Zusammenfassung im Panel.
- Tastenregel aus dem Budget für alle Listen; Auswahl-Dialoge mit Fokus in der Suche.
- Regeln ins Kassenbuch.
- Quick Add hört auf, ein eigener Ort zu sein.
- Budget an die Bedeutungen angleichen (Targets prüfen: zu viel Orange).
- Kassenbuch: Seitenpanel ordnen, Historie wie im Budget.
- Am PC große Leerflächen, am Handy Kontowahl mal Pillenleiste, mal Selectbox: vereinheitlichen.
- Navigation nur mit den drei Orten, Einstellungen hinter einem Symbol (heute sechs Punkte;
  Styleguide §7.4).
- Kassenbuch am Handy: die zwei wischenden Leisten entfallen, Filter-Knopf mit Sheet und gesetzte
  Filter als Chips (Styleguide §7.5).
- Wochentag eines Targets als Auswahlfeld statt sieben Knöpfe im `Segmented` (Styleguide §7.5).
- Panel rund 30 % der Breite und ab 840 px (heute fest 340, 360 und 420 px, Targets prüfen erst ab
  1024 px; Styleguide §7.6).
- Kategorie-Picker nicht mehr über einem Sheet stapeln (`CategoryPicker.sheetLayered`); die Auswahl
  ersetzt den Inhalt des Sheets und führt zurück (Styleguide §7.15).
- Rückmeldung am PC über der Auswahl-Leiste des Kassenbuchs statt auf ihr (Styleguide §7.13).
- Budget-Historie: Filter nach Art als Auswahlfeld „Zeigen“ statt `ChipBar` (Styleguide §7.11).
- Browser-Tests beim Umbau: Wird eine Seite umgebaut, fliegen ihre `[flow]`-, `[tastatur]`-,
  `[fokus]`- und `[scroll]`-Tests raus und werden gegen die Muster aus dem Styleguide neu
  geschrieben, je Flow einer. Nicht nachziehen. `[fable]`, der Layout-Sweep und die Backend-Tests
  bleiben (Roman, 2026-11-07).
- Tastenregeln aus Styleguide §7.8 und §7.9 als ein generischer Test über alle Listen mit Panel,
  wie der Layout-Sweep, statt eines Tests je Seite.
- Navigation ab 600 px links als Seitenleiste oder Rail (heute Leiste oben; Styleguide §7.4 und §7.15).
- Breakpoints 600 und 840 px aus Styleguide §7.15 in Token-Layer und `@theme`; heute schalten
  Trefferflächen (`TouchTargets`) und der Layout-Sweep bei `md` (768 px), Sheets bei `lg` (1024 px).
  Hängt an Romans Entscheidung zum Panel (840 oder 1024 px).
- Logo und Icon: Vorlage Buddy in `standards/frontend/brand/` (Entwurf 2026-11-07, Palette 2).
- `banking-import-t4raw` zeitgesteuertes Vorbereiten
- `banking-import-q7vd2` confirmTan-Fehlerzweig
- `categorization-hz6vj` Kategorisieren im Import
- `categorization-krscs` Regel-Dialog
- `ledger-hg5m5` Regel-Angebot nach Kategorie-Änderung
- `ledger-ayjzq` Sicht „Für mich“ (Lena-Filter)

## Laufend
Unabhängig von den Etappen, weil es im Alltag stört, eine Sicherheitsgrenze betrifft oder
jede Arbeit bremst.

- `infrastructure-g4wy8` Verbindungsabbrüche
- `ledger-mj3a9` Bob-Betragsdeckel
- `infrastructure-mshaf` Gate schneller und stabil

## Danach, bei Bedarf
- Reports (bis dahin in YNAB).
- Budget-Stufe 3: erwartete Einnahme, Leihen, Entwurf, Targets mit Deckel.
- ETF-Bewertung, ING-Import.
- Benachrichtigungen: `infrastructure-jsys4`, `banking-import-r9tfx`, `ledger-g47hp`
- `budgeting-bk6tq` Spike Vortrag ab dem ersten Tag
- `budgeting-e2z7v` Targets nach YNAB zurückschreiben
- `infrastructure-phadg` Altlasten Test-Doktrin
