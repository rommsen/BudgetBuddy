---
id: 0016
title: Kontenmodell (Art x Verknüpfung), Bewertung als Buchung, Bar/Quick-Add und ETF-Konto in v1
scope: ledger
status: accepted
date: 2026-09-06
supersedes: []
superseded_by: []
related_tasks: [ledger-zm0zx]
related_research: []
---

# ADR 0016: Kontenmodell, Bewertung als Buchung, Bar/Quick-Add und ETF-Konto in v1

## Context
Das Kassenbuch soll alle Konten zeigen: Comdirect, Bar, ING-Konten (alle über den YNAB-
Spiegel) und das Wertpapierkonto mit Budget-Geld (Geldmarkt-ETF, ING-Depot), das YNAB
nicht automatisch bewerten kann (ADR 0012). Zwei offene Fragen aus der Vision waren zu
klären: ob Bar und Quick-Add ein Konto oder zwei sind, und wie der ETF in v1 geführt wird.
Depots als Vermögen sind kein Konto im Kassenbuch (künftiger Context `wealth`).

Für die zweite Frage wurde geprüft, was YNAB bei einer Saldo-Anpassung (Reconciliation
Balance Adjustment) auf einem Budget-Konto tatsächlich tut. YNAB-Support bestätigt: die
Anpassung fließt sowohl in den Kontostand als auch in Ready to Assign
(https://support.ynab.com/en_us/balance-adjustments-a-guide-rko4OwILs,
https://support.ynab.com/en_us/reconciling-accounts-a-guide-BJFE3fHys). Das ändert die
Entscheidung gegenüber dem ursprünglichen Architekten-Entwurf: ein manuelles YNAB-Konto
reicht für v1, ein BB-eigenes Konto ist nicht nötig.

## Decision
### Bar- und Quick-Add-Konto: eins
Es gibt genau ein Konto "Bar" im Kassenbuch. Quick Add ist kein Konto, sondern eine
**Herkunft** an der Buchung (Eingabeweg). In YNAB existiert dafür ein Konto Bargeld, auf
das `ynab_quickadd_account_id` (ADR 0004) zeigt. Die Vision-Zeile "heute in YNAB zwei
Konten" war ein Irrtum aus dem Brainstorm und wird korrigiert.

### ETF in v1: manuelles YNAB-Konto, kein BB-eigenes Konto
Roman legt in YNAB ein manuelles On-Budget-Konto "Geldmarkt-ETF" mit dem heutigen Wert
an (kein Bankanbindung nötig) und gleicht einmal im Monat auf Stückzahl mal Kurs ab. YNAB
erzeugt daraus eine Reconciliation Balance Adjustment, die Kontostand **und** Ready to
Assign anpasst (verifiziert, Quellen oben). Damit ist das Budget-Loch aus ADR 0012 ab dem
Folgemonat geschlossen, ohne Code und ohne BB-eigenes Konto, und YNABs Ready to Assign
(die Zahl, mit der Roman bis nach v2 budgetiert) bleibt richtig. Der ursprüngliche
Architekten-Entwurf (BB-eigenes ETF-Konto ab v1) hätte diese Zahl bis nach v2 falsch
gelassen, sein eigener "Negative"-Punkt.

Konsequenz: der ETF ist im Kassenbuch ein **gespiegeltes Konto wie jedes andere**, keine
Sonderbehandlung beim Spiegel-Import.

### Kontenmodell: Art x Verknüpfung
**Konto** trägt eine **Art** (Bankkonto | Bar | Wertpapierkonto mit Budget-Geld) und eine
**Verknüpfung** (YNAB-gespiegelt mit YnabAccountId | BB-eigen). Beide Achsen sind
orthogonal und bleiben Teil des Modells, auch wenn "BB-eigen" in v1 keine Instanz hat
(die einzige heute absehbare BB-eigene Kandidatin, der ETF, ist gespiegelt, siehe oben).
Ein **Wertpapierkonto** hält Papier (ISIN, Name) und Stückzahl; der Kontostand ist die
Summe seiner Buchungen, wie bei jedem Konto.

### Bewertung als spätere BB-geführte Fähigkeit
Bewertung (Stückzahl mal Kurs) ist in v1 **nicht** Sache von BB: der Abgleich läuft in
YNAB von Hand, wie oben beschrieben. Nach dem Muster aus ADR 0012 (Fähigkeiten wandern
einzeln von YNAB-geführt zu BB-geführt) ist eine spätere, BB-geführte Bewertung
**vorgesehen, nicht v1**: BB rechnet Stückzahl mal Kurs, schreibt dafür eine Buchung der
Art "Bewertungsänderung" ins Kassenbuch (Betrag = neuer Wert minus alter Wert, Herkunft:
Bewertung, mit Kurs, Datum, Quelle) analog zur Historie-Entscheidung aus ADR 0013
(ledger-fsz0a: `ValuationRecorded` als Ereignis im Kassenbuch), und pusht die Anpassung
nach YNAB. Damit bleibt die Regel "Kontostand = Summe der Buchungen" auch für Bewertung
gültig, ohne Sonderrechnung an der Anzeige.

Voraussetzung dieser späteren Fähigkeit ist eine **Kursquelle**: eine Schnittstelle mit
zwei Implementierungen, Eingabe von Hand (ein Feld pro Monat) und automatischer Abruf
(Kandidaten wie kostenfreie Kurs-APIs per ISIN sind zu verifizieren, Scraping ist
ausgeschlossen; fällt der Abruf aus, bleibt der letzte Kurs stehen und wird als veraltet
markiert). Die konkrete Quelle ist Gegenstand eines Folge-Spikes (Research), nicht dieser
Entscheidung.

Solange Bewertung nicht BB-geführt ist, ist Kurs-Eingabe von Hand **in YNAB** (Abgleich)
der v1-Boden.

### BB-eigene Konten allgemein
Ein BB-eigenes Konto wird nie nach YNAB gepusht; seine Buchungen tragen keine ImportId.
Diese Regel bleibt für den Fall bestehen, dass künftig eine BB-eigene Kontoinstanz entsteht.

## Consequences
### Positive
- Das ETF-Loch schließt sich in v1 ohne Code und ohne externe Abhängigkeit; YNABs Ready
  to Assign bleibt korrekt, solange Budgetieren in YNAB stattfindet.
- Ein Konto pro physischem/logischem Ort (Bar) statt zwei; die Herkunft Quick Add bleibt
  an der Buchung sichtbar, ohne ein zusätzliches Konto zu erzeugen.
- Das Kontomodell (Art x Verknüpfung) trägt eine spätere BB-geführte Bewertung, ohne dass
  v1 dafür etwas bauen muss.

### Negative
- Solange Bewertung YNAB-geführt bleibt, ist sie manuell: Roman muss den Abgleich einmal
  im Monat selbst anstoßen, sonst drift der Kontostand.
- Die spätere BB-geführte Bewertung hängt an einer Kursquelle, die noch nicht existiert;
  bis dahin bleibt ein Aufsatz offen.

### Neutral
- "BB-eigen" ist modelliert, hat aber in v1 keine Instanz. Das ändert sich erst, wenn eine
  Fähigkeit ein Konto braucht, das YNAB nicht abbilden kann.

## Alternatives considered
- **Bar und Quick-Add als zwei Konten**: abgelehnt, Quick Add ist ein Eingabeweg, kein
  Ort, an dem Geld liegt; zwei Konten für einen Ort hätten Bar-Bestand künstlich gespalten.
- **ETF als Tracking-Konto in YNAB und nur Anzeige in BB**: abgelehnt, Tracking-Konten
  zählen nicht ins Budget; das Loch bliebe.
- **Bewertung als berechneter Wert ohne Buchung**: abgelehnt, bricht die Regel "Kontostand
  = Summe der Buchungen" und die Historie.
- **Automatische Kursquelle als v1-Pflicht**: abgelehnt, keine Quelle ist ohne Prüfung
  robust; in v1 läuft der Abgleich ohnehin in YNAB, nicht in BB.
- **BB-eigenes ETF-Konto ab v1 (Architekten-Entwurf)**: abgelehnt, ein BB-eigenes Konto
  wird nie nach YNAB gepusht, also hätte YNABs Ready to Assign das ETF-Vermögen bis nach
  v2 (Budgetieren in BB) weiter falsch angezeigt, obwohl ein manuelles YNAB-Konto dasselbe
  Ziel (Budget-Loch schließen) ohne diesen Nachteil und ohne Code erreicht.

## References
- ADR 0012 (`0012-budgetbuddy-leads-ynab-projects.md`)
- ADR 0004 (`0004-manual-entries-no-importid-own-account.md`)
- ADR 0013 (ledger-fsz0a, Historie-Technik, `ValuationRecorded`)
- YNAB Support: https://support.ynab.com/en_us/balance-adjustments-a-guide-rko4OwILs
- YNAB Support: https://support.ynab.com/en_us/reconciling-accounts-a-guide-BJFE3fHys
- Task `ledger-zm0zx`
