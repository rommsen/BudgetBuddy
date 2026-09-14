# categorization

## Purpose
Ordnet geholten Transaktionen automatisch YNAB-Kategorien zu — über eine Regel-Engine,
die im Vorbeigehen wächst — und propagiert Amazon-Order-IDs über Buchungen hinweg, um
Sammelfälle schneller entscheidbar zu machen.

## Classification
**core** — das regelbasierte Matching ist BudgetBuddys direkte Antwort auf YNABs
schwaches Payee-basiertes Auto-Matching.

## Actors
- **Roman** — kategorisiert manuell, wo keine Regel greift, und erzeugt Regeln direkt aus
  dem Zuweisungs-Dialog.
- **Regel-Engine** — wendet Regeln nach Priorität an.

## Ubiquitous language
- **Rule** — Auto-Kategorisierungs-Regel: Pattern, **PatternType**, **TargetField**,
  CategoryId, Priority, optionaler **PayeeOverride**, Enabled.
- **PatternType** — wie das Pattern matcht: `Contains | Exact | Regex`.
- **TargetField** — worauf das Pattern matcht: `Combined | Memo | Payee`.
- **Priority** — Reihenfolge der Regel-Anwendung; die erste passende Regel gewinnt.
- **PayeeOverride** — überschreibt den an YNAB gemeldeten Payee unabhängig von der Kategorie.
- **AutoCategorized** — TransactionStatus, wenn eine Regel automatisch gegriffen hat
  (vs. **ManualCategorized** für Romans Hand-Zuordnung).
- **Order-ID-Matching** — eine kategorisierte Transaktion vererbt ihre Kategorie an andere
  mit gleicher Amazon-Order-ID (`SuggestedByOrderId`, `OrderIdSuggestion`).
- **NeedsAttention** — TransactionStatus für Sammel-/Sonderfälle (Amazon, PayPal), die
  Romans Blick brauchen.
- **ExternalLink** — Label+Url, z.B. zur Amazon-Bestellung, als Entscheidungshilfe.
- **Review-Spur (`ReviewSteps`)**: die geordnete Liste von `ReviewStep`s einer
  SyncTransaction, ältester Schritt zuerst. Jeder Schritt ist ein vollständiger
  Schnappschuss (Kategorie, Splits, PayeeOverride) mit Zeitpunkt `At` und Herkunft `By`
  (`ReviewActor`: `ByRule` mit Regel-Id/-Name, `ByUser`, oder `ByOrderIdMatch` mit der
  Amazon-Order-ID). Eine Wiederholung mit unverändertem Stand erzeugt keinen neuen
  Eintrag. Trägt fest, wie eine Buchung durch Regel-Vorschlag und Romans Korrektur
  gelaufen ist, bevor die Kassenbuch-Buchung beim Push entsteht (ADR 0021);
  `ledger-qvt84` liest die Spur beim Push und übersetzt sie in
  `EntryCategorizedByRule`/`EntryRecategorized`-Ereignisse mit den Original-Zeitpunkten
  (categorization-gdrx8). Sieben Schreibpfade hängen einen Schritt an:
  - `RulesEngine.classifyTransactions`: `ByRule`, beim Regel-Treffer zur Abrufzeit.
  - `categorizeTransaction`, `bulkCategorize`, `splitTransaction`, `clearSplit`
    (`Server.Api`): `ByUser`, Romans manuelle Entscheidung.
  - `OrderIdMatcher.applySuggestions`: `ByOrderIdMatch orderId`, Kategorie aus der
    YNAB-Historie derselben Bestellung zur Abrufzeit; eigener Urheber, weil weder Regel
    noch Roman diese Buchung entschieden haben.
  - `OrderIdMatcher.propagateInSession`: `ByUser`, weil BB Romans Entscheidung auf der
    Quell-Buchung nur auf die Geschwister derselben Bestellung überträgt; die fachliche
    Entscheidung bleibt seine.
- **Available** — der aktuelle Budgetwert einer Kategorie (YNAB `balance`, das "Available"
  des laufenden Monats), als `Money option` (None = keine Zahl zeigen). Im Kategorie-Picker
  rechtsbündig und farbcodiert (grün >0 / rot <0 / neutral =0) hinter dem Namen, damit "passt
  diese Buchung hier rein" am Zuweisungspunkt entscheidbar ist. So frisch wie der letzte
  Kategorien-Load, kein Live-Refresh pro Zeile. **Ausnahme "Inflow: Ready to Assign":** deren
  eigenes `balance` ist ein interner Akkumulator (Fantasiewert); stattdessen zeigt sie den
  echten Ready-to-Assign des Monats (`to_be_budgeted`), via `applyReadyToAssign` (ADR 0011).

## Aggregates
- **Rule** — eine Regel; Invariante: gültiges Pattern (bei Regex kompilierbar), eindeutige
  Identität, sinnvolle Priority.
- **RuleSet** (implizit) — die nach Priority geordnete Menge angewandter Regeln.

## Key events
- *TransactionCategorized* — einer Transaktion wurde eine Kategorie zugeordnet (auto/manuell).
- *RuleCreated* / *RuleUpdated* — eine Regel entstand oder änderte sich.
- *OrderIdCategorySuggested* — eine Kategorie wurde via Order-ID vorgeschlagen.

## Key commands
- *ApplyRules* auf eine Menge BankTransactions.
- *CreateRule* (oft direkt aus dem Zuweisungs-Dialog).
- *PropagateOrderId* innerhalb einer SyncSession.

## Relationships with other contexts
- **Downstream of:** `banking-import` — erhält geholte BankTransactions.
- **Upstream of:** `ledger` (über die SyncSession, beim Push; ADR 0021). Die
  SyncTransaction führt ihre Review-Spur (`ReviewSteps`, categorization-gdrx8) bis zum
  Push mit; `ledger-qvt84` liest sie und übersetzt sie in eine Ereignisfolge im
  Kassenbuch, danach pusht `ynab-sync`.
- Voller Kontext-Überblick: `../../context-map.md`.

## Open questions
- Sollen Regeln versionierbar/auditierbar sein, oder reicht das aktuelle CRUD-Modell?
- Order-ID-Matching nur Amazon, oder verallgemeinerbar auf andere Merchant-IDs?
