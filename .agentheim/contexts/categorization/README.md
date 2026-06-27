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
- **Available** — der aktuelle Budgetwert einer Kategorie (YNAB `balance`, das "Available"
  des laufenden Monats). Im Kategorie-Picker rechtsbündig und farbcodiert (grün >0 / rot <0 /
  neutral =0) hinter dem Namen, damit "passt diese Buchung hier rein" am Zuweisungspunkt
  entscheidbar ist. So frisch wie der letzte Kategorien-Load, kein Live-Refresh pro Zeile.

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
- **Upstream of:** `ynab-sync` — liefert kategorisierte SyncTransactions zum Push.
- Voller Kontext-Überblick: `../../context-map.md`.

## Open questions
- Sollen Regeln versionierbar/auditierbar sein, oder reicht das aktuelle CRUD-Modell?
- Order-ID-Matching nur Amazon, oder verallgemeinerbar auf andere Merchant-IDs?
