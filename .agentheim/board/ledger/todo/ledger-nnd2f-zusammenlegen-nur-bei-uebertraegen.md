---
id: ledger-nnd2f
title: "Der Spiegel legt nur noch zusammen, wenn YNAB einen Comdirect-Import mit einem Übertrag abgeglichen hat; eine verschluckte Barbuchung bleibt im Kassenbuch stehen (Ledger/Domain.fs mergeObservedInYnab)"
status: todo
type: bug
context: ledger
created: 2026-11-13
completed:
depends_on: [ledger-nkwqb]
blocks: []
tags: [laufend, money, ynab-sync, server, tests]
related_adrs: [0015, 0093]
related_research: []
prior_art: [ledger-nkwqb]
---

## Why
Probelauf von ledger-nkwqb gegen eine Kopie der Produktion (2026-11-13, vor dem Deploy): Neben zwei ING-Überträgen
hätte der Spiegel auch eine Barbuchung von Bob aufgenommen. YNAB hatte sie mit einer von Hand erfassten Buchung gleichen
Payees und Betrags eine Woche früher abgeglichen, weil BB Barbuchungen mit `import_id` schickt und YNAB jede Buchung mit
`import_id` wie einen Bank-Import gegen von Hand erfasste Buchungen der letzten Tage abgleicht. Es waren zwei echte
Zahlungen. BB war vorher richtig (beide Buchungen), YNAB falsch (eine). `mergeObservedInYnab` folgt YNABs Abgleich blind und
hätte BB ebenso falsch gemacht. Roman hat den Abgleich in YNAB aufgehoben.

Roman (2026-11-13): „Für mich macht Buchungen zusammenlegen generell nur Sinn bei Überträgen. Das ist ja die einzige
Möglichkeit, wo das auftreten kann.“ Deshalb wird nicht deployt, bevor diese Task fertig ist.

## What
- `mergeObservedInYnab` legt nur zusammen, wenn zusätzlich zu den heutigen Bedingungen beides gilt:
  - Die bleibende Buchung ist in YNAB ein Übertrag: die Beobachtung O trägt ein Gegenkonto (`TransferAccountId` der
    Beobachtung gesetzt).
  - Die aufgenommene Buchung stammt aus dem Comdirect-Import (sie hat eine `comdirect`-Verknüpfung).
- In jedem anderen Fall (etwa Barbuchung von Bob, Quick Add, Neue Buchung, oder bleibende Buchung kein Übertrag) gibt es
  keine Zusammenlegung, nur eine Log-Zeile im Spiegel-Lauf. Die Buchung bleibt im Kassenbuch und zählt weiter. Das
  Kennzeichnen einer von YNAB verschluckten Buchung und das Verhindern des Abgleichs sind ledger-ay81j.
- Der Spiegel-Lauf überspringt `observe` nur noch für Beobachtungen, deren Buchung tatsächlich aufgenommen wird
  (heute: jedes Ziel eines Merge-Paars, Kann-Punkt 103 in ledger-k4n2s). Für eine verdeckte Buchung, die nicht aufgenommen
  wird, kommt im Vollabruf ohnehin keine Beobachtung (YNAB liefert sie nicht).
- Kein Datumsfenster: Comdirect und ING buchen einen Übertrag nicht immer am selben Tag; die Bedingung „Übertrag plus
  Comdirect-Import“ trägt die Unterscheidung.

## Acceptance criteria
- [ ] 1. Regressionstest, rot vor dem Fix (Kommentar nennt den Bug, Doktrin 8 und 9), als Handler-Test von `runMirror`
      mit erfundenen Daten: GIVEN eine von Hand in YNAB erfasste Barbuchung A (kein Übertrag) und eine Barbuchung E2 aus
      BB (Actor Bob oder Quick Add, `importid`, keine `comdirect`-Verknüpfung), gleicher Betrag, sieben Tage später,
      verknüpft mit B. WHEN ein Lauf A mit `matched_transaction_id = B` liefert. THEN kein Merge-Ereignis, E2 zählt im
      Kontostand, beide Buchungen stehen in der Liste.
- [ ] 2. Intakt-Szenario: der Regressionstest aus ledger-nkwqb (Übertrag A, Comdirect-Import E2) legt weiter zusammen.
- [ ] 3. Domain-Tests auf `mergeObservedInYnab`: Übertrag mit Comdirect-Import ergibt die zwei Ereignisse; Übertrag ohne
      `comdirect`-Verknüpfung ergibt `[]`; Comdirect-Import, aber bleibende kein Übertrag, ergibt `[]`.
- [ ] 4. Lauf-Test: Für eine Beobachtung, deren Buchung Ziel eines abgelehnten Merge-Paars ist, läuft `observe` normal
      (eine Änderung an ihr wird beobachtet).
- [ ] 5. README ledger (Bullet **In YNAB zusammengelegt**, Spiegel-Lauf) und Nachtrag zu ADR 0015 (Punkt 1 um die
      Bedingung ergänzt) als README_DELTA bzw. ADRS-Text gemeldet; README-Delta mit `check:readme-delta` geprüft.
- [ ] 6. `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Vor dem Deploy wiederholt der Conductor den Probelauf gegen eine frische Kopie der Produktion: erwartet zwei
  Zusammenlegungen (die ING-Überträge), keine Barbuchung.
- Der Kann-Punkt 103 in ledger-k4n2s ist mit Kriterium 4 erledigt.
