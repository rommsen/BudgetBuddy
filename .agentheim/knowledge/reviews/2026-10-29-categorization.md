# Bestandsreview categorization, 2026-10-29

Lauf: `/architecture-review categorization` (infrastructure-fam9k, erster Lauf), vier Agents (backend-code-quality, frontend-architecture, qa-milestone-reviewer, design-quality).
Befunde nach der Synthese: hoch 3, mittel 49, niedrig 27. Am 2026-10-30 nach der neuen Regel nachträglich in sieben Gruppen geordnet. Der Lauf hatte je Befund ab mittel eine Task angelegt; deren Ids stehen in Klammern und sind seit 2026-10-30 verworfen (d1d17a5), ersetzt durch je eine Task pro Gruppe.
Nicht erneut vorgeschlagen: SplitSheet.fs:39 (steht in design-system-bym4m).

## Gruppe 1: Regeln speichern, importieren und sortieren fehlerhaft

Beim Ändern, Anlegen und Importieren von Regeln gehen Werte verloren oder werden still verfälscht (Override nicht löschbar, alter Kategoriename, Platzhalter Unknown, halber Import, offene Reihenfolge bei gleicher Priorität).

- **Höchste Schwere:** hoch
- **Befunde:** 14 (9 ab mittel, 5 niedrig)
- **BCs:** categorization
- **Task:** categorization-msep5

- `src/Server/Api.fs:597` | .agentheim/knowledge/contexts/categorization/README.md, Ubiquitous language "PayeeOverride"; .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1 | hoch | Ein gesetzter PayeeOverride einer Regel lässt sich nicht mehr entfernen. (categorization-q3960)
- `src/Server/Api.fs:628` | .claude/rules/query-safety.md, "Kein stiller Default" | hoch | Kategoriewechsel einer Regel speichert bei YNAB-Fehler den alten Kategorienamen. (categorization-aemz9)
- `src/Server/Api.fs:550` | .claude/rules/query-safety.md, "Kein stiller Default"; .claude/rules/semantic-messaging.md, "Kein Befehls-DU ohne Aufrufer" | mittel | Regeln mit unbekannter Kategorie werden mit dem Platzhalternamen Unknown gespeichert. (categorization-syy3j)
- `src/Server/Persistence.fs:330 und :341` | .claude/rules/query-safety.md, "Kein stiller Default in Konvertern" | mittel | Unbekannte PatternType- und TargetField-Werte aus der Datenbank werden still zu Regex bzw. Combined. (categorization-k4xp9)
- `src/Server/Api.fs:665 bis :690` | .claude/rules/query-safety.md, mehrere Schreibvorgänge in einer Transaktion; standards/backend/overview.md, "Validierung an der API-Grenze"; .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1 | mittel | Regel-Import läuft ohne Transaktion und ohne die Regel-Validierung. (categorization-b2576)
- `src/Server/Persistence.fs:364` | .agentheim/knowledge/contexts/categorization/README.md, "Priority: die erste passende Regel gewinnt"; .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1 | mittel | Bei gleicher Priorität ist offen, welche Regel gewinnt, und die Reihenfolge ist ungetestet. (categorization-a510t)
- `src/Server/Api.fs:699 und :716` | standards/backend/overview.md, "konkrete Meldungen"; .claude/rules/test-doctrine.md, Kein Test für Punkt 6 (Gegenprobe) | mittel | Regel testen meldet ein ungültiges Muster als kein Treffer, und der Treffer-Fall ist ungetestet. (categorization-xn2sb)
- `src/Client/Components/Rules/State.fs:260` | .agentheim/knowledge/contexts/categorization/README.md, Aggregate Rule "sinnvolle Priority" | mittel | Neue Regeln bekommen Priorität 1, wenn die Regelliste nicht geladen ist. (categorization-mnmyg)
- `src/Server/Api.fs:49` | standards/backend/overview.md, "konkrete Meldungen auf Deutsch" | mittel | Fehlermeldungen der Regeln und Splits erscheinen beim Nutzer auf Englisch. (categorization-w32b3)
- `src/Server/Api.fs:581` | semantic-messaging.md, Namen nach Absicht | niedrig | Validierungsfehler nicht als `InvalidPattern ("update"/"import")`, eigener Fall `ValidationFailed`.
- `src/Server/Validation.fs:122` | semantic-messaging.md, Kein Befehls-DU ohne Aufrufer | niedrig | `validatePayeeOverride` und `RulesError.DuplicateRule` einbinden oder löschen (CategoryNotFound siehe categorization-syy3j).
- `src/Shared/Domain.fs:380` | semantic-messaging.md, Nie CreateXInput | niedrig | `RuleCreateRequest`/`RuleUpdateRequest` nach Absicht benennen.
- `src/Server/Api.fs:545` | semantic-messaging.md, Logik nicht inline in Api.fs | niedrig | Kategoriename-Suche viermal inline (545, 621, 990, 1065), ein Helfer mit einheitlichem Fehlerverhalten.
- `src/Server/Api.fs:688` | Lesbarkeit, Mutabilität | niedrig | `let mutable importedCount` und `updatedTransactions` (1076) durch Ausdrücke ersetzen.

