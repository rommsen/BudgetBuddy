---
id: 0019
title: Unkategorisierte Split-Zeile ist ein benanntes drittes Split-Ziel (ergänzt ADR 0006)
scope: ledger
status: accepted
date: 2026-09-06
supersedes: []
superseded_by: []
related_tasks: [ledger-m8r2v]
related_adrs: [0006, 0013, 0014, 0015]
related_research: []
---

# ADR 0019: Unkategorisierte Split-Zeile ist ein benanntes drittes Split-Ziel (ergänzt ADR 0006)

## Context
ADR 0006 hat `SplitTarget` als XOR modelliert: eine Split-Zeile ist *entweder* eine
Kategorie *oder* ein Übertrag, "nie beides und nie keins". Das war aus der **Schreib**-Sicht
richtig — BBs Split-Editor und `buildCashbackSplit` erzeugen nie etwas anderes.

Der Spiegel liest jetzt aber in die Gegenrichtung. In YNAB ist eine Subtransaction ohne
`category_id` **und** ohne `transfer_account_id` gültige Wirklichkeit: eine unkategorisierte
Split-Zeile. Der Fall "weder noch" war im Typ unrepräsentierbar, obwohl er in den Daten
vorkommt.

Die drei Auswege waren: laut scheitern (ein einziges solches Vorkommen im Budget blockiert
jeden Spiegel-Lauf, auch die Abnahme in `ledger-kt0wq`), die Zeile still weglassen (die
Split-Summe stimmt dann nicht mehr mit dem Buchungsbetrag überein — genau das, was der
Spiegel beweisen soll), oder den Fall benennen. Entschieden mit dem Tactical Modeler.

## Decision
- `SplitTarget` bekommt einen dritten Fall `Uncategorized` (`src/Shared/Domain.fs`). Das XOR
  aus ADR 0006 **bleibt**: "Kategorie *und* Übertrag" ist weiter unrepräsentierbar. Nur
  "weder noch" wandert von *unmöglich* zu *benannt*.
- `mkSplits` bleibt unverändert: eine unkategorisierte Zeile zählt in der Summe voll mit und
  wird nie weggelassen.
- Lesepfad: eine Subtransaction ohne Kategorie und ohne Übertrags-Ziel kommt als
  `Uncategorized` an; der Spiegel-Lauf bricht deswegen nicht ab.
- Schreibpfad: `YnabSubtransactionRequest` bekommt `UncategorizedSub (amount, memo)`, das als
  `{ amount, memo }` **ohne** `category_id` und **ohne** `payee_id` serialisiert wird —
  YNABs eigene Darstellung einer unkategorisierten Zeile, kein geratenes Payload. BBs eigene
  Entscheidungswege können den Fall nicht konstruieren; der Arm ist defensiv und durch einen
  JSON-Contract-Test festgenagelt.
- Ereignis-Log: der Split-Ziel-Codec (`Ledger.Events.Codec`) bekommt den Diskriminator
  `"uncategorized"`. Die Schema-Version von `EntryObservedInYnab` bleibt **1**: der Codec
  wird nur um einen Diskriminator erweitert, kein gespeicherter Wert ändert seine Bedeutung
  (ADR 0013, Punkt 7), und bis zu diesem Slice existiert ohnehin kein gespeichertes Ereignis
  mit Splits.

## Consequences
### Positive
- Der Spiegel bildet YNAB vollständig ab: die Split-Summe stimmt weiter mit dem
  Buchungsbetrag überein, auch bei unkategorisierten Zeilen.
- Ein normaler Datenfall wird nicht mehr zum Lauf-Fehler.

### Negative
- Jedes `match` auf `SplitTarget` braucht einen dritten Arm. Betroffen waren fünf Stellen
  (Push-Encoder, `resolveSubtransaction`, Ereignis-Codec, Spiegel-Decoder, das Label im
  Split-Sheet) plus zwei Test-Matches.

### Neutral
- Im Split-Sheet zeigt eine solche Zeile das Label "Ohne Kategorie". BB erzeugt sie nicht,
  gespiegelte Buchungen können sie aber tragen.

## Alternatives considered
- **Decoder scheitert laut:** verwandelt normale YNAB-Daten in einen Lauf-Fehler und
  blockiert die Abnahme auf dem echten Budget.
- **Zeile weglassen:** stiller Datenverlust; die Split-Summe passt nicht mehr zum
  Buchungsbetrag, und die Auditierbarkeit des Kassenbuchs ist dahin.
- **Kategorie mit `Guid.Empty` erfinden:** eine erfundene Fremd-Id, die irgendwann in einen
  Push zurückfließen kann. Genau das, was ADR 0006 mit "omit, never null" vermeiden wollte.
