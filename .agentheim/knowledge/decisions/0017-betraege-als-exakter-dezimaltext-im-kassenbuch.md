---
id: 0017
title: Beträge im Kassenbuch als exakter Dezimal-Text, Kontostand in F# summiert
scope: ledger
status: accepted
date: 2026-09-06
supersedes: []
superseded_by: []
related_tasks: [ledger-e3k7p]
related_adrs: [0013, 0016]
related_research: []
---

# ADR 0017: Beträge im Kassenbuch als exakter Dezimal-Text, Kontostand in F# summiert

## Context
ADR 0013 legt das Ereignis-Log und das Zustandsmodell fest und verbietet
SQLite-Spezialitäten im Schema, sagt aber nichts über die Spaltenform eines Betrags. Die
bestehende Tabelle `sync_transactions` speichert `amount REAL`. Für eine Sync-Session,
die nach dem Push ihren Zweck erfüllt hat, ist das folgenlos. Das Kassenbuch ist anders:
es ist dauerhaft, sein Zweck ist Auditierbarkeit ("jede Zahl lässt sich bis zum Ursprung
zurückverfolgen"), und der Kontostand ist laut BC-README die Summe aller Buchungen eines
Kontos. Eine Summe über hunderte `REAL`-Werte liefert Ergebnisse wie `123.45000000000002`
und weicht je nach Summierungsreihenfolge ab. `Money.Amount` ist im Shared-Modell ein
`decimal`, also exakt; die Datenbank wäre die einzige Stelle, an der die Genauigkeit
verloren geht.

Zusätzlich soll ein Wertpapierkonto später Bewertungs-Buchungen führen (ADR 0016), deren
Beträge mehr als zwei Nachkommastellen haben können. Eine feste Minor-Unit-Annahme
(Cent oder YNAB-Milliunits als `INTEGER`) würde diese Stellen abschneiden.

## Decision
1. **Beträge liegen in `ledger_entries.amount` als `TEXT`**: die invariante
   Dezimal-Darstellung des `decimal` (`d.ToString(CultureInfo.InvariantCulture)`),
   zurückgelesen mit `Decimal.Parse(..., NumberStyles.Number, InvariantCulture)`. Die
   Währung steht wie bisher in einer eigenen Spalte. In den Ereignis-Payloads gilt
   dasselbe: Thoth kodiert `decimal` als Zeichenkette, der Roundtrip ist verlustfrei.
2. **Der Kontostand wird in F# über geladene Buchungen summiert**, nicht per SQL-`SUM`.
   Ein `CAST(amount AS REAL)` in einer Query würde die Genauigkeit genau dort wieder
   aufgeben, wo sie zählt, und ist damit ausgeschlossen.
3. **Bestehende Tabellen bleiben unangetastet.** `sync_transactions.amount REAL` wird
   nicht angefasst; die Entscheidung gilt für die Kassenbuch-Tabellen.
4. Auch Zeitpunkte liegen als ISO-8601-Roundtrip-Text (`"o"` plus `RoundtripKind`), in
   Payload wie Zustandstabelle identisch. Der Grund ist derselbe: Log und Zustand müssen
   tickgenau dasselbe ergeben, sonst schlägt der Replay-Vergleich aus ADR 0013 Punkt 6
   aus einem Grund fehl, der nichts mit dem Modell zu tun hat.

## Consequences
### Positive
- Replay und Zustandstabelle vergleichen exakt gleiche Werte; der Pflicht-Replay-Test aus
  ADR 0013 misst das Modell, nicht die Rundung.
- Kein Genauigkeitsverlust über beliebig viele Nachkommastellen, also auch für spätere
  Bewertungs-Buchungen (ADR 0016) tragfähig.
- `TEXT` ist ein gewöhnlicher SQL-Typ, keine SQLite-Eigenheit (ADR 0013, Punkt 8): ein
  späterer Wechsel auf Postgres bleibt billig.

### Negative
- Kein `SUM(amount)` in SQL. Der Kontostand kostet ein Laden der Buchungen eines Kontos.
  Bei Single-User-Volumen (Größenordnung Zehntausende Buchungen) ist das folgenlos; wird
  es messbar, ist die Antwort eine gepflegte Saldo-Spalte oder ein Wechsel auf
  `NUMERIC` in Postgres, nicht die Rückkehr zu `REAL`.
- Sortieren oder Vergleichen nach Betrag in SQL geht nicht direkt (Textsortierung ist
  keine Zahlensortierung). Das Kassenbuch sortiert nach Datum, der Fall tritt heute nicht
  auf; wenn er auftritt, wird in F# sortiert.
- Zwei unterschiedliche Betrags-Darstellungen im Repo (`REAL` in `sync_transactions`,
  `TEXT` im Kassenbuch), bis die Sync-Tabellen irgendwann verschwinden.

### Neutral
- Der Unterschied ist für Aufrufer unsichtbar: die Persistence-Schicht gibt `Money` mit
  `decimal` zurück, wie überall sonst.

## Alternatives considered
- **`REAL` wie `sync_transactions`**: abgelehnt, Gleitkomma-Drift in einem auditierbaren
  Kassenbuch. Der einzige Vorteil (SQL-`SUM`) wiegt das nicht auf.
- **`INTEGER` in Cent oder YNAB-Milliunits**: exakt und summierbar, aber bäckt eine feste
  Nachkommastellenzahl ins Schema. Neu zu bewerten, falls Performance je zum Thema wird.