## Gruppe 2: Regelseite am Handy und per Tastatur

Trefferflächen unter 48 px, ein Menü, das nur über Fokus öffnet, ein zentriertes Modal am Handy und Texte, die dem Styleguide nicht folgen.

- **Höchste Schwere:** hoch
- **Befunde:** 9 (5 ab mittel, 4 niedrig)
- **BCs:** categorization
- **Task:** categorization-cgq35

- `src/Client/Components/Rules/View.fs:94` | standards/frontend/styleguide.md §4 (Touch-Targets mindestens 48 px) | hoch | Prioritätspfeile der Regelliste sind am Handy 24 mal 20 px groß und liegen neben dem Zeilen-Tap. (categorization-m856w)
- `src/Client/Components/Rules/View.fs:569` | standards/frontend/styleguide.md §4 und §5 (Button, Icons) | mittel | Der Overflow-Knopf der Regelseite ist 32 px groß und hat keine Beschriftung. (categorization-mq0pf)
- `src/Client/Components/Rules/View.fs:577 und :583` | standards/frontend/styleguide.md §5 (FilterPopover, BottomSheet, Button: jede klickbare Aktion), §7 | mittel | Das Menü der Regelseite öffnet nur über Fokus und seine Einträge sind nicht per Tastatur bedienbar. (categorization-kwe4r)
- `src/Client/Components/Rules/View.fs:294` | standards/frontend/styleguide.md §5 (Modal nur Desktop, mobile Auswahl über BottomSheet) | mittel | Der Regel-Dialog läuft am Handy als zentriertes Modal mit Dropdown für die Kategorie. (categorization-b38ze)
- `src/Client/Components/Rules/View.fs:427` | standards/frontend/styleguide.md §8 (UI-Texte deutsch) | mittel | Englischer Platzhalter im Regel-Dialog. (categorization-k7vt7)
- `src/Client/Components/Rules/View.fs:400` | styleguide §5 ErrorDisplay.warning | niedrig | selbst gebauter Warnkasten "Keine Kategorien geladen" durch `ErrorDisplay.warning` ersetzen.
- `src/Client/Components/Rules/View.fs:311` | styleguide §8 konsistente Begriffe | niedrig | "z.B." (311, 368 bis 370) zu "z. B.", "..." (393, 472, 648) zu "…".
- `src/Client/Components/Rules/View.fs:476` | styleguide §8 Buttons als Verb | niedrig | "Test" zu "Testen".
- `src/Client/Components/Rules/View.fs:648` | styleguide §6 Skeletons | niedrig | `NotAsked` und `Loading` zeigen dasselbe Skeleton.

## Gruppe 3: Regel-Dialog: Ladezustände, Fehler und Seiteneffekte

Der Client der Regeln verschluckt Fehler, führt Ladezustände als Bool oder Text, bindet späte Antworten nicht an die Anfrage und hat Seiteneffekte in update und View.

- **Höchste Schwere:** mittel
- **Befunde:** 14 (10 ab mittel, 4 niedrig)
- **BCs:** banking-import, categorization
- **Task:** categorization-krscs

