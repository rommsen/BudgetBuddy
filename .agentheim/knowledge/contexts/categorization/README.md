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
- **Priority**: Reihenfolge der Regel-Anwendung; die erste passende Regel gewinnt. Höhere Zahl steht vorn. Bei gleicher Priority gewinnt die zuerst gespeicherte Regel (`ORDER BY priority DESC, rowid ASC`). Die Priority neuer Regeln vergibt der Server: niedrigste vorhandene minus 1, ohne Regel 0, auch 0 oder negativ (`Persistence.Rules.priorityBelow`, `NewRule` trägt keine). Eine neue Regel steht damit am Ende und wird nach allen älteren geprüft, aus „Neue Regel“ im Tab „Regeln“ wie aus „Regel anlegen“ an der Buchung (categorization-m47g8, Roman 2026-11-14). Die Pfeile im Tab „Regeln“ (`reorderRules`) und „Rückgängig“ nach dem Löschen (`restoreRule`) vergeben allen Regeln die Priority neu, von oben gezählt.
- **PayeeOverride**: überschreibt den an YNAB gemeldeten Payee unabhängig von der Kategorie. Eine Regeländerung (`RuleChange`) sagt mit `PayeeOverrideChange` ausdrücklich, was geschieht: behalten, setzen oder entfernen. Die Oberfläche hat dafür kein Feld mehr (categorization-n3sxw): das Formular schickt `Keep`, eine Regel mit Override behält ihn; Engine und Speicherung bleiben.
- **AutoCategorized** — TransactionStatus, wenn eine Regel automatisch gegriffen hat
  (vs. **ManualCategorized** für Romans Hand-Zuordnung).
- **Order-ID-Matching** — beim Abruf übernimmt eine neue Amazon-Buchung ohne Kategorie die Kategorie der jüngsten YNAB-Buchung mit gleicher Order-ID (`OrderIdMatcher.applySuggestions`, `SuggestedByOrderId`, `OrderIdSuggestion`). Eine Kategorie-Id aus der Historie, die keine Guid ist, überspringt den Eintrag, statt den Abruf abzubrechen (categorization-hz6vj, in banking-import-d0fey). Die Weitergabe an Buchungen derselben Bestellnummer innerhalb eines Imports (`propagateInSession`) entfiel mit der Prüfsitzung (ADR 0093).
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
  (categorization-gdrx8). Seit dem Abruf mit Buchen (ADR 0093) hängen zwei Schreibpfade
  einen Schritt an, beide zur Abrufzeit:
  - `RulesEngine.classifyTransactions`: `ByRule`, beim Regel-Treffer.
  - `OrderIdMatcher.applySuggestions`: `ByOrderIdMatch orderId`, Kategorie aus der
    YNAB-Historie derselben Bestellung; eigener Urheber, weil weder Regel noch Roman diese
    Buchung entschieden haben.
  Romans Entscheidungen fallen danach im Kassenbuch (Ereignisse der Entscheidungs-Familie).
- **Available** — der aktuelle Budgetwert einer Kategorie (YNAB `balance`, das "Available"
  des laufenden Monats), als `Money option` (None = keine Zahl zeigen). Im Kategorie-Picker
  rechtsbündig und farbcodiert (grün >0 / rot <0 / neutral =0) hinter dem Namen, damit "passt
  diese Buchung hier rein" am Zuweisungspunkt entscheidbar ist. So frisch wie der letzte
  Kategorien-Load, kein Live-Refresh pro Zeile. **Ausnahme "Inflow: Ready to Assign":** deren
  eigenes `balance` ist ein interner Akkumulator (Fantasiewert); stattdessen zeigt sie den
  echten Ready-to-Assign des Monats (`to_be_budgeted`), via `applyReadyToAssign` (ADR 0011).
