# Fahrplan: 95 % BB, aus einer Hand

Stand 2026-11-07. Grundlage: ADR 0086, `vision.md` Abschnitt „What success looks like“.

## Stand
Zuletzt aktualisiert: 2026-11-13, nach work-Lauf ledger-nkwqb und banking-import-pknvp.

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
- **Etappe 3 · Ein Produkt:** in elf Schnitte geteilt (Roman, 2026-11-11). 5 erledigt: 1 design-system-k6kfw, 2 ledger-s1weh
  (beide deployt), 3 budgeting-w9yt4 (Angepinnte Kategorien, 828f6a24), 4 banking-import-d0fey (Import direkt, ADR 0093, 613eee26)
  mit ledger-r7h2w (wartendes Duplikat übernehmen oder verwerfen, ef7e039b), ledger-fxt09 (Payee ändern, 0a7d9cf0) und
  banking-import-t4raw (automatisch abrufen, Vorgabe an, 1b1c868f), 5 ledger-v6s7c (Durchgehen, 64a3402f). Work-Lauf
  2026-11-12 nachts, Roman offline: alle Verifier PASS (w9yt4, d0fey, v6s7c in Iteration 2), zwei Review-Läufe mit Behebung
  (cfc21b06..613eee26: 45 Funde, darunter Import-Push ohne Schreib-Sperre; faa240c5..64a3402f: 28 Funde, darunter ein
  schräger Scroll, der als Wisch „Passt“ auslöste). Gate auf e59c5ef0: VERIFY: PASS (dotnet 4751, node 316). Nicht gepusht,
  nicht deployt. Entschieden (Roman, 2026-11-11): wartende Duplikate im Kontostand mitgezählt, Abruf im Intervall mit
  Vorgabe an, alle Funktionen der heutigen Prüfung bleiben (Regel aus der Buchung mit Schnitt 6), Durchgehen älteste zuerst
  und nach §7.14, 4 und 5 werden gemeinsam deployt.
  Romans Blick am Handy und PC (2026-11-13): siehe Eintrag unten. Bestätigt (Roman, 2026-11-13): „Ist doppelt, verwerfen“
  im Akzent, Texte „Alle“ und „Eine Push-TAN fragt es nie an.“ in den Einstellungen.
  ledger-s1weh (Übersicht mit Zu tun): Abweichungen (a) bis (c) vom Prototyp in der Task. Romans Blick am Handy und PC
  (2026-11-11): passt, (a) bis (c) und der Dialog am PC bestätigt.
  Review df9611c4..HEAD (2026-11-11): 22 Funde behoben in 3142ef3e..ec4f4124, darunter Budget aus Zu tun öffnete bei wartender
  Rückkehr im gewählten statt im laufenden Monat und überholte Antworten setzten Zähler zurück. Entschieden (Roman):
  „Buchung“ auf der Übersicht am PC als Dialog (Modal ab 1024 px).
  Deployt 2026-11-11 (dc0e3b9d): ledger-s1weh samt Review-Fixes. Reihenfolge und Zuordnung im Abschnitt Etappe 3.
- **Laufend erledigt (2026-11-10):** ledger-mj3a9 (Bob gesperrt bei ungültigem `BOT_MAX_AMOUNT_EUR`, Nachtrag
  ADR 0068), ledger-q42pg (Feldfehler am Betrag statt Toast), ledger-m7a9q (Probelauf: YNAB zieht die Gegenbuchung
  eines Übertrags mit, Nachtrag ADR 0092); Review 68279598..HEAD mit Fixes 009b3186 und 5444edd7. Neu im Backlog:
  ledger-t4rk8 (Betrag eines Übertrags in BB ändern, braucht Romans Entscheidungen beim Verfeinern). Danach
  design-system-ak97z (Umschalter Ausgabe/Einnahme hält die Tastatur offen, 45273030) mit Review b74d67df. Deployt 2026-11-11 (5f7fc08a).
- **Blick Roman (2026-11-11):** ledger-q42pg und design-system-ak97z am Handy geprüft, passt.
- **Review design-system-k6kfw (2026-11-11):** 31 Funde behoben in 63e9f6a7..ec87e30b, darunter „Abrufen“ startet den
  Sync erst nach dem Lesen der Sitzung (kein zweiter Start, keine zweite Push-TAN) und sichtbare Ladefehler der Konten.
  Entschieden (Roman): Schriftgrößen der Übersicht nach §7.14 (Kriterium in ledger-s1weh), Abbrechen-Pfeil im
  Sync-Ablauf in die Kann-Sammlung banking-import.