- `src/Client/Components/Rules/State.fs:201` | .claude/rules/elmish-state.md, "Boolean Flags für Async State", Failure anzeigen; standards/frontend/styleguide.md §8 (Ursache benennen) | mittel | Regel-Dialog zeigt bei fehlgeschlagenem Kategorien-Laden oder Deep-Link auf /rules eine leere Liste mit falschem Hinweis. (categorization-dr3v5)
- `src/Client/Components/Rules/Types.fs:17` | .claude/rules/elmish-state.md, "Boolean Flags für Async State" | mittel | Speicherzustand des Regel-Dialogs ist ein Bool-Flag. (categorization-g7j7q)
- `src/Client/Components/Rules/Types.fs:16` | .claude/rules/elmish-state.md, RemoteData-Muster | mittel | Ergebnis von Regel testen ist ein Anzeigetext mit Emoji, den die View wieder zerlegt. (categorization-p7s85)
- `src/Client/Components/Rules/State.fs:241` | .claude/rules/elmish-state.md, Ladeantwort an die Anfrage binden | mittel | Eine späte Antwort von Regel testen zeigt passt für eine inzwischen geänderte Eingabe. (categorization-mx09q)
- `src/Client/Components/Rules/State.fs:238` | .claude/rules/elmish-state.md, Failure korrekt anzeigen | mittel | Ein Netzwerkfehler beim Regel testen erscheint als ungültiges Muster. (categorization-qjmrm)
- `src/Client/Components/Rules/State.fs:88` | .claude/rules/elmish-state.md, "Cmd.OfAsync.perform" | mittel | Der Timeout der Lösch-Bestätigung läuft über Cmd.OfAsync.perform. (categorization-xh9rw)
- `src/Client/Components/Rules/State.fs:334` | .claude/rules/elmish-state.md, "Side Effects NUR über Cmd" | mittel | Der Regel-Export startet den Download per JS.eval direkt im update. (categorization-sh0cp)
- `src/Client/Components/Rules/View.fs:613` | .claude/rules/elmish-state.md, "Side Effects NUR über Cmd" | mittel | Dieselbe Regeldatei lässt sich kein zweites Mal importieren, weil die View die Datei selbst liest. (categorization-kpsp8)
- `src/Client/Components/Rules/Types.fs:107, :108, :129` | .claude/rules/semantic-messaging.md, "Kein Befehls-DU ohne Aufrufer" | mittel | Tote Nachrichten im Regel-Modul: ToggleRuleEnabled, RuleToggled und ImportRulesStart. (categorization-hdx88)
- `src/Client/Components/SyncFlow/Types.fs:72` | .claude/rules/elmish-state.md, "Boolean Flags für Async State" | mittel | Speicherzustand des Regel-Formulars im Zuweisungs-Dialog ist ein Bool-Flag. (banking-import-qrdh4)
- `src/Client/Components/Rules/State.fs:94` | elmish-state.md, Ladeantwort-Bindung | niedrig | `CancelConfirmDelete of RuleId`, nur zurücksetzen, wenn dieselbe Id noch bestätigt wird.
- `src/Client/Components/Rules/Types.fs:115` | semantic-messaging.md, Absicht statt CRUD | niedrig | `UpdateRuleForm*`-Setter (115 bis 122) nach Nutzerabsicht benennen (`PatternEntered`, `CategoryChosen` usw.).
- `src/Client/Components/Rules/Types.fs:125` | categorization README, Key events RuleCreated/RuleUpdated | niedrig | `SaveRule`/`RuleSaved` in Anlegen und Ändern trennen, `IsNewRule` plus `EditingRule` als DU `Editor`.
- `src/Client/Components/Rules/State.fs:52` | elmish-state.md, Ladeantwort-Bindung | niedrig | späte `LoadRules`-Antwort überschreibt eine optimistische `MoveRule`-Reihenfolge; Ladeversion mitführen oder MoveRule sperren.

## Gruppe 4: Kategorie-Picker vereinheitlichen

Import, Split und Quick-Add nutzen eine zweite, ältere Kategorieauswahl ohne Tastatur, Vorschläge und Fehlerzustand; Trefferflächen sind zu klein, und ADR 0011 widerspricht der heutigen Färbung.

- **Höchste Schwere:** mittel
- **Befunde:** 17 (11 ab mittel, 6 niedrig)
- **BCs:** banking-import, design-system, ynab-sync
- **Task:** design-system-taytp

