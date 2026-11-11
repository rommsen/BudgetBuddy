# Fahrplan: 95 % BB, aus einer Hand

Stand 2026-11-07. Grundlage: ADR 0086, `vision.md` Abschnitt „What success looks like“.

## Stand
Zuletzt aktualisiert: 2026-11-10, nach work-Lauf und Review zu ledger-mj3a9, ledger-q42pg, ledger-m7a9q.

- **Etappe 1 · Bedeutungen:** erledigt, 5 von 5 (Skizze und Prototyp, Layout-Sweep, Muster im
  Styleguide, Designwert-Tests aufgeräumt, Palette 2 im Token-Layer). Romans Blick auf dak16 neben
  dem Prototyp: freigegeben (2026-11-08). Entschieden (Roman, 2026-11-08): Fokusring, Textlinks und
  Marke im Akzent, gescheiterter Push bleibt Gelb (Nachtrag ADR 0087).
  Panel ab 1024 px (Roman, 2026-11-07).
- **Etappe 2 · Lücken schließen:** erledigt, 8 von 8. Neu (2026-11-09): ledger-z6sya (Buchung
  löschen im Detail, mit Rückgängig, 67becce0) und ledger-m7mye (Betrag bearbeiten im Detail, ee0cc661),
  beide nach ADR 0092; Review 01b989fc..8561beb8 mit Fixes fe515af6..2c808771 (unter anderem doppelte
  Neuanlage nach halbem Split-Ersatz behoben). Romans Blick auf beide am Handy und PC (2026-11-10): passt;
  Abweichungen roter Grund, Höhe und Position des Lösch-Knopfs und Modal-Hintergrund angenommen. Leerer
  Betrag als Meldung am Feld statt Toast: ledger-q42pg. Bank-Referenz nach Betragsänderung per Test belegt,
  im Alltag beim nächsten Import auf Duplikat achten. Folge-Spike ledger-m7a9q (Betrag eines Übertrags per Update) im
  Backlog, danach kann die Sperre für Überträge fallen. ledger-nb4tv (ETF-Konto und Abgleich) nach
  Etappe 3 verschoben (Roman, 2026-11-10).
  Deployt (2026-11-10, f85d3b3d): ledger-m7k2d, ledger-z6sya, ledger-m7mye.
- **Etappe 3 · Ein Produkt:** noch nicht verfeinert, Zeilen wachsen.
- **Laufend erledigt (2026-11-10):** ledger-mj3a9 (Bob gesperrt bei ungültigem `BOT_MAX_AMOUNT_EUR`, Nachtrag
  ADR 0068), ledger-q42pg (Feldfehler am Betrag statt Toast), ledger-m7a9q (Probelauf: YNAB zieht die Gegenbuchung
  eines Übertrags mit, Nachtrag ADR 0092); Review 68279598..HEAD mit Fixes 009b3186 und 5444edd7. Neu im Backlog:
  ledger-t4rk8 (Betrag eines Übertrags in BB ändern, braucht Romans Entscheidungen beim Verfeinern). Danach
  design-system-ak97z (Umschalter Ausgabe/Einnahme hält die Tastatur offen, 45273030) mit Review b74d67df. Deployt 2026-11-11 (5f7fc08a).
- **Blick Roman (2026-11-11):** ledger-q42pg und design-system-ak97z am Handy geprüft, passt.
- **Nächster Schritt:** Etappe 3 in Tickets schneiden, zuerst Navigation mit drei Orten und Übersicht als Einstieg.
  Offen zum Verfeinern mit Roman: ledger-t4rk8 (Betrag eines Übertrags in BB ändern).
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

## Etappe 2 · Lücken schließen (erledigt 2026-11-10)
Ziel: Nichts zwingt mehr zu YNAB außer Reports.

- ✓ `ledger-at63w` Buchung im Kassenbuch erfassen für jedes Konto ohne Import, mit Vorbelegung
  (Roman 2026-11-08: Import-Konto erst mit Abgleich, der kommt später).
- ✓ `budgeting-pscgb` Kategorien verwalten: anlegen, umbenennen, Gruppe wechseln, Gruppen (ADR 0088)
- ✓ `budgeting-sn2sn` Kategorie deaktivieren und eigene Reihenfolge, nur in BB (ADR 0088)
- ✓ `budgeting-t7dka` Bug: Targets anwenden übergeht in BB deaktivierte Kategorien (Nachtrag ADR 0088)
- ✓ `budgeting-e2z7v` Targets beim Speichern direkt nach YNAB (Nachtrag ADR 0084, Roman 2026-11-08)
- ✓ `ledger-qvjvs` Buchung zum Übertrag machen (Konten im Picker des Details, Schreibweg nach YNAB)
- ✓ `ledger-p3vq8` Push gelungen, Verknüpfung nicht geschrieben
- ✓ `ledger-z6sya` Buchung löschen im Detail (auch in YNAB), mit Rückgängig (Blick Roman 2026-11-10: passt).
- ✓ `ledger-m7mye` Betrag einer Buchung bearbeiten im Detail (auch in YNAB), im Prototyp ab Version 15
  (Roman, 2026-11-08); Blick Roman 2026-11-10: passt, Feldfehler statt Toast in `ledger-q42pg`.
  Überträge gesperrt bis Spike `ledger-m7a9q`.

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
- `ledger-nb4tv` ETF-Konto und Abgleich (aus Etappe 2 verschoben, Roman 2026-11-10)

## Laufend
Unabhängig von den Etappen, weil es im Alltag stört, eine Sicherheitsgrenze betrifft oder
jede Arbeit bremst.

- `infrastructure-g4wy8` Verbindungsabbrüche
- ✓ `ledger-mj3a9` Bob-Betragsdeckel (2026-11-10)
- `ledger-t4rk8` Betrag eines Übertrags in BB ändern
- ✓ `design-system-ak97z` Umschalter im Sheet Betrag hält die Tastatur offen (2026-11-10)
- `infrastructure-mshaf` Gate schneller und stabil

## Danach, bei Bedarf
- Reports (bis dahin in YNAB).
- Budget-Stufe 3: erwartete Einnahme, Leihen, Entwurf, Targets mit Deckel.
- ETF-Bewertung, ING-Import.
- Benachrichtigungen: `infrastructure-jsys4`, `banking-import-r9tfx`, `ledger-g47hp`
- `budgeting-bk6tq` Spike Vortrag ab dem ersten Tag
- `infrastructure-phadg` Altlasten Test-Doktrin
