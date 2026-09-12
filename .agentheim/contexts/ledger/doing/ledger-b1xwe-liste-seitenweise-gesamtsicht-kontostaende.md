---
id: ledger-b1xwe
title: "Kassenbuch-Liste neu: seitenweise laden, Gesamtsicht über alle Konten, Kontostände-Leiste"
status: doing
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
- [ ] Eine neue Lesefunktion liefert Buchungen seitenweise: Parameter Konto (optional,
      leer bedeutet alle Konten), Seitenzeiger und Seitengröße; Rückgabe sind die
      Buchungen der Seite, die Gesamtzahl der Treffer und die Summe über den gesamten
      Treffer. Die Summe wird in F# über exakten Dezimal-Text gebildet, nicht per
      SQL-`SUM` (ADR 0017). Test: eine Seite aus einer mehrseitigen Menge trägt die
      Gesamtsumme, nicht die Seitensumme.
- [ ] Eine Lesefunktion liefert alle Konten mit ihrem Kontostand in einem Aufruf; der
      Stand rechnet über die nicht gelöschten Buchungen und ist unabhängig davon, welche
      Seite der Liste geladen ist. Test: Kontostand bleibt gleich, egal wie viele Seiten
      geladen sind.
- [ ] Die Kassenbuch-Seite zeigt oben die Kontostände: am Handy eine waagerecht
      scrollbare Leiste, am PC eine linke Spalte; "Alle Konten" ist in beiden der erste
      Eintrag und trägt die Summe aller Konten. Das aktive Konto ist hervorgehoben.
- [ ] Ein Klick oder Tipp auf "Alle Konten" zeigt die Buchungen aller Konten, neueste
      zuerst; jede Zeile trägt ihr Konto (Kürzel am Handy, eigene Spalte am PC). Ein Klick
      auf ein Konto zeigt nur dessen Buchungen, ohne Konto-Angabe je Zeile.
- [ ] Die Liste lädt beim Scrollen nach, 100 Buchungen je Seite. Während des Nachladens
      steht ein Skeleton am Listenende; die Scroll-Position springt nicht.
- [ ] Am Handy gruppiert die Liste nach Datum; am PC ist es eine Tabelle mit Sticky-Kopf.
      Beide nutzen ausschließlich Komponenten und Tokens des Design-Systems
      (Styleguide-Gate `design-system-001`).
- [ ] Herkunfts-Badge und das Kennzeichen "in YNAB gelöscht" bleiben wie bisher; das
      bestehende Historie-Sheet funktioniert unverändert weiter.
- [ ] `dotnet build`, `dotnet test` und `npm run build` sind grün; die neuen
      Lesefunktionen haben Tests, die Seitenlogik und Summenbildung abdecken.
- [ ] Tests schreiben nie in die Produktionsdatenbank (In-Memory oder temporäre DB).

## Notes
Geschnitten in `ledger-s8m3w` am 2026-09-12, verfeinert am selben Tag. Konzept und
Begründung: `ledger/README.md`, Abschnitt "Oberfläche". Brief mit Schrittfolge und
erfundenen Beispieldaten: `docs/ux/kassenbuch/briefs.md`, Ablauf 6.

**Bei der Verfeinerung entschieden** (Annahmen von Claude, Roman kann beim Durchspielen
korrigieren):
- **"Alle Konten" ist ein eigener Eintrag** in der Kontostände-Leiste, kein Schalter. Es
  ist damit sichtbar, dass es die Summe aller Konten trägt, und der Wechsel ist eine
  Geste wie jeder Konto-Wechsel.
- **Überträge erscheinen in der Gesamtsicht mit beiden Seiten.** Das ist ehrlich: es sind
  zwei Buchungen. Dass sie sich in der Summe aufheben, ist richtig und kein Fehler. Eine
  Zusammenfassung beider Seiten zu einer Bewegung ist v2 (`ledger-t7v2q`).
- **Nachladen per Beobachter am Listenende**, nicht per Button. Ein Button wäre ein
  Klick je hundert Buchungen; beim Durchsehen am Handy ist das zu viel.

**Bleibt offen:** Roman hat den YNAB-Referenzweg für Ablauf 6 (wie er "All Accounts" und
die Konto-Sicht heute nutzt, was ihm an den Kontoständen fehlt, wie er mit dem ETF-Konto
umgeht) noch nicht beschrieben. Das blockiert diese Scheibe nicht, wäre aber beim
Durchspielen der Maßstab.