- `src/Client/DesignSystem/BottomSheet.fs:439 und :410` | standards/frontend/styleguide.md §2 "Auswahl" (Einfachauswahl über CategoryPicker.sheet/popover, Pfeile und Enter), §5 | mittel | BottomSheet.categoryPicker ist eine zweite Kategorieauswahl neben CategoryPicker, ohne Tastatur und ohne Ohne Kategorie. (design-system-cr1n0)
- `src/Client/DesignSystem/BottomSheet.fs:110` | standards/frontend/styleguide.md §5 Icons (keine rohen svg inline) | mittel | Der Schließen-Knopf der Bottom Sheets ist ein eigenes Inline-SVG. (design-system-g5q09)
- `src/Client/DesignSystem/BottomSheet.fs:186` | standards/frontend/styleguide.md §4 (Touch-Targets mindestens 48 px) | mittel | Der Schließen-Knopf aller Bottom Sheets ist 32 px groß. (design-system-qhw6y)
- `src/Client/DesignSystem/BottomSheet.fs:71` | standards/frontend/styleguide.md §4 (Touch-Targets) | mittel | Die Chips Vorgeschlagen und Zuletzt verwendet im Kategorie-Sheet sind etwa 33 px hoch. (design-system-yw9bs)
- `src/Client/DesignSystem/BottomSheet.fs:586` | standards/frontend/styleguide.md §2 "Auswahl" (PickerParts.row mit truncate), Zugänglichkeit | mittel | Kontozeilen im Account-Picker sind nicht fokussierbar und kürzen lange Namen nicht. (design-system-nhn83)
- `src/Client/DesignSystem/BottomSheet.fs:36` | .agentheim/knowledge/decisions/0011-category-available-in-assignment-pickers.md Decision 3 gegen src/Client/DesignSystem/Money.fs:225 und standards/frontend/styleguide.md §2 | mittel | ADR 0011 nennt positive Available-Beträge grün, Code und Styleguide färben sie neutral. (design-system-zpq7t)
- `src/Client/Components/SyncFlow/Views/TransactionList.fs:315` | standards/frontend/styleguide.md §2 "Auswahl", Abschnitt Vorgeschlagen | mittel | Der Kategorie-Picker im Import zeigt nie Vorschläge zum Payee. (banking-import-bqhd3)
- `src/Client/Components/SyncFlow/Views/TransactionList.fs:66` | CLAUDE.md MVU (alle Zustandsänderungen über update); .claude/rules/elmish-state.md | mittel | Welche Import-Buchung den Kategorie-Picker offen hat, liegt außerhalb des Models. (banking-import-qcx84)
- `src/Client/Components/SyncFlow/State.fs:851` | .claude/rules/elmish-state.md, Failure anzeigen, RemoteData | mittel | Ein Fehler beim Laden der Kategorien im Import lässt alle Kategorie-Picker stumm leer. (banking-import-smbe4)
- `src/Client/Views/QuickAddPage.fs:177` | standards/frontend/styleguide.md §4 (Touch-Targets), §5 (Icons) | mittel | Kategorie entfernen auf der Quick-Add-Seite ist 42 px breit und eine Textglyphe. (ynab-sync-gwtm0)
- `src/Client/Views/QuickAddPage.fs:83` | CLAUDE.md MVU; .claude/rules/semantic-messaging.md (Absicht statt technischer Setter) | mittel | Die Quick-Add-Seite baut den Formularzustand in der View und schickt ihn ungeprüft ins update. (ynab-sync-x708m)
- `src/Client/Views/QuickAddPage.fs:262` | elmish-state.md, Option statt Leerwert | niedrig | `CategoryId: YnabCategoryId option` statt "".
- `src/Client/Views/QuickAddPage.fs:256` | styleguide §7.1 Punkt 4 Layer-Klasse | niedrig | `categoryPickerLayered` stapelt auf der Seite über nichts, Kopfkommentar Zeile 15 veraltet.
- `src/Client/Views/QuickAddPage.fs:162` | Zugänglichkeit, Beschriftung | niedrig | Label "Kategorie" per `htmlFor`/`id` mit dem Auswahl-Knopf verknüpfen.
- `src/Client/DesignSystem/BottomSheet.fs:403` | styleguide §2 "Keine Treffer" | niedrig | leere Kategoriesuche zeigt "Keine Treffer".
- `src/Client/DesignSystem/BottomSheet.fs:597` | styleguide §2 "Keine Treffer" | niedrig | leerer Account-Picker als Hinweiszeile statt Abschnittskopf.
- `src/Client/DesignSystem/BottomSheet.fs:335` | Zugänglichkeit, Beschriftung | niedrig | `ariaLabel` "Kategorie suchen" (auch Zeile 566 für Konten).

