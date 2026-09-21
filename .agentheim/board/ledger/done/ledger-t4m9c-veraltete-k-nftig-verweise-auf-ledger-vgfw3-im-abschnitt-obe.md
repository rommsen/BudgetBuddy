---
id: ledger-t4m9c
title: "Veraltete 'künftig'-Verweise auf ledger-vgfw3 im ledger-README korrigieren (Oberfläche und Sichten)"
status: done
type: chore
context: ledger
created: 2026-09-19
completed:
depends_on: []
blocks: [ledger-f8q2x]
tags: [doku, kassenbuch, readme]
related_adrs: [0036]
related_research: []
prior_art: [ledger-vgfw3]
---

## Why
`ledger-vgfw3` (Filter-Chips und Summenzeile über der Kassenbuch-Liste) ist erledigt.
Der Abschnitt `## Oberfläche` im ledger-README spricht an zwei Stellen noch so, als
stehe das aus:
- "Der heutige Schalter ist eine schlichte Zeile über der Liste, kein Filter-Chip (die
  Filter-Chip-Leiste selbst ist weiter `ledger-vgfw3`)." — im Absatz
  `**Gelöschte Buchungen**`.
- Der Bullet "**Filter-Chips und Summenzeile über dem Treffer.**
  `getLedgerEntriesPage` liefert Trefferzahl und Summe bereits (ledger-b1xwe), aber die
  Seite zeigt heute weder Filter-Chips noch die Summenzeile an — das ist
  `ledger-vgfw3`." unter der Überschrift "Was das Konzept voraussetzt und heute nicht
  gibt (Scheiben-Material für `ledger-s8m3w`)".

Beide Stellen behaupten einen Zustand ("gibt es noch nicht", "ist offen"), der seit
`ledger-vgfw3` nicht mehr stimmt. Wer diese Stellen liest, ohne den Task-Status von
`ledger-vgfw3` zu kennen, bekommt ein falsches Bild vom aktuellen Stand der Oberfläche.

Eine dritte Stelle liegt im Abschnitt `## Sichten`, im Eintrag zu `getLedgerEntriesPage`:
- "Fundament für die künftigen Filter (`ledger-vgfw3`) und die Summenzeile über der Liste."

`ledger-ezrw0`s Verifier hatte empfohlen, diese dritte Stelle gleich beim Neu-Ausgeben des
Blocks mitzukorrigieren, weil sie ohnehin gerade von Hand neu geschrieben wurde. Der Worker
hat das korrekt nicht getan, weil es eine Erweiterung der Akzeptanzkriterien jener Aufgabe
gewesen wäre. **Roman hat am 2026-09-20 entschieden, dass sie hierher gehört**, also deckt
diese Aufgabe alle drei Stellen ab.

Zu beachten beim Umsetzen: die `## Sichten`-Stelle sitzt in einem mehrzeilig umbrochenen
Unterbullet auf Einrückung 2. Die `README_DELTA`-Grammatik erreicht nur Bullets auf Spalte 0
mit fettem Anfang, weshalb `ledger-ezrw0` auf das Eltern-Bullet `**Sichten**` ausweichen und
den gesamten 32-Zeilen-Block ersetzen musste — mit erhaltener Einrückung in `expected` UND
`body`, sonst wird der Block flachgeklopft. Siehe `ledger-ezrw0`s Outcome und den dortigen
Verifier-Befund aus Iteration 1; derselbe Weg ist hier nötig.

## What
Die beiden oben zitierten Stellen im Abschnitt `## Oberfläche` so korrigieren, dass sie
den erledigten Stand von `ledger-vgfw3` wiedergeben (Filter-Chips und Summenzeile über
der Liste existieren), statt ihn weiterhin als offen zu beschreiben. Der
"Scheiben-Material"-Bullet gehört dabei entweder ganz heraus (das Vorausgesetzte gibt es
jetzt) oder wird, falls ein Rest offen ist, präzisiert, was genau noch fehlt.

## Acceptance criteria
- [ ] Die Stelle im Absatz `**Gelöschte Buchungen**` nennt `ledger-vgfw3` nicht mehr als
      offen ("weiter `ledger-vgfw3`"), sondern spiegelt den erledigten Stand wider.
- [ ] Der Bullet "Filter-Chips und Summenzeile über dem Treffer" unter "Was das Konzept
      voraussetzt und heute nicht gibt" ist entfernt oder auf einen tatsächlich noch
      offenen Rest präzisiert — nicht länger pauschal als fehlend beschrieben.
- [ ] Der Satz "Fundament für die künftigen Filter (`ledger-vgfw3`)" im `## Sichten`-Eintrag
      zu `getLedgerEntriesPage` nennt `ledger-vgfw3` nicht mehr als künftig.
- [ ] Alle drei Korrekturen decken sich mit dem Stand von `ledger-vgfw3` in `done/`.
- [ ] Die Einrückung des `## Sichten`-Blocks ist unverändert: die sechs Unterbullets stehen
      weiterhin auf Einrückung 2, ihre Fortsetzungszeilen auf 4.
- [ ] Keine anderen Stellen im README werden angefasst.

## Notes
Erfasst am 2026-09-19 aus der Verifikation von `ledger-ezrw0`, Iteration 1. Reine
Doku-Aufgabe: am Code ändert sich nichts.
## Outcome

Alle drei veralteten Verweise auf `ledger-vgfw3` im ledger-README sind korrigiert. Zwei kamen
als `README_DELTA` vom Worker und sind über `applyReadmeDelta` angewandt worden (Disposition
beide Male `applied`), die dritte hat der Conductor von Hand gesetzt, weil die Grammatik sie
nicht erreicht.