- **Muster-Test**: prüft ein Muster gegen einen Text, ohne eine Regel zu speichern (`testRule`). Drei getrennte Ausgänge: passt, passt nicht, kein Urteil. Kein Urteil gibt es bei ungültigem Muster (nennt den Grund) und bei Netzfehler (eigene Meldung), beides ist nie „passt nicht“.
- **Tab „Regeln“**: der Ort der Regeln, ein Tab im Kassenbuch neben „Buchungen“ (`#/ledger/rules`, der alte Pfad `#/rules` führt dorthin; ADR 0087 Punkt 9). Die Liste nach Priority mit Pfeilen; eine Regel öffnet am PC (ab 1024 px) im Panel, am Handy im Sheet. Felder: Name (leer heißt Muster), Wenn (`PatternType`), Suchen in (`TargetField`), Muster, Kategorie, „Regel aktiv“ (auch beim Anlegen, vorbelegt an, `NewRule.Enabled`), Muster-Test. Export, Import und Neu laden gibt es nicht mehr (categorization-n3sxw).
- **Regel wiederherstellen** (`restoreRule`, `RuleRestore`): „Rückgängig“ an der Zeile „gelöscht“ legt die gelöschte Regel mit allen Feldern (auch PayeeOverride und Enabled) an ihrer alten Stelle wieder an. Einfügen und neue Priorities laufen in einer Transaktion, die Antwort ist die ganze Liste, danach leuchtet die Regel kurz. Scheitert der Aufruf, fehlt die Regel, und die Zeile wird rot mit Grund und „Erneut“ (categorization-t6j00).
- **Rückmeldung im Tab „Regeln“** (Regel R3, ADR 0098, categorization-t6j00): kein Toast, auf keiner Breite. Eine gelöschte Regel hinterlässt an ihrer Stelle die **Zeile „gelöscht“** (`Model.Removed`, `Feedback.removedRowIn`): „wird gelöscht …“, solange `deleteRule` läuft, dann „gelöscht“ mit „Rückgängig“, „wird wiederhergestellt …“, gescheitert rot „nicht wiederhergestellt“ mit Grund und „Erneut“. Sie trägt keine Nummer, die Regeln danach rücken auf; es gibt höchstens eine. Eine gespeicherte, angelegte oder wiederhergestellte Regel leuchtet kurz (`Model.Flashed`). Ein **Fehler an einer Regel** (`Model.Fault`: „Nicht gelöscht: …“, „Nicht gespeichert: …“, „Reihenfolge nicht gespeichert: …“) steht rot unter ihr, fehlt sie in der Liste, darüber. Zeile und Fehler verschwinden mit der **nächsten Aktion** (`isNextAction`: Regel wählen, „Neue Regel“, Pfeil, Löschen, „Regel anlegen“ aus einer Buchung, Tab verlassen), ohne Zeitlimit. Scheitert das Neuladen bei stehender Liste, steht über ihr der Hinweis „Regeln nicht neu geladen“ mit „Neu laden“, bis ein Laden klappt. An den Meldungsplatz der Seite (`ShowFeedback`) gehen nur „Regel angelegt“ mit dem Namen aus einer Buchung, „Regel nicht angelegt“ bei geschlossenem Formular und „Regel nicht wiederhergestellt“, wenn die Zeile schon weg ist.

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
- *RestoreRule*: eine gelöschte Regel an ihrer Stelle wieder anlegen („Rückgängig“ im Tab „Regeln“).

## Relationships with other contexts
- **Downstream of:** `banking-import` — erhält geholte BankTransactions.
- **Upstream of:** `ledger` (beim Abruf mit Buchen, ADR 0021 und ADR 0093). Die
  SyncTransaction trägt ihre Review-Spur (`ReviewSteps`, categorization-gdrx8) vom Abruf bis
  zum Buchen; `recordComdirectPush` übersetzt sie in eine Ereignisfolge im Kassenbuch,
  danach pusht `ynab-sync`.
- Voller Kontext-Überblick: `../../context-map.md`.

## Szenarien und Tests

- **Bug-Tasks:** mindestens ein „kaputt“- und ein „intakt“-Szenario als Akzeptanzkriterium (GIVEN/WHEN/THEN). Der Regressionstest zum kaputt-Szenario ist vor dem Fix rot.
- **Fachliches Verhalten** (Zustandswechsel, Grenzen, Berechtigungen, was an den Client geht) steht in den ACs als Szenario; jedes Szenario hat einen Test. Einfache Fakten-ACs (Datei existiert, Zahl im Outcome) brauchen keins.
- **Maßstab für Tests:** `.claude/rules/test-doctrine.md` (Heimat-Ebene, Pflichtliste, kein Test für Tautologien).

## Test command

`./verify.sh` im Repo-Wurzelverzeichnis. Läuft dotnet build, Expecto über die Tests.dll, npm run build, guard:classes, guard:tokens und test:scripts; grün nur bei der Schlusszeile `VERIFY: PASS` und Exit 0. Bei `VERIFY: FAIL` stehen die roten Schritte und die Namen der roten Tests am Ende der Ausgabe.

## Open questions
- Sollen Regeln versionierbar/auditierbar sein, oder reicht das aktuelle CRUD-Modell?
- Order-ID-Matching nur Amazon, oder verallgemeinerbar auf andere Merchant-IDs?