## Gruppe 5: Split im Import

Das Split-Sheet des Imports baut die Design-System-Bausteine nach, sperrt Speichern nicht und wird im Filter falsch eingeordnet.

- **Höchste Schwere:** mittel
- **Befunde:** 6 (5 ab mittel, 1 niedrig)
- **BCs:** banking-import
- **Task:** banking-import-wp7js

- `src/Client/Components/SyncFlow/Views/SplitSheet.fs:173` | standards/frontend/overview.md, Schaltflächen zeigen den Ladezustand und sind gesperrt | mittel | Speichern im Split-Sheet des Imports bleibt während des Aufrufs aktiv, ein Doppeltipp schickt zwei Aufrufe. (banking-import-e7hna)
- `src/Client/Components/SyncFlow/Views/SplitSheet.fs:49 und :168` | standards/frontend/styleguide.md §2 (primäre Aktion über Button.primary), §5 (vorhandene DS-Komponente statt Inline-Feliz) | mittel | Das Split-Sheet des Imports baut die Split-Bausteine des Design Systems nach. (banking-import-m7b58)
- `src/Client/Components/SyncFlow/Views/SplitSheet.fs:97` | standards/frontend/styleguide.md §4 (Touch-Targets), §5 (Icons) | mittel | Knöpfe im Split-Sheet des Imports sind schmaler als 48 px und nutzen Textglyphen. (banking-import-njams)
- `src/Client/Components/SyncFlow/Views/TransactionList.fs:22` | .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1; .agentheim/knowledge/contexts/categorization/README.md, Szenarien und Tests | mittel | Aufgeteilte Buchungen landen im Import-Filter unter ohne Kategorie. (banking-import-cf7b3)
- `src/Client/Components/SyncFlow/State.fs:42` | .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 2 | mittel | Die Auswahl aller sichtbaren Import-Zeilen nutzt eine ungetestete Kopie des Filters. (banking-import-e4e1z)
- `src/Client/Components/SyncFlow/Views/SplitSheet.fs:52` | standards/frontend/overview.md prop.key | niedrig | stabile Id je `SplitDraftLine` statt Index-Key.

## Gruppe 6: Kategorisieren im Import: Order-Id und Review-Spur

Sammel-Kategorisierung überträgt nicht auf Order-Id-Geschwister, eine kaputte Id bricht confirmTan ab, Ids sind nackte Strings, und die Schreibpfade der Review-Spur haben keine API-Adapter-Tests.

- **Höchste Schwere:** mittel
- **Befunde:** 7 (5 ab mittel, 2 niedrig)
- **BCs:** categorization
- **Task:** categorization-hz6vj

- `src/Server/Api.fs:1077` | .agentheim/knowledge/contexts/categorization/README.md, "Order-ID-Matching", Key command PropagateOrderId | mittel | Sammel-Kategorisierung überträgt die Kategorie nicht auf Buchungen mit derselben Order-Id. (categorization-pk04j)
- `src/Server/OrderIdMatcher.fs:61` | .claude/rules/domain-purity.md, "Regelverletzungen sind Result-Werte, keine Ausnahmen" | mittel | Eine nicht lesbare Kategorie-Id aus der YNAB-Historie lässt den Order-Id-Abgleich und damit confirmTan abbrechen. (categorization-tg9fw)
- `src/Shared/Domain.fs:507` | .claude/rules/semantic-messaging.md, Fachbegriffe als Typen | mittel | Kategorie-Ids in YnabTransaction und OrderIdSuggestion sind nackte Strings statt YnabCategoryId. (categorization-s2cdw)
- `src/Server/Api.fs:974` | .agentheim/knowledge/contexts/categorization/README.md, "Review-Spur" und Szenarien und Tests; .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1 | mittel | Manuelles Kategorisieren im Import hat keinen Test auf API-Adapter-Ebene. (categorization-vzh1m)
- `src/Server/Api.fs:1054, :1095, :1125; src/Tests/OrderIdMatcherTests.fs:352` | .agentheim/knowledge/contexts/categorization/README.md, "Review-Spur"; .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1 und Kein Test für Punkt 6 | mittel | Sammel-Kategorisieren, Aufteilen und Aufteilung entfernen haben keine API-Adapter-Tests. (categorization-jnyk8)
- `src/Server/Api.fs:979` | semantic-messaging.md, Namen nach Absicht | niedrig | unbekannte Transaktions-Id als `TransactionNotFound` statt `SessionNotFound` (auch skip, split, clearSplit).
- `src/Server/Api.fs:1114` | categorization README, Order-ID-Matching | niedrig | `splitTransaction` leert `SuggestedByOrderId` wie categorize und bulk.

