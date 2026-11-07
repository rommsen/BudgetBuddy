# Fahrplan: 95 % BB, aus einer Hand

Stand 2026-11-07. Grundlage: ADR 0086, `vision.md` Abschnitt „What success looks like“.

Regeln:
- Verfeinert wird nur die nächste Etappe. Spätere Etappen haben Zeilen, keine Tickets.
- Neue Ideen für spätere Etappen kommen hier als Zeile dazu.
- Bestehende Tasks tragen einen Tag: `etappe-1`, `etappe-2`, `etappe-3`, `laufend`, `danach`.
- Funde ohne Geld-, Daten-, Sicherheits- oder Alltagsfehler gehen in die Kann-Sammlung des BC.

## Etappe 1 · Bedeutungen festlegen (läuft)
Ziel: Jede Farbe und jedes Muster heißt überall dasselbe, und die drei Orte sind als Skizze
freigegeben. Klein halten.

- Entschieden 2026-11-07 (Skizze Version 2, https://claude.ai/artifact/HPpe24Y1owgpSZLWvtdGWd,
  Recherchen `farbschema-neon-dark-2026-11-07`, `seitenlayout-liste-detail-2026-11-07`):
  Pink entfällt; ein Akzent (Cyan) für Aktion und Auswahl, Orange entfällt als Aktionsfarbe (Palette 2);
  Lena über ein Flag-Symbol in der YNAB-Flag-Farbe aus den Settings, Farbe nur im Symbol, immer mit Name;
  Durchgehen startet in der Übersicht, das Kassenbuch zeigt Kontowahl, Suche und Filter-Knopf;
  Regeln als Tab im Kassenbuch; Layout-Regeln aus der Skizze als Grundlage.
- Bedeutungsschicht des Styleguides: Farben (aufbauend auf ADR 0075) und die Muster
  Auswahl aus wenigen Optionen, Historie, Seitenpanel, Liste mit Aktionen, leerer Zustand.
  *Wird nach der Skizze erfasst.*
- Skizze der drei Orte (Übersicht, Kassenbuch, Budget) als claude.ai-Artifact, von Roman
  freigegeben.
- `infrastructure-nx14s` Layout-Invarianten als ein Sweep
- `infrastructure-jc1y1` Designwert-Tests aufräumen

## Etappe 2 · Lücken schließen
Ziel: Nichts zwingt mehr zu YNAB außer Reports.

- Buchung erfassen im Kassenbuch für jedes Konto, wenige Klicks, kluge Vorbelegung
  (das Quick-Add-Versprechen für alle Konten; ING-Tagesgeld).
- Kategorien verwalten: anlegen, umbenennen, Gruppe wechseln, deaktivieren (nie löschen).
  Neuer Schreibweg nach YNAB.
- `design-system-taytp` Kategorie-Picker vereinheitlichen
- `ledger-p3vq8` Push gelungen, Verknüpfung nicht geschrieben
- `ledger-nb4tv` ETF-Konto und Abgleich

## Etappe 3 · Ein Produkt
Ziel: Drei Orte, eine Sprache, Seite für Seite an der Skizze.

- Übersicht als Einstieg statt Sync.
- Import ins Kassenbuch: automatisch und jederzeit von Hand anstoßbar.
- Durchgehen neuer Buchungen, Karte für Karte bestätigen.
- Regeln ins Kassenbuch.
- Quick Add hört auf, ein eigener Ort zu sein.
- Budget an die Bedeutungen angleichen (Targets prüfen: zu viel Orange).
- Kassenbuch: Seitenpanel ordnen, Historie wie im Budget.
- Am PC große Leerflächen, am Handy Kontowahl mal Pillenleiste, mal Selectbox: vereinheitlichen.
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