- **Blick Roman (2026-11-11):** design-system-k6kfw (Navigation mit drei Orten) am Handy und PC geprüft, passt.
- **Bestandsreview ledger (2026-11-12):** 107 Befunde, Bericht `.agentheim/knowledge/reviews/2026-11-12-ledger.md`. Gruppen G1,
  G2, G4, G5 verfeinert und in 10 Tasks gebaut, alle Verifier PASS (ct3cx in Iteration 2, f8bjm in Iteration 3): ledger-xqqnv
  (vorhandene import_id heißt angekommen, auch bei Barbuchungen), ledger-vfr4h (Sperre und Auslöser belegt), ledger-smv1s
  (Zeilen-Sperre `whenRowIdle`), ledger-sjbvc (Sammellauf in einem Feld), ledger-ct3cx (Liste behält Auswahl, Seiten und
  Scrollstand nach Spiegel-Lauf), ledger-nc708 (Historie an Ladevorgänge gebunden), ledger-ng110 (Split-Editor: offener Rest
  gelb, zu viel rot), ledger-vf6z5 (Neue Buchung mit Feldfehlern), ledger-dn4vy (Filter und Liste nach Styleguide,
  Kontrast-Invariante 10), ledger-f8bjm (Ersatz-POST mit verlorener Antwort: neue Ereignisse und Spalte `pending_replacement`,
  Nachträge ADR 0058 und 0092). Entschieden (Roman, 2026-11-12): Rest gelb/rot, 409 bei Barbuchung angekommen, ausgegraute
  Zeile in `text-muted`. Gate auf 6359f73c: VERIFY: PASS (dotnet 4886, node 322). Nicht gepusht, nicht deployt. G3
  (ledger-h7de3, Fehlertexte in Ereignissen) noch unverfeinert, wartet nicht mehr.
- **Review-Lauf a3f7eb1d..982bdf79 (2026-11-12):** 4 Agents, 29 Funde behoben in 7115d633..738c8407, darunter: Nachladen
  überschrieb neuere Zeilen, gescheitertes „Lena fragen“ verwarf eine offene Eingabe, die Kontoliste bei „Neue Buchung“
  zeigte Laden/Fehler/leer nie (FS0020, jetzt in allen Projekten ein Build-Fehler), die Aufzählung der Zeilen-Sperre stellte
  keine Antworten zu, ein offener Ersatz ohne Fehlschlag blieb gesperrt. Entschieden (Roman): gelbe Hinweise bleiben in der
  ausgegrauten Zeile, „Ersatz“-Texte passen, Toast am Handy als Ticket design-system-vf94m. Gate: VERIFY: PASS (dotnet 4899,
  node 322).
- **Deployt 2026-11-12 (57461f98):** Schnitte 3 bis 5 (w9yt4, d0fey mit r7h2w, fxt09, t4raw, v6s7c) und die zehn Tasks aus dem
  Bestandsreview ledger samt Review-Fixes. Automatischer Abruf ab jetzt an (alle 6 Stunden, bucht direkt), Migration
  `pending_replacement` (ledger-f8bjm). Health-Check grün.
- **Blick Roman (2026-11-13) auf den Deploy 57461f98:** Angepinnt, Abrufen und Import, automatisch abrufen, Durchgehen,
  Auswahl und Scrollstand passen; Payee ändern, Split, Filter und ausgegraute Zeile noch ohne Rückmeldung. Ein Wisch nach links im Durchgehen zeigte
  einmal „Erst eine Kategorie wählen“, nicht reproduzierbar (Roman: wohl falsch gewischt). Neue Buchung am Handy: Memo steht
  neben dem Datum, Ticket ledger-yhemh. Keine wartenden Duplikate gesehen, dafür ein Kontostand-Fehler: ein von YNAB
  abgeglichener Übertrag (ING an Girokonto) steht im Kassenbuch doppelt, weil der Spiegel
  `matched_transaction_id` nicht liest und der Import einen Übertrag nicht als mögliches Duplikat erkennt. Ticket ledger-nkwqb
  (Geld betroffen). In YNAB ist die Buchung einmal da; Roman entfernt dort die Kategorie am Übertrag und gibt ihn frei,
  in BB wird nichts gelöscht (DELETE scheitert am 404, die Comdirect-Referenz ginge verloren). Roh dazu:
  ynab-sync-c7aag (YNABs Payee-Zuordnung überschreibt Importe), banking-import-r28hx (alle Konten per FinTS).