## Gruppe 7: Testlücken und nachgebaute Tests

Tests rechnen Produktionslogik im Test nach oder fehlen für Ablehnungen, Gegenproben und deaktivierte Regeln.

- **Höchste Schwere:** mittel
- **Befunde:** 12 (7 ab mittel, 5 niedrig)
- **BCs:** categorization, ynab-sync
- **Task:** categorization-mznx3

- `src/Tests/RulesEngineTests.fs:157` | .claude/rules/test-doctrine.md, Kein Test für Punkt 6 (Gegenprobe) | mittel | Payee- und Memo-Regeln haben keine Gegenprobe gegen das jeweils andere Feld. (categorization-e8e56)
- `src/Server/Api.fs:535 und :604` | .agentheim/knowledge/contexts/categorization/README.md, Aggregate Rule (bei Regex kompilierbar); .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 4 | mittel | Ablehnung eines kaputten Regex beim Anlegen und Ändern einer Regel ist ungetestet. (categorization-h88qx)
- `src/Tests/PersistenceTypeConversionTests.fs:81` | .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1 | mittel | Eine deaktivierte Regel ist im Persistenz-Rundlauf nicht abgesichert. (categorization-m61mq)
- `src/Client/Components/Rules/State.fs:187` | .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1; .agentheim/board/categorization/done/cat-001-rule-reorder-ui.md, AC "Bei Fehler: Toast + Reload" | mittel | Fehlgeschlagenes Umsortieren der Regeln hat keinen Client-update-Test. (categorization-fbj8w)
- `src/Tests/YnabClientTests.fs:690` | .claude/rules/test-doctrine.md, Kein Test für Punkt 6 | mittel | YnabClientTests rechnet den PayeeOverride im Test nach. (ynab-sync-cd9t5)
- `src/Tests/YnabClientTests.fs:1100` | .claude/rules/test-doctrine.md, Kein Test für Punkt 6 | mittel | YnabClientTests baut den Transaktions-Encoder im Test nach. (ynab-sync-e71p7)
- `src/Tests/YnabClientTests.fs:448` | .claude/rules/test-doctrine.md, Kein Test für Punkt 6 | mittel | Der Kategorien-Decoder ist im Test kopiert statt aus dem Produktionscode aufgerufen. (ynab-sync-wsb8m)
- `src/Tests/RulesEngineTests.fs:215` | test-doctrine.md, Stil Punkt 19, Kein Test für Punkt 6 | niedrig | Test heißt High Priority, prüft aber nur die Listenreihenfolge; Daten und Namen auf "erste Regel der Liste gewinnt".
- `src/Client/Components/Rules/State.fs:303` | test-doctrine.md, Heimat-Ebene Punkt 1 | niedrig | update-Test für `RuleSaved (Ok _)`: neu vorn, bearbeitet an derselben Stelle, Dialog zu.
- `src/Tests/RulesEngineTests.fs:601` | test-doctrine.md, Heimat-Ebene Punkt 2 | niedrig | `textBasedSpecialLinkTests` doppelt `specialPatternTests` (303 bis 356); zusammenlegen.
- `src/Tests/RulesListTests.fs:104` | test-doctrine.md, Kein Test für Punkt 6 (Konstanten) | niedrig | Wortlaut-Vergleich auf `priorityHint` streichen.
- `src/Tests/RulesEngineTests.fs:60` | test-doctrine.md, Stil Punkt 19 | niedrig | englische Testnamen (auch OrderIdMatcherTests.fs:64ff., RulesReorderTests.fs:40ff.) beim nächsten Anfassen als deutsche fachliche Sätze.
