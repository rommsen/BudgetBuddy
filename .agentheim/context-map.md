# Context map

BudgetBuddy zerfällt in drei fachliche Bounded Contexts entlang der Sync-Pipeline,
plus einen Infrastructure-Context für globale Technik-Belange. Die Pipeline läuft
linear: **banking-import → categorization → ynab-sync**, orchestriert durch eine
SyncSession.

## Contexts

### banking-import
- **Purpose:** Transaktionen aus Bankquellen holen (Comdirect via OAuth + Push-TAN;
  perspektivisch ING) und *vor* dem Push beurteilen, ob sie Duplikate sind.
- **Core language:** BankTransaction, Reference (Comdirect-Dedup-Schlüssel),
  DuplicateStatus, DuplicateDetectionDetails (Fuzzy-Match Amount/Date/Payee),
  Push-TAN, ComdirectAccount.
- **Classification:** core — der zuverlässige Comdirect-Import ist ein Hauptgrund für
  die Existenz des Tools.
- **Key actors:** Roman (löst Sync aus, bestätigt TAN), Comdirect (externe Quelle).

### categorization
- **Purpose:** Geholten Transaktionen automatisch YNAB-Kategorien zuordnen — über eine
  Regel-Engine, die im Vorbeigehen wächst — und Amazon-Order-IDs über Buchungen hinweg
  propagieren.
- **Core language:** Rule, PatternType (Contains/Exact/Regex), TargetField
  (Combined/Memo/Payee), Priority, PayeeOverride, AutoCategorized, Order-ID-Matching,
  NeedsAttention, ExternalLink.
- **Classification:** core — das regelbasierte Matching ist BBs Antwort auf YNABs
  schwaches Payee-Matching.
- **Key actors:** Roman (kategorisiert manuell, erstellt Regeln), Regel-Engine (wendet an).

### ynab-sync
- **Purpose:** Kategorisierte Transaktionen nach YNAB pushen, dabei YNABs eigenes
  Import-ID-Dedup respektieren, und YNAB-Strukturen (Budgets/Accounts/Categories/Payees)
  als Spiegel bereitstellen.
- **Core language:** ImportId, YnabImportStatus, YnabRejectionReason (DuplicateImportId/
  UnknownRejection), YnabTransaction, Splits/TransactionSplit, Payee/PayeeOverride,
  Transfer-Payee, YnabBudget/Account/Category.
- **Classification:** core — ohne sauberen Push ist das Tool wertlos. (Conformist
  gegenüber der YNAB-API.)
- **Key actors:** Roman, YNAB (externes Upstream/Downstream-System).

### infrastructure
- **Purpose:** Globally-true Technik-Belange — Stack (.NET/F#, Fable), Persistence
  (SQLite/Dapper, AES-Verschlüsselung), Transport (Fable.Remoting), SyncSession-
  Orchestrierung, Deployment (Docker/Tailscale/Portainer). BC-lokale Technik bleibt
  im jeweiligen Context.
- **Classification:** generic/supporting.
- **Key actors:** Roman (betreibt), Tailscale/Docker (Laufzeit).

### design-system
- **Purpose:** Frontend-Infrastruktur — visuelle Sprache (neon-on-dark), Token-Layer,
  das Komponenten-Inventar (`src/Client/DesignSystem/`, 20 Komponenten) und die projektweiten
  UI-Muster (Sheets/Click-Commit ADR 0005, Picker ADR 0004). Hält den **Styleguide** als
  reviewbares Gate für alle UI-Arbeit.
- **Core language:** Styleguide, Token, neon-on-dark, Farbsemantik, DS-Komponente, Muster,
  Drift.
- **Classification:** supporting — kein fachlicher Differenzierer, dient aber dem
  Vision-Ziel "angenehmer als YNAB-Web". First-class Frontend-Infra-BC (analog
  `infrastructure` fürs Backend).
- **Key actors:** Roman (reviewt das Gate, baut UI), alle fachlichen BCs (Konsumenten).

## Relationships

- **banking-import → categorization → ynab-sync:** Customer-supplier / Pipeline.
  Jeder Context ist Upstream des nächsten; die `SyncTransaction` wandert durch und
  sammelt Zustand ein (erst DuplicateStatus, dann Kategorie/Regel, dann YnabImportStatus).
- **ynab-sync → YNAB (extern):** **Conformist** — BB ordnet sich der YNAB-API und ihrem
  Datenmodell unter (ImportId-Dedup, Cleared-Verhalten, Transfer-Semantik). BB ersetzt
  YNABs Verhalten nicht, es bedient es bequemer.
- **banking-import → Comdirect (extern):** **Anticorruption layer** — die rohe
  Comdirect-JSON (`RawData`) wird in das saubere `BankTransaction`-Modell übersetzt.
- **Zwei Duplikat-Welten:** `banking-import` besitzt das *Vor-Import*-Urteil
  (DuplicateStatus); `ynab-sync` besitzt YNABs *eigenes* Dedup (ImportId →
  RejectedByYnab DuplicateImportId). Die **ImportId** ist der Shared-Kernel-Begriff,
  der beide verbindet.
- **infrastructure** ist generic supporting für alle drei: stellt SyncSession-Lifecycle,
  Persistence und Transport bereit, ohne fachliche Entscheidungen zu treffen.
- **design-system → alle UI-tragenden BCs:** Customer-Supplier. Liefert die UI-Bausteine
  (Tokens, Komponenten, Muster), gegen die deren View-Schichten bauen; der Styleguide
  (`design-system-001`) ist das **Gate** — künftige UI-Tasks hängen daran (Hard-Enforcement
  2026-06-13). Analog zu `infrastructure`, aber fürs Frontend.

## Open questions
- **ING** als zweite Quelle: eigener Adapter in `banking-import` oder Comdirect-Muster
  generalisieren? (verschoben)
- ~~Eigener **`design-system`**-Context?~~ *Aufgelöst 2026-06-13:* angelegt (s.o.) —
  kodifiziert das bestehende Design System retroaktiv zum Styleguide-Gate und konsolidiert
  den View-Code darauf.
