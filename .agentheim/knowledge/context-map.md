# Context map

BudgetBuddy zerfällt in fünf fachliche Bounded Contexts plus zwei Infrastruktur-Contexts.
Die Sync-Pipeline **banking-import → categorization → ynab-sync** bleibt bestehen; neu
(Revision 2026-09-06, ADR 0012) ist das **Kassenbuch** (`ledger`) als Mitte, in der jede
Buchung dauerhaft landet und ihre Historie behält. `ynab-sync` wird dadurch beidseitig:
es pusht weiterhin BBs Entscheidungen nach YNAB und spiegelt zusätzlich YNABs Zustand ins
Kassenbuch zurück. Seit der Revision 2026-10-25 (ADR 0063) kommt das **Budget**
(`budgeting`) dazu: es liest die Aktivität pro Kategorie aus dem Kassenbuch und schreibt
Zuweisungen über `ynab-sync` nach YNAB.

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

### ledger *(neu, 2026-09-06)*
- **Purpose:** BBs eigenes, dauerhaftes **Kassenbuch** über alle Konten. Hält jede Buchung
  mit Herkunft und vollständiger, auditierbarer Historie. Zeigt Buchungen und Stände pro
  Konto und in der Gesamtsicht, einschließlich Konten, die YNAB nicht führen kann
  (Wertpapierkonto mit Budget-Geld, bewertet als Stückzahl mal Kurs). Wird schrittweise
  zum Ort, an dem Buchungen nachbearbeitet werden (Kategorie, Splits, Überträge).
- **Core language:** Kassenbuch, Konto, Buchung, Herkunft, Historie, Spiegel, Abweichung,
  Führung (BB-geführt / YNAB-geführt), Übertrag, Bewertung, Gesamtsicht.
- **Classification:** core — das Kassenbuch ist das Gedächtnis, das BB zum führenden
  System macht.
- **Key actors:** Roman (liest, bearbeitet nach), Frau (liest), YNAB (Spiegel-Quelle und
  Projektionsziel), Kursquelle (Bewertung).

### budgeting *(neu, 2026-10-25)*
- **Purpose:** Das **Budget**: Geld auf Kategorien zuweisen, zwischen Kategorien
  verschieben, Targets anwenden, Monat für Monat mit Vortrag. BB führt, YNAB wird
  nachgezogen. Später erwartete Einnahmen, Leihen zwischen Kategorien, Entwürfe, Targets
  mit Deckel.
- **Core language:** Budget (das Ganze), Kategorie (der Topf), Kategoriegruppe, Monat,
  Zu verteilen, Zuweisen, Verschieben, Aktivität, Verfügbar, Vortrag, Target, Targets
  anwenden, Budget-Bewegung, Budget-Historie, erwartete Einnahme, Leihe, Entwurf.
- **Classification:** core: die Fähigkeit, die am stärksten an YNAB bindet.
- **Key actors:** Roman (verteilt, verschiebt), YNAB (Quelle der Targets, Ziel der
  Zuweisungen), Kassenbuch (liefert Aktivität).

### ynab-sync
- **Purpose:** In **beide Richtungen** zwischen Kassenbuch und YNAB vermitteln. Push:
  kategorisierte Transaktionen und BB-Entscheidungen nach YNAB, mit Import-ID-Dedup.
  Spiegel: YNABs Zustand (Konten, Buchungen, Änderungen) laufend ins Kassenbuch holen und
  Abweichungen erkennen. Stellt YNAB-Strukturen (Budgets/Accounts/Categories/Payees) bereit.
- **Core language:** ImportId, YnabImportStatus, YnabRejectionReason (DuplicateImportId/
  UnknownRejection), YnabTransaction, Splits/TransactionSplit, Payee/PayeeOverride,
  Transfer-Payee, YnabBudget/Account/Category, Spiegel-Lauf, Delta.
- **Classification:** core — ohne sauberen Push und vollständigen Spiegel ist das
  Kassenbuch wertlos. (Conformist gegenüber der YNAB-API.)
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
  das Komponenten-Inventar (`src/Client/DesignSystem/`) und die projektweiten UI-Muster
  (Sheets/Click-Commit ADR 0005, Picker ADR 0004). Hält den **Styleguide** als
  reviewbares Gate für alle UI-Arbeit.
- **Core language:** Styleguide, Token, neon-on-dark, Farbsemantik, DS-Komponente, Muster,
  Drift.
