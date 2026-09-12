---
id: ledger-b1xwe
title: "Kassenbuch-Liste neu: seitenweise laden, Gesamtsicht über alle Konten, Kontostände-Leiste"
status: backlog
type: feature
context: ledger
created: 2026-09-12
completed:
depends_on: [design-system-001]
blocks: [ledger-vgfw3, ledger-r1d2f, ledger-q1hw2, ledger-p3n8k, ledger-kdnnz]
tags: [frontend, backend, kassenbuch, scheibe]
related_adrs: [0013, 0017]
related_research: []
prior_art: [ledger-q5w9c, ledger-kt0wq]
---

## Why
Erste lesende Scheibe aus dem UX-Konzept (`ledger/README.md`, Abschnitt "Oberfläche";
abgeleitet in `ledger-s8m3w`). Sie ist das Fundament: ohne serverseitige, seitenweise
Abfrage gibt es weder Filter noch Summenzeile noch Gesamtsicht, und bei rund 14.000
Buchungen lädt die Seite heute alles eines Kontos auf einmal.

Zugleich erfüllt sie ein v1-Kriterium der Vision: "Buchungen pro Konto und über alle
Konten, wie YNABs All Accounts".

An dieser Scheibe spielt Roman erstmals an **echten Daten** durch, ob sich das Kassenbuch
wie eine Arbeitsfläche anfühlt. Der Prototyp-Durchgang aus `ledger-xbrht` ist bewusst
hierher verschoben.

## What
**Server.** Eine neue Sicht neben `getLedgerEntriesForAccount` (die bleibt, bis nichts
mehr auf sie zeigt): Buchungen seitenweise, wahlweise für ein Konto oder über alle Konten,
neueste zuerst. Rückgabe je Seite: die Buchungen, die Gesamtzahl der Treffer und die
Summe über den **ganzen** Treffer, nicht über die Seite. Beträge weiter als exakter
Dezimal-Text summiert, nie per SQL-`SUM` (ADR 0017).

Kontostände aller Konten in einem Aufruf, damit die Leiste nicht je Konto nachlädt.
Kontostand bleibt der Zustand über die nicht gelöschten Buchungen, er kommt nie aus der
geladenen Liste.

**Client.** Die Kassenbuch-Seite bekommt den Aufbau aus dem Konzept:
- Oben die **Kontostände**: am Handy eine waagerecht scrollbare Leiste mit "Alle Konten"
  zuerst, am PC eine linke Spalte mit "Alle Konten" zuoberst. Aktives Konto hervorgehoben.
- Darunter die **Liste**. Am Handy Datum als Gruppenkopf, Zeile mit Payee, Kategorie,
  Betrag, darunter klein Memo. Am PC eine Tabelle mit Sticky-Kopf.
- **Konto ist ein Filter**: der Wechsel zwischen Gesamtsicht und Konto behält die
  Scroll-Position nicht, aber alle sonstigen Zustände. In der Gesamtsicht trägt jede Zeile
  ihr Konto (Kürzel am Handy, Spalte am PC).
- **Nachladen beim Scrollen**, 100 Buchungen je Seite. Während des Nachladens ein
  Skeleton, kein Sprung.
- Herkunfts-Badge und Kennzeichen wie heute; das Historie-Sheet bleibt vorerst, wie es ist
  (Umbau in `ledger-q1hw2`).

**Nicht in dieser Scheibe:** Filter-Chips, Summenzeile über dem Treffer, Auswahl-Modus,
Detail-Umbau, Prüfvermerk, gelöschte ausblenden. Die haben eigene Tasks.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Geschnitten in `ledger-s8m3w` am 2026-09-12. Konzept und Begründung:
`ledger/README.md`, Abschnitt "Oberfläche". Brief mit Schrittfolge und erfundenen
Beispieldaten: `docs/ux/kassenbuch/briefs.md`, Ablauf 6.

Offen für die Verfeinerung:
- Ob "Alle Konten" ein eigener Eintrag in der Kontostände-Leiste ist oder ein Schalter.
- Wie die Gesamtsicht mit Überträgen umgeht (beide Seiten sichtbar, Summe wäre 0).
- Ob das Nachladen per Beobachter am Listenende oder per Button geschieht.
- Der YNAB-Referenzweg für Ablauf 6 fehlt noch in `briefs.md`, Roman beschreibt ihn bei
  der Verfeinerung.