**1. `## Sichten`-Eintrag zu `getLedgerEntriesPage`** (`replace` auf den Anker `Sichten`, ganzer
37-zeiliger Extent des Eltern-Bullets). Aus "Fundament für die künftigen Filter
(`ledger-vgfw3`) und die Summenzeile über der Liste." wurde "Grundlage für den serverseitigen
Such-Filter (`ledger-vgfw3`, erledigt) und die Summenzeile über der Liste."

**2. Bullet "Filter-Chips und Summenzeile über dem Treffer"** unter `## Oberfläche`:
**präzisiert statt entfernt**, und das ist die inhaltlich richtige Wahl, nicht nur die
technisch mögliche. Die Grammatik kennt keinen `remove`-Op, und `Prüfung 4` des Gates verlangt
bei `replace` eine unveränderte Spalte-0-Bullet-Zahl, eine Löschung ist also strukturell nicht
ausdrückbar. Entscheidend ist aber der Inhalt: `ledger-vgfw3` deckt laut eigenem `## Why` nur
die Server-Hälfte ab und trägt `blocks: [ledger-j8xq4]`; die Filterleiste, die Kategorienauswahl
mit Mehrfachauswahl, das Popover am PC und die Summenzeile sind `ledger-j8xq4`, und das liegt
weiterhin im Backlog. Das Bullet nennt jetzt beide Zustände getrennt: das serverseitige
Fundament ist da, die Anzeige ist offen, unter dem richtigen Task-Verweis.

**3. Absatz `**Gelöschte Buchungen**`: vom Conductor von Hand gesetzt.** Die Stelle ist per
`README_DELTA` nachweislich nicht erreichbar, und zwar strukturell, nicht wegen eines Fehlers im
Delta: sie ist reiner Fließtext ohne führendes `- `, und `lib/readme-delta.mjs` erfasst über
`BULLET_START_RE = /^- /` nur Zeilen mit `- ` als Bullet-Anfang. Der Verifier hat nachgerechnet,
dass im Abschnitt `## Oberfläche` das erste Spalte-0-Bullet erst nach diesem Absatz steht, ihn
also kein Bullet-Extent je überdecken kann, und die Gegenprobe gefahren: ein `replace` mit
diesem Anker ergibt `Prüfung 6 (Anker-Erreichbarkeit) verletzt`, und die Fallback-Disposition
hätte den Absatz am Abschnittsende ein zweites Mal angehängt, das Dokument also beschädigt.
ADR 0040 weist genau solche Stellen dem Conductor zu. Gesetzt wurde, in der vom Verifier
vorgeschlagenen geglätteten Fassung ohne verschachtelte Klammer: "Der heutige Schalter ist eine
schlichte Zeile über der Liste, kein Filter-Chip; die Filter-Chip-Leiste selbst ist weiter offen
(`ledger-j8xq4`)."

Gegenprobe nach allen drei Änderungen: `ledger-vgfw3` kommt im README nur noch zweimal vor, beide
Male korrekt (einmal als "erledigt", einmal als Beschreibung dessen, was es geliefert hat).

**Verifikation (Iteration 1, PASS).** Der Prüfgegenstand war hier kein Code-Diff, sondern das
Delta; der Worktree war leer, wie es für eine reine `.agentheim/`-Aufgabe sein muss. Der
Verifier hat drei Dinge selbst nachgemessen statt sie zu übernehmen:

- **Einrückungsprofil (Kriterium 5, die historische Falle).** `ledger-ezrw0` hatte genau diesen
  Block einmal flachgeklopft, während das Werkzeug `applied` meldete. Der Verifier hat das Delta
  in einer Kopie außerhalb des Worktrees angewandt und danach ausgezählt: ein Spalte-0-Bullet,
  sechs Unterbullets auf Einrückung 2, 28 Fortsetzungszeilen auf Einrückung 4, vorher wie
  nachher identisch.
- **Keine stille Drift (Kriterium 6).** Voll-Dokument-Diff zwischen Original und angewandter
  Kopie: genau zwei Hunks, vier entfernte und fünf eingefügte Zeilen. Im 37-Zeilen-Extent
  unterscheiden sich exakt zwei Zeilen (ein umbrochener Satz), alles andere zeichengleich, kein
  verlorener Backtick, keine eingeführte oder entfernte Transliteration.
- **Wahrheitsgehalt der neuen Formulierung (Kriterium 4).** Gegen den Code geprüft, nicht gegen
  die Behauptung: `LedgerEntryPageQuery` trägt alle fünf Filterkriterien, `buildFilterClause`
  wird von allen fünf Lesewegen gemeinsam benutzt, `TotalCount`/`TotalAmount` entstehen
  serverseitig. Und die Gegenaussage stimmt ebenfalls: ein Grep über
  `src/Client/Components/Ledger/*.fs` findet weder Filter-Chips noch Summenzeile, die Seite zeigt
  sie also wirklich nicht.

Anmerkung des Verifiers ohne Mangelcharakter: `TESTS_PASSING: yes` bei `TESTS_ADDED: 0` ist hier
inhaltsleer statt falsch, `n/a` wäre sauberer gewesen. Die tragende Aussage, dass das Gate grün
ist, steht im `TDD_SKIPPED` und stimmt (Exit 0, `Plugin-Version: 0.9.4`, keine verletzte
Prüfung).

Kein Code geändert, `dotnet test` daher nicht gefahren.