- **Classification:** supporting.
- **Key actors:** Roman (reviewt das Gate, baut UI), alle fachlichen BCs (Konsumenten).

### Künftige Contexts (Seeds, nicht angelegt)
- **wealth** (Vermögen): Depots ING und Consors als Vermögenssicht, kein Budget-Geld.
  Nicht v1/v2. Wird angelegt, wenn das Kassenbuch steht.

## Relationships

- **banking-import → categorization → ledger:** Customer-supplier / Pipeline. Bank-
  Buchungen werden kategorisiert und landen dann dauerhaft im Kassenbuch (Herkunft:
  Comdirect-Import + Regel/Roman). Seit ADR 0021 (2026-09-11) endet die Pipeline im
  `ledger`: `banking-import → categorization → ledger → ynab-sync`. Das Kassenbuch wird
  vor dem Push geschrieben, der Push folgt.
- **ledger ↔ ynab-sync:** **Partnership** in zwei Richtungen. `ledger` ist Upstream für
  den Push (BB-geführte Entscheidungen gehen nach YNAB); `ynab-sync` ist Upstream für den
  Spiegel (YNAB-geführte Änderungen kommen ins Kassenbuch, Herkunft: YNAB-Spiegel). Die
  **Abweichung** entsteht an dieser Naht und gehört dem `ledger`.
- **ynab-sync → YNAB (extern):** **Conformist** — BB ordnet sich der YNAB-API unter
  (ImportId-Dedup, Delta-Sync, Cleared-Verhalten, Transfer-Semantik). YNAB ist zugleich
  Quelle (Spiegel) und Ziel (Push).
- **banking-import → Comdirect (extern):** **Anticorruption layer** — die rohe
  Comdirect-JSON (`RawData`) wird in das saubere `BankTransaction`-Modell übersetzt.
- **ledger → Kursquelle (extern):** Anticorruption layer, klein: ein Kurs pro Papier,
  monatlich, für die Bewertung des Wertpapierkontos mit Budget-Geld.
- **Zwei Duplikat-Welten:** `banking-import` besitzt das *Vor-Import*-Urteil
  (DuplicateStatus); `ynab-sync` besitzt YNABs *eigenes* Dedup (ImportId →
  RejectedByYnab DuplicateImportId). Die **ImportId** ist der Shared-Kernel-Begriff,
  der beide verbindet, und wird im Kassenbuch als Teil der Herkunft mitgeführt.
- **Führung wandert:** Jede Fähigkeit ist entweder BB-geführt oder YNAB-geführt. Das
  Kassenbuch kennt für jede Buchung beides. Die Vision bewegt Fähigkeiten von rechts nach
  links (Nachbearbeitung, Überträge, als Nächstes das Budget).
- **ledger → budgeting:** Customer-supplier. Das Kassenbuch liefert die Aktivität pro
  Kategorie und Monat; das Kategorie-Detail springt zurück zur Buchung. Die Kategorie ist
  in beiden Contexts dasselbe Ding (YNABs Kategorie-Id), "Vortrag" (Budget) und
  "Übertrag" (Kassenbuch) sind verschiedene Begriffe.
- **budgeting ↔ ynab-sync:** Partnership. `ynab-sync` holt Targets und Budget-Zustand aus
  YNAB und schreibt BBs Zuweisungen zurück, nach demselben Muster wie die Schreibwege des
  Kassenbuchs.
- **banking-import → budgeting** (künftig): eine erwartete Einnahme wird beim Import der
  echten Buchung eingelöst.
- **infrastructure** ist generic supporting für alle fachlichen Contexts.
- **design-system → alle UI-tragenden BCs** (auch `ledger` und `budgeting`): Customer-Supplier.
  Der Styleguide (`design-system-001`) ist das **Gate**; jede UI-Task hängt daran.

## Open questions
- **ING** als zweite Quelle: eigener Adapter in `banking-import` oder Comdirect-Muster
  generalisieren? (verschoben; bis dahin über den Spiegel)
- **Quick Add** zuerst ins Kassenbuch? *Gelöst (ADR 0021, 2026-09-11):* ja, erst
  Kassenbuch, dann Push, dann Verknüpfung (`ledger-tdnpk`).
- Ob **budgeting** ein eigener Context wird oder in `ledger` aufgeht: *Gelöst (ADR 0063,
  2026-10-25):* eigener Context.
