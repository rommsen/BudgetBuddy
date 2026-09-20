---
id: ledger-k3wqn
title: "Payee- und Memo-Filter finden keine Umlaut-Schreibweisen: COLLATE NOCASE faltet nur ASCII"
status: backlog
type: bug
context: ledger
created: 2026-09-20
completed:
depends_on: []
blocks: []
tags: [backend, kassenbuch, filter, suche]
related_adrs: [0036]
related_research: []
prior_art: [ledger-vgfw3]
---

## Why

`ledger-vgfw3` (ADR 0036) hat Payee- und Memo-Filter im Kassenbuch-Lesepfad eingeführt und
beide bewusst ohne Beachtung der Groß-/Kleinschreibung gebaut:

- `payee = @payee COLLATE NOCASE`
- `memo LIKE @memo COLLATE NOCASE`

SQLites `NOCASE` faltet aber **nur ASCII A-Z**. Sobald das Zeichen, das gefaltet werden
müsste, ein Umlaut ist, greift die Faltung nicht. Gegen sqlite3 nachgemessen (2026-09-20):

```
INSERT INTO t VALUES ('MÜLLER GMBH'), ('Müller GmbH');

SELECT payee FROM t WHERE payee = 'müller gmbh' COLLATE NOCASE;
  -> liefert NUR 'Müller GmbH'
  -> liefert NICHT 'MÜLLER GMBH'

SELECT memo FROM t WHERE memo LIKE '%grüne%' COLLATE NOCASE;
  -> liefert NUR 'Grüne Erde'
  -> liefert NICHT 'GRÜNE ERDE Rechnung'
```

Zur Gegenprobe: rein ASCII funktioniert (`'cafe lotte'` findet `'Cafe Lotte'`).

**Warum das hier wirklich weh tut.** Comdirect- und YNAB-Verwendungszwecke stehen häufig in
Großbuchstaben: `MÜLLER`, `BÄCKEREI`, `GRÜNE`, `STRAßE`. Der Memo-Filter ist das Feld, in das
Roman frei tippt. Tippt er `müller` gegen ein gespeichertes `MÜLLER`, kommt **kein Treffer**
zurück, ohne Fehler und ohne Hinweis, dass die Suche an dieser Stelle doch
groß-/kleinschreibungs-empfindlich ist. Das ist die schlechteste Form eines Fehlers: er sieht
aus wie "es gibt nichts".

**Warum kein Test das gefunden hat.** Alle acht Filter-Tests aus `ledger-vgfw3` säen reines
ASCII (`"cafe lotte"` / `"Cafe Lotte"` in AC6, das transliterierte `"Baecker"` in AC7). Der
Fall kann dort strukturell nicht auftreten. Gefunden im QA-Review am Ende des Work-Laufs vom
2026-09-19, nicht von der Verifikation der Aufgabe selbst.

**Nicht zu verwechseln mit `ledger-h4qtx`.** Jenes Item behandelt die Payee-*Vorschläge*, die
groß-/kleinschreibungs-empfindlich gruppieren, und setzt dabei voraus, dass `NOCASE`
funktioniert. Es behebt diesen Fehler nicht.

## What

Zu entscheiden ist zuerst, ob der Fehler behoben oder die Einschränkung bewusst
festgeschrieben wird. Beides ist vertretbar, aber der heutige Zustand ist keins von beidem:
die README verspricht "ohne Groß-/Kleinschreibung", der Code hält das für Umlaute nicht.

Denkbare Richtungen, nicht vorentschieden:

- **Vergleich normalisieren**, etwa beidseitig über `lower()` plus eine eigene, umlautfeste
  Faltung, oder eine gepflegte Normalform-Spalte (`payee_normalized`) mit eigenem Index. Die
  Index-Frage ist hier real: der Payee-Filter ist heute ein Gleichheitsvergleich und damit
  indexfähig, eine Funktion darum herum wäre es nicht mehr (dieselbe Abwägung wie bei
  `substr(date, 1, 10)` in ADR 0036).
- **SQLites ICU-Erweiterung**, falls sie im Docker-Image überhaupt verfügbar ist. Zu prüfen,
  nicht anzunehmen.
- **Bewusst bei ASCII-Faltung bleiben** und es überall ehrlich benennen: im README, in ADR
  0036 und in der Oberfläche (etwa ein Hinweis in der leeren Trefferliste). Dann gehört ein
  Test dazu, der die Grenze festnagelt, statt sie offen zu lassen.

## Acceptance criteria

- [ ] Entscheidung getroffen und begründet: Faltung reparieren oder Einschränkung
      festschreiben.
- [ ] Ein Test sät die Schreibweise, die die Produktion tatsächlich liefert (Großbuchstaben
      mit Umlauten, `MÜLLER GMBH` / `GRÜNE ERDE`), nicht transliteriertes ASCII, und pinnt das
      entschiedene Verhalten. Bei "reparieren" muss er gegen den heutigen Code rot sein.
- [ ] README und ADR 0036 sagen dasselbe wie der Code. Steht dort weiterhin "ohne
      Groß-/Kleinschreibung", muss das dann auch für Umlaute gelten.
- [ ] Wird normalisiert: die Auswirkung auf die Indexnutzung ist benannt, wie ADR 0036 es für
      den Zeitraum-Filter schon tut.
- [ ] `dotnet build` und `dotnet test` grün.

## Notes

Gefunden im QA-Review (`qa-milestone-reviewer`) nach dem Work-Lauf vom 2026-09-19, unabhängig
gegen sqlite3 nachgeprüft. Der Befund betrifft ausgelieferten Code auf `feature/kassenbuch`,
Commit `203232d`.