- **Work-Lauf 2026-11-13 (laufend):** ledger-nkwqb (Spiegel legt von YNAB abgeglichene Buchungen zusammen, Vollabruf-Generation 1,
  f41a4d24) und banking-import-pknvp (YNAB-Übertrag als mögliches Duplikat beim Import, 127b9512), beide Verifier PASS in
  Iteration 1. Nachträge ADR 0014, 0015, 0093. Gate im Worktree über den kombinierten Stand: VERIFY: PASS (dotnet 4936, node 322).
  Probelauf gegen eine Kopie der Produktion (nur GET bei YNAB): 3 Zusammenlegungen (zwei ING-Überträge auf dem Girokonto, eine
  Barbuchung von Bob, die YNAB mit einer älteren Buchung gleichen Betrags abgeglichen hat), danach stimmen alle Kontostände mit
  YNAB. Nicht gepusht, nicht deployt.
- **Nächster Schritt:** Romans Antwort zur Barbuchung (eine oder zwei Zahlungen; bei zwei den Abgleich in YNAB vor dem Deploy
  aufheben), dann Deploy auf Anweisung (der erste Spiegel-Lauf danach liest einmal alles). Review vorgeschlagen:
  `/review-agents` mit backend-code-quality und qa-milestone-reviewer. Danach Schnitt 6 `categorization-n3sxw` (Regeln ins
  Kassenbuch) verfeinern. Offen zum Verfeinern: ledger-yhemh (Memo unter Datum), ledger-h7de3 (Fehlertexte in Ereignissen),
  design-system-vf94m (Toast verdeckt „+ Buchung“), ledger-t4rk8 (Betrag eines Übertrags in BB ändern).
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

Schnitte (Roman, 2026-11-11). Zuerst 1 und 2, die Navigation geht mit einer schlichten Übersicht live.
Bis 4 und 5 stehen, zeigt „Zu tun“ auf den heutigen Sync-Ablauf; der Sync ist dann kein Ort mehr.
Baut ein Schnitt eine Seite um, gilt die Zeile „Browser-Tests beim Umbau“ als Kriterium.

1. ✓ `design-system-k6kfw` Navigation mit drei Orten, Zahnrad, ab 768 px links (2026-11-11)
2. ✓ `ledger-s1weh` Übersicht als Einstieg (Zu tun, Monat, Konten am Handy, „+ Buchung“) (2026-11-11)
3. ✓ `budgeting-w9yt4` Angepinnte Kategorien (2026-11-12)
4. ✓ `banking-import-d0fey` Import direkt ins Kassenbuch (2026-11-12) (nimmt `banking-import-t4raw`, `categorization-hz6vj` auf)
5. ✓ `ledger-v6s7c` Durchgehen Karte für Karte (2026-11-12)
6. `categorization-n3sxw` Regeln ins Kassenbuch (nimmt `categorization-krscs`, `ledger-hg5m5` auf)
7. `ledger-ea70s` Ein Erfassen, Quick Add entfällt (nimmt `design-system-taytp` auf)
8. `ledger-f8d7x` Kassenbuch ordnen, beim Verfeinern nach Handy und PC teilen (nimmt `ledger-ayjzq` auf)
9. `design-system-gcpws` Liste mit Panel überall, generischer Tastentest
10. `budgeting-wnkgj` Budget angleichen
11. `design-system-x1abn` Logo und Icon

Eigenständig bleiben `banking-import-q7vd2` (confirmTan-Fehlerzweig) und `ledger-nb4tv` (ETF-Konto).
Die Zeilen darunter sind die Quelle der Schnitte.

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
- `infrastructure-rm6zb` Wächter gegen Formatangaben in interpolierten Strings (Fable)

## Danach, bei Bedarf
- Reports (bis dahin in YNAB).
- Budget-Stufe 3: erwartete Einnahme, Leihen, Entwurf, Targets mit Deckel.
- ETF-Bewertung, ING-Import.
- Benachrichtigungen: `infrastructure-jsys4`, `banking-import-r9tfx`, `ledger-g47hp`
- `budgeting-bk6tq` Spike Vortrag ab dem ersten Tag
- `infrastructure-phadg` Altlasten Test-Doktrin
