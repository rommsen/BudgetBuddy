# Vision: BudgetBuddy

## Purpose
BudgetBuddy ist ein **YNAB-Companion**: ein persönliches Tool, das die tägliche
Bank→YNAB-Arbeit erledigt. Es zieht Transaktionen von der Bank (Comdirect), ordnet
ihnen regelbasiert automatisch YNAB-Kategorien zu, lässt Regeln im Vorbeigehen
entstehen, fängt Duplikate ab — und pusht das Ergebnis sauber nach YNAB. YNAB bleibt
die Source of Truth fürs Budgetieren; BudgetBuddy macht den Weg dorthin schnell und
fehlerfrei.

Gebaut von und für Roman als Daily Driver (Frau nutzt perspektivisch mit). Leitprinzip:
**fertiges Tool, kein Hobby-Projekt — was nicht real gebraucht wird, fliegt raus.**

## Users
- **Roman** — primärer Nutzer. Zieht regelmäßig Comdirect-Transaktionen rüber,
  kategorisiert, pflegt Regeln, pusht nach YNAB. Arbeitet am PC und mobil (Read +
  Kategorisieren).

Single-User-Tool. Kein Multi-Tenant, keine Rollen, keine Konflikt-Auflösung.

## The problem
YNABs eigener Import für deutsche Banken (Comdirect) und sein Payee-basiertes
Auto-Matching greifen nicht zuverlässig. Manuelles Kategorisieren in YNAB ist mühsam,
Duplikate beim wiederholten Import sind ein ständiges Ärgernis, und für
Amazon/PayPal-Sammelbuchungen fehlt der Kontext. BudgetBuddy schließt genau diese
Lücke zwischen Bank und YNAB — mit einer Regel-Engine, die tatsächlich greift, und
einem Duplikat-Schutz, der vor dem Push urteilt.

## What success looks like
- Ein **Sync**-Durchlauf zieht neue Comdirect-Transaktionen, kategorisiert den Großteil
  automatisch korrekt, und pusht nach YNAB — ohne dass Roman in YNAB nacharbeiten muss.
- Duplikate werden **vor** dem Push erkannt, nichts landet doppelt in YNAB.
- Eine neue Regel entsteht direkt aus dem Zuweisungs-Dialog, ohne Kontextwechsel.
- Amazon/PayPal-Buchungen tragen genug Kontext (Order-ID-Propagierung, External Links),
  um schnell entschieden zu werden.
- Roman fühlt den Workflow als **schneller und angenehmer** als YNAB-Web — sonst hätte
  das Tool keine Berechtigung.

## Non-goals
- **YNAB ersetzen.** BudgetBuddy baut *keine* eigene Source of Truth fürs Budget auf.
  Kein eigenes Budget-View, keine Allocations, keine Goals, keine Move-Money-Logik.
  (Diese Reise ist als separate, *nicht aktivierte* Idee dokumentiert in
  `docs/idea-ynab-replacement.md` — siehe ADR 0001.)
- ~~**Manuelle Transaction-Eingabe.**~~ *Aufgehoben per ADR 0003 (2026-06-12):* als
  **Quick Add** umgesetzt — ein enthaltenes Phase-0-Experiment. Bucht direkt nach YNAB
  (eigenes Quick-Add-Konto, keine ImportId, kein lokaler Store); YNAB bleibt Source of
  Truth. Phase 1+ der Ersatz-Reise bleibt Non-Goal.
- **Cleared-Setting / YNAB-Verhalten nachbauen.** Der aktuelle Workflow mit YNAB ist gut
  so; BudgetBuddy ersetzt nicht, wie YNAB Transaktionen verbucht.
- **Multi-User / Konflikt-Modell.** Single-User reicht.
- **Spekulative Reports.** Erst bauen, wenn real vermisst.

### Was *innerhalb* der Companion-Grenze bleibt
Die Linie ist sauber: **In-Scope = ein YNAB-Konzept bequemer aus BB heraus bedienbar
machen. Non-Goal = eigene Wahrheit aufbauen oder YNABs Verhalten ersetzen.**
- **Split Transaction UI** — In Scope (Backend existiert, UI fehlt).
- **Transfer-Payees** ("Transfer to/from" als Payee, z.B. Barabhebung) — In Scope, hohe Prio.
- **ING als zweite Datenquelle** — In Scope, niedrige Prio.
- **Quick Add** (manuelle Eingabe → YNAB, Bar-Konto) — ✅ umgesetzt 2026-06-11/12 (ADR 0003/0004).

## Ubiquitous language (seed)
Die Sprache stammt direkt aus `src/Shared/Domain.fs`. Verbindliche Definitionen leben
in den jeweiligen Bounded-Context-READMEs; hier der projektweite Kern.

- **Sync** — ein kompletter Durchlauf Bank→YNAB. Festgehalten als **SyncSession** mit
  Lifecycle `AwaitingBankAuth → AwaitingTan → FetchingTransactions → ReviewingTransactions
  → ImportingToYnab → Completed | Failed`.
- **BankTransaction** — eine roh von der Bank geholte Buchung (Comdirect-Herkunft).
- **SyncTransaction** — eine BankTransaction *im Durchlauf*, die ihren Verarbeitungs-Zustand
  mitträgt: `TransactionStatus` (`Pending → AutoCategorized | ManualCategorized |
  NeedsAttention | Skipped → Imported`), Kategorie, gematchte Regel, Splits, und **zwei
  getrennte** Duplikat-/Import-Achsen (siehe unten).
- **Rule** — Auto-Kategorisierungs-Regel: Pattern + **PatternType** (`Contains | Exact |
  Regex`) + **TargetField** (`Combined | Memo | Payee`) + Priority + optionaler PayeeOverride.
- **DuplicateStatus** — BudgetBuddys *Vor-Import*-Urteil über Duplikate
  (`NotDuplicate | PossibleDuplicate | ConfirmedDuplicate`). **Verschieden von** ↓
- **YnabImportStatus** — was *nach* dem Push passierte
  (`NotAttempted | YnabImported | RejectedByYnab`). YNABs eigenes Import-ID-Dedup lebt hier.
- **ImportId** — BudgetBuddys Dedup-Schlüssel, aus der TransactionId erzeugt, den YNAB
  nutzt, um Wiederholungen zu erkennen. Die Brücke zwischen beiden Duplikat-Welten.
- **Order-ID-Matching** — Amazon-Order-ID-Propagierung: eine kategorisierte Transaktion
  vererbt ihre Kategorie an andere mit gleicher Order-ID. **NeedsAttention** markiert
  Amazon/PayPal-Sammelfälle.

## Open questions
- Soll **ING** als zweite Quelle einen eigenen Adapter im `banking-import`-Context
  bekommen oder das Comdirect-Muster generalisieren? (verschoben bis Aktivierung)
- ~~Lohnt sich später ein eigener **`design-system`**-Bounded-Context?~~ *Aufgelöst
  2026-06-13:* `design-system`-BC angelegt, um den bestehenden Code (`src/Client/DesignSystem/`)
  **retroaktiv** zu einem reviewbaren Styleguide zu kodifizieren (`design-system-001`, das
  UI-Gate) und den View-Code darauf zu konsolidieren (`design-system-002`).
- ADR-Backfill der bestehenden Architektur (Stack/Persistence/Transport/Deployment) —
  optional, siehe `infrastructure`-Context.
