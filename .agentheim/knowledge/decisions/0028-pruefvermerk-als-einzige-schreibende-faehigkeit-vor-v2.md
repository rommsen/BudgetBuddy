---
id: 0028
title: Der Prüfvermerk ist die einzige schreibende Fähigkeit, die vor v2 nach BB wandert
scope: ledger
status: accepted
date: 2026-09-12
supersedes: []
superseded_by: []
related_tasks: [ledger-s8m3w, ledger-xbrht, ledger-kdnnz]
related_adrs: [0012, 0013, 0014, 0021]
related_research: []
---

# ADR 0028: Der Prüfvermerk ist die einzige schreibende Fähigkeit, die vor v2 nach BB wandert

## Context
Die Vision kennt zwei Stufen. **v1** ist lesend: alle Konten unter einem Dach, ein
vollständiges Kassenbuch, das YNABs Zustand spiegelt und sich zusätzlich merkt, was BB
selbst entschieden hat. **v2** ist Ownership: Roman öffnet YNAB nicht mehr zum
Nachbearbeiten, BB wird für Kategorie, Split und Übertrag führend, YNAB wird nachgezogen,
Abweichungen werden sichtbar (ADR 0012).

`ledger-xbrht` hat das UX-Konzept für das Kassenbuch als Arbeitsfläche entworfen. Dabei
wurde ein Widerspruch sichtbar, den die Stufen-Einteilung so nicht vorgesehen hatte:

**Romans häufigster Ablauf am Handy ist schreibend.** Er geht neue Buchungen durch und
markiert jede als geprüft ("habe ich mir angesehen"). Das ist der Grund, aus dem er die
YNAB-App überhaupt öffnet. Ein rein lesendes Kassenbuch kann diesen Ablauf nicht
übernehmen, und damit läuft v1 genau in das Scheiterkriterium der Vision: "Kassenbuch
gebaut, aber niemand schaut hinein." Roman zu einer reinen Lesesicht: "Nur reingucken,
dafür würde ich es wahrscheinlich gar nicht öffnen."

Zugleich ist der Prüfvermerk die **kleinste** denkbare schreibende Fähigkeit:

- Er trägt **ein** Feld (vermutlich YNABs `approved`), kein Objekt mit Invarianten wie
  Splits.
- Er ist **idempotent** und ohne Wertkonflikt: zweimal geprüft ist geprüft.
- Er hat **keine Budget-Wirkung**. Ein falscher Prüfvermerk verfälscht keine Zahl, anders
  als eine falsche Kategorie oder ein kaputter Split.
- Die **Abweichungs-Frage ist trivial**: wenn YNAB den Vermerk anders zeigt als BB,
  gewinnt der jüngere Stand, ohne dass jemand etwas verliert.

Kategorie, Split und Lena-Zuweisung sind das nicht. Sie brauchen einen allgemeinen
Rückschreibe-Weg (Update bestehender Buchungen mit Splits und Payee-Auflösung) und die
Abweichungs-Sicht, weil ein Konflikt dort echten Informationsverlust bedeutet.

## Decision
**Der Prüfvermerk wandert vor v2 nach BB. Sonst nichts.**

1. BB darf den Prüfvermerk setzen und zurücknehmen und schreibt ihn nach YNAB zurück. Der
   dafür nötige Rückschreibe-Weg wird **auf dieses eine Feld zugeschnitten**, nicht als
   allgemeiner Update-Weg gebaut.
2. Kategorie ändern, Split festlegen oder ändern und die Lena-Zuweisung bleiben **v2**.
   Sie kommen zusammen mit dem allgemeinen Rückschreibe-Weg und der Abweichungs-Sicht.
3. Bei einem Konflikt im Prüfvermerk gewinnt der jüngere Stand, ohne Abweichungs-Sicht.
   Das ist die Ausnahme, die dieses Feld sich leisten kann; für jedes andere Feld gilt
   sie nicht.
4. Die Vision wird entsprechend angepasst: v1 bekommt den Prüfvermerk als benannte
   Ausnahme, v2 bleibt inhaltlich unverändert.

## Consequences
**Gut:**
- v1 deckt Romans Handy-Hauptfall ab und wird damit im Alltag benutzt statt nur betrachtet.
  Das ist die einzige Art, das Scheiterkriterium wirklich zu prüfen.
- Der Rückschreibe-Weg entsteht an einem Feld, an dem ein Fehler nichts kostet. BB sammelt
  Erfahrung mit dem Schreiben nach YNAB, bevor es um Kategorien und Splits geht.
- Die Grenze bleibt scharf: genau ein Feld, mit einer benannten Begründung, warum es die
  Ausnahme ist.

**Schlecht oder offen:**
- v1 ist nicht mehr rein lesend. Die Einteilung verliert ihre Einfachheit und braucht
  diese ADR als Begründung.
- Ein zugeschnittener Rückschreibe-Weg wird beim Bau des allgemeinen (v2) vermutlich
  umgebaut. Das ist bewusst in Kauf genommen: ein allgemeiner Weg jetzt wäre v2 durch die
  Hintertür.
- Ob "geprüft" technisch `approved` ist, ist noch nicht festgezurrt. `cleared` trägt für
  Roman keine Information, weil importierte Buchungen immer schon bei der Bank sind. Die
  Festlegung gehört in `ledger-kdnnz` (lesend) und die schreibende Scheibe.

## Alternatives considered
**Alles lesend lassen, wie die Vision es sagt.** Sauber, aber v1 wäre eine Sicht, die
Roman im Alltag nicht öffnet. Das Scheiterkriterium der Vision wäre damit nicht abgewendet,
sondern eingebaut.

**Alle schreibenden Abläufe vorziehen.** Das ist v2, nur früher und ohne die
Abweichungs-Sicht, die v2 erst verantwortbar macht. Eine falsch zurückgeschriebene
Kategorie oder ein kaputter Split verfälscht Zahlen, auf die sich das Budget stützt.

**Den Prüfvermerk nur lokal in BB führen, ohne Rückschreiben.** Dann wüsste YNAB nichts
davon, und Roman müsste weiter in der YNAB-App prüfen. Zwei Orte mit zwei Wahrheiten für
dasselbe Häkchen, genau das, wogegen der Spiegel gebaut wurde.
