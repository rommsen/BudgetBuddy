# Fahrplan: 95 % BB, aus einer Hand

Stand 2026-11-07. Grundlage: ADR 0086, `vision.md` Abschnitt „What success looks like“.

## Stand
Zuletzt aktualisiert: 2026-11-09, nach dem work-Lauf zu ledger-m7k2d samt Review.

- **Etappe 1 · Bedeutungen:** erledigt, 5 von 5 (Skizze und Prototyp, Layout-Sweep, Muster im
  Styleguide, Designwert-Tests aufgeräumt, Palette 2 im Token-Layer). Romans Blick auf dak16 neben
  dem Prototyp: freigegeben (2026-11-08). Entschieden (Roman, 2026-11-08): Fokusring, Textlinks und
  Marke im Akzent, gescheiterter Push bleibt Gelb (Nachtrag ADR 0087).
  Panel ab 1024 px (Roman, 2026-11-07).
- **Etappe 2 · Lücken schließen:** 6 von 9 gebaut und in main (2026-11-08): ledger-p3vq8,
  budgeting-pscgb, ledger-qvjvs, budgeting-sn2sn, budgeting-e2z7v, ledger-at63w. Neue ADRs 0089
  (Übertrag in BB), 0090 (Targets nach YNAB), 0091 (Erfasste Buchung verwerfen). Offen in Etappe 2:
  ledger-nb4tv (ETF-Konto), ledger-z6sya (Buchung löschen im Detail), ledger-m7mye (Betrag bearbeiten im
  Detail). Bug ledger-m7k2d (Split einer Barbuchung nicht änderbar) in main (2026-11-09), noch nicht deployt.
  Bug budgeting-t7dka (Targets anwenden übergeht deaktivierte Kategorien) in main (2026-11-09,
  40631e3f, Review 518d4ac3, Nachtrag ADR 0088), von Roman deployt (2026-11-09).
- **Etappe 3 · Ein Produkt:** noch nicht verfeinert, Zeilen wachsen.
- **Nächster Schritt:** ledger-m7k2d deployen (auf Romans Anweisung). Gebaut und in main (2026-11-09,
  bc3c22ca, Review 2edff908..c40ee10c, Nachtrag ADR 0058): eine aufgeteilte Barbuchung lässt sich wieder
  ändern. budgeting-t7dka ist deployt (2026-11-09).
- **Danach:** ledger-z6sya (Buchung löschen) und ledger-m7mye (Betrag bearbeiten) mit dem Prototyp daneben
  verfeinern, dann ledger-nb4tv (ETF-Konto und Abgleich).

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

## Etappe 1 · Bedeutungen festlegen (erledigt 2026-11-08)
Ziel: Jede Farbe und jedes Muster heißt überall dasselbe, und die drei Orte sind als Skizze
freigegeben. Klein halten.

Entschieden in ADR 0087 (Palette 2, Lena als Flag, drei Orte, Panel statt Dialog, Tastenregeln).
Vorlage für alles Weitere: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17,
Handy und PC), Skizze https://claude.ai/artifact/HPpe24Y1owgpSZLWvtdGWd (Version 2).

Reihenfolge: `infrastructure-nx14s` → `infrastructure-jc1y1` → `design-system-dak16`;
`design-system-bcdqy` läuft unabhängig. `jc1y1` und `dak16` sind verfeinert und warten in
`backlog/`, bis ihr Vorgänger erledigt ist; dann promoten.

- [x] Skizze und Prototyp der drei Orte, von Roman freigegeben (2026-11-07)
- [x] `infrastructure-nx14s` Layout-Invarianten als ein Sweep (2026-11-07)
- [x] `infrastructure-jc1y1` Designwert-Tests aufräumen (2026-11-08, Verifier PASS in Iteration 4)
- [x] `design-system-dak16` Palette 2 im Token-Layer, Kontrast-Wächter (2026-11-08, Verifier PASS in Iteration 2, Blick Roman freigegeben)
- [x] `design-system-bcdqy` Muster im Styleguide (2026-11-07, von Roman freigegeben)

## Etappe 2 · Lücken schließen
Ziel: Nichts zwingt mehr zu YNAB außer Reports.

- ✓ `ledger-at63w` Buchung im Kassenbuch erfassen für jedes Konto ohne Import, mit Vorbelegung
  (Roman 2026-11-08: Import-Konto erst mit Abgleich, der kommt später).
- ✓ `budgeting-pscgb` Kategorien verwalten: anlegen, umbenennen, Gruppe wechseln, Gruppen (ADR 0088)
- ✓ `budgeting-sn2sn` Kategorie deaktivieren und eigene Reihenfolge, nur in BB (ADR 0088)
- ✓ `budgeting-t7dka` Bug: Targets anwenden übergeht in BB deaktivierte Kategorien (Nachtrag ADR 0088)
- ✓ `budgeting-e2z7v` Targets beim Speichern direkt nach YNAB (Nachtrag ADR 0084, Roman 2026-11-08)
- ✓ `ledger-qvjvs` Buchung zum Übertrag machen (Konten im Picker des Details, Schreibweg nach YNAB)
- ✓ `ledger-p3vq8` Push gelungen, Verknüpfung nicht geschrieben
- `ledger-nb4tv` ETF-Konto und Abgleich
- `ledger-z6sya` Buchung löschen im Detail (auch in YNAB), mit Rückgängig.
- `ledger-m7mye` Betrag einer Buchung bearbeiten im Detail (auch in YNAB). Heute geht das in BB nicht; im Prototyp
  ab Version 15 (Roman, 2026-11-08).

## Etappe 3 · Ein Produkt
Ziel: Drei Orte, eine Sprache, Seite für Seite an der Skizze.

Prototyp Version 15 bis 17 (Roman, 2026-11-08, am Handy durchgegangen) korrigieren Version 14:
Budget-Tabs mit fester Höhe, Monatsbalken in der Historie mit Beträgen (Styleguide „Monatsbalken“),
Split-Beträge ohne Vorzeichen und Tastatur bleibt beim Feldwechsel offen, Konten als Ziel in jeder
Kategorieauswahl einer Buchung (Detail, Durchgehen, Erfassen, Split; Übertrag mit Gegenbuchung),
Betrag im Buchungsdetail bearbeiten, Amazon nur als Link mit Bestellnummer (keine API).

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
- Panel rund 30 % der Breite und ab 1024 px (heute fest 340, 360 und 420 px;
  Styleguide §7.6).
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
- Navigation ab 768 px (`md`) links als Seitenleiste oder Rail (heute Leiste oben; Styleguide §7.4
  und §7.15).
- Logo und Icon: Vorlage Buddy in `standards/frontend/brand/` (Entwurf 2026-11-07, Palette 2).
- `banking-import-t4raw` zeitgesteuertes Vorbereiten
- `banking-import-q7vd2` confirmTan-Fehlerzweig
- `categorization-hz6vj` Kategorisieren im Import
- `design-system-taytp` Kategorie-Picker vereinheitlichen: Import und Erfassen nutzen nur `CategoryPicker`,
  `BottomSheet.categoryPicker` entfällt (aus Etappe 2 verschoben, Roman 2026-11-08)
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
- `infrastructure-phadg` Altlasten Test-Doktrin
