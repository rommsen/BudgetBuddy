---
id: categorization-msep5
title: "Regeln speichern, importieren und sortieren fehlerhaft"
status: todo
type: bug
context: categorization
created: 2026-10-30
completed:
depends_on: []
blocks: [categorization-cgq35]
tags: [captured, bestandsreview]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Gruppe 1 aus dem Bestandsreview categorization vom 2026-10-29, höchste Schwere **hoch**. Beim Ändern, Anlegen und Importieren von Regeln gehen Werte verloren oder werden still verfälscht (Override nicht löschbar, alter Kategoriename, Platzhalter Unknown, halber Import, offene Reihenfolge bei gleicher Priorität).

## What
Alle Befunde der Gruppe, je Zeile Fundstelle | Regel | Schwere | Vorschlag. Bericht: `.agentheim/knowledge/reviews/2026-10-29-categorization.md`.

- `src/Server/Api.fs:597` | .agentheim/knowledge/contexts/categorization/README.md, Ubiquitous language "PayeeOverride"; .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1 | hoch | Ein gesetzter PayeeOverride einer Regel lässt sich nicht mehr entfernen.
- `src/Server/Api.fs:628` | .claude/rules/query-safety.md, "Kein stiller Default" | hoch | Kategoriewechsel einer Regel speichert bei YNAB-Fehler den alten Kategorienamen.
- `src/Server/Api.fs:550` | .claude/rules/query-safety.md, "Kein stiller Default"; .claude/rules/semantic-messaging.md, "Kein Befehls-DU ohne Aufrufer" | mittel | Regeln mit unbekannter Kategorie werden mit dem Platzhalternamen Unknown gespeichert.
- `src/Server/Persistence.fs:330 und :341` | .claude/rules/query-safety.md, "Kein stiller Default in Konvertern" | mittel | Unbekannte PatternType- und TargetField-Werte aus der Datenbank werden still zu Regex bzw. Combined.
- `src/Server/Api.fs:665 bis :690` | .claude/rules/query-safety.md, mehrere Schreibvorgänge in einer Transaktion; standards/backend/overview.md, "Validierung an der API-Grenze"; .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1 | mittel | Regel-Import läuft ohne Transaktion und ohne die Regel-Validierung.
- `src/Server/Persistence.fs:364` | .agentheim/knowledge/contexts/categorization/README.md, "Priority: die erste passende Regel gewinnt"; .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1 | mittel | Bei gleicher Priorität ist offen, welche Regel gewinnt, und die Reihenfolge ist ungetestet.
- `src/Server/Api.fs:699 und :716` | standards/backend/overview.md, "konkrete Meldungen"; .claude/rules/test-doctrine.md, Kein Test für Punkt 6 (Gegenprobe) | mittel | Regel testen meldet ein ungültiges Muster als kein Treffer, und der Treffer-Fall ist ungetestet.
- `src/Client/Components/Rules/State.fs:260` | .agentheim/knowledge/contexts/categorization/README.md, Aggregate Rule "sinnvolle Priority" | mittel | Neue Regeln bekommen Priorität 1, wenn die Regelliste nicht geladen ist.
- `src/Server/Api.fs:49` | standards/backend/overview.md, "konkrete Meldungen auf Deutsch" | mittel | Fehlermeldungen der Regeln und Splits erscheinen beim Nutzer auf Englisch.
- `src/Server/Api.fs:581` | semantic-messaging.md, Namen nach Absicht | niedrig | Validierungsfehler nicht als `InvalidPattern ("update"/"import")`, eigener Fall `ValidationFailed`.
- `src/Server/Validation.fs:122` | semantic-messaging.md, Kein Befehls-DU ohne Aufrufer | niedrig | `validatePayeeOverride` und `RulesError.DuplicateRule` einbinden oder löschen (CategoryNotFound siehe categorization-syy3j).
- `src/Shared/Domain.fs:380` | semantic-messaging.md, Nie CreateXInput | niedrig | `RuleCreateRequest`/`RuleUpdateRequest` nach Absicht benennen.
- `src/Server/Api.fs:545` | semantic-messaging.md, Logik nicht inline in Api.fs | niedrig | Kategoriename-Suche viermal inline (545, 621, 990, 1065), ein Helfer mit einheitlichem Fehlerverhalten.
- `src/Server/Api.fs:688` | Lesbarkeit, Mutabilität | niedrig | `let mutable importedCount` und `updatedTransactions` (1076) durch Ausdrücke ersetzen.

Entschieden im Refine (2026-10-30), bindend für den Worker:

- **PayeeOverride ändern:** `RuleUpdateRequest.PayeeOverride` wird ein eigener Typ mit drei Fällen (behalten, setzen, entfernen). "`None` heißt entfernen" wäre eine Falle: `ToggleRuleEnabled` (`Rules/State.fs:133`) schickt `None` und meint "nicht anfassen".
- **Kategoriename:** Nachgeschlagen wird nur, wenn die Kategorie sich wirklich ändert, oder beim Anlegen. Der Dialog schickt `CategoryId` immer mit; ohne diesen Vergleich könnte man bei einem YNAB-Ausfall nicht mal den Namen einer Regel ändern. Scheitert das Nachschlagen oder fehlt die Kategorie, wird nichts gespeichert und die Meldung nennt den Grund (neuer Fall `RulesError.CategoryLookupFailed`, `CategoryNotFound` wird benutzt). Kein "Unknown" mehr.
- **Priorität neuer Regeln vergibt der Server:** `Priority` entfällt aus `RuleCreateRequest`, der Server setzt höchste vorhandene Priorität plus 1. Damit fällt auch der Fehler aus `SyncFlow/State.fs:1054` weg: `Priority = 1` mit Kommentar "Highest priority" ist bei `ORDER BY priority DESC` die niedrigste, Regeln aus dem Zuweisungs-Dialog landen ganz unten.
- **Gleiche Priorität:** `ORDER BY priority DESC, rowid ASC`, die zuerst gespeicherte Regel gewinnt. Das erhält auch die Dateireihenfolge beim Import.
- **Import:** alle Regeln gegen dieselbe Validierung wie beim Anlegen, alle Fehler gesammelt mit Regelname, dann alle in einer Transaktion. Alles oder nichts. Kein YNAB-Aufruf, Kategorie und Name kommen aus der Datei. Bestehende Regeln bleiben, der Import hängt an wie bisher.
- **Konverter:** unbekannte Werte aus der DB per `failwith` wie in `.claude/rules/query-safety.md`. Eine kaputte Zeile lässt `getAllRules` scheitern, das ist gewollt: lieber sichtbarer Fehler als falsch kategorisierte Buchungen.
- **Regel testen:** `testRule` liefert `Async<RulesResult<bool>>`, ungültiges Muster ist `InvalidPattern`. Am Client nur so viel wie nötig; den Rest macht categorization-krscs.
- **Eine Fehlerübersetzung:** drei Kopien von `rulesErrorToString` (die im Server ist tot, zwei im Client) werden zu einer deutschen in `src/Shared`.
- **Validierungsfehler:** eigener Fall `ValidationFailed of string list` statt `InvalidPattern ("update"/"import")`. `validatePayeeOverride` wird in Anlegen, Ändern und Import eingebunden. `RulesError.DuplicateRule` fliegt raus.
- **Namen:** `RuleCreateRequest` wird `NewRule`, `RuleUpdateRequest` wird `RuleChange`.
- **Kategoriename-Suche:** ein Helfer für alle vier Stellen, er liefert `Result`. Die beiden Sync-Stellen (Api.fs:990, :1065) behalten ihr heutiges Verhalten (Fehler ergibt `None`).
- **Zugang für Tests:** Anlegen, Ändern und Import ziehen aus `rulesApi` in einen Handler mit Port, Vorbild `Server.Budgeting.CommandHandlers.YnabPort`. Sonst lassen sich die YNAB-Fälle ohne Live-Dienst nicht testen.

Nicht in dieser Task: `updatedTransactions` (Api.fs:1076) gehört zu categorization-hz6vj.

## Acceptance criteria
Tests laufen gegen In-Memory-SQLite mit gefälschtem YNAB-Port. Jeder Regressionstest ist vor dem Fix rot.

- [ ] Kaputt: GIVEN eine Regel mit PayeeOverride WHEN der Override im Dialog geleert und gespeichert wird THEN blieb er stehen. Intakt: er ist danach entfernt. Ein Ändern von Enabled allein lässt ihn unverändert.
- [ ] Kaputt: GIVEN YNAB antwortet mit Fehler WHEN die Kategorie einer Regel gewechselt wird THEN stand neue Id neben altem Namen. Intakt: Fehler, die Regel ist unverändert.
- [ ] GIVEN YNAB ist nicht erreichbar WHEN nur der Name einer Regel geändert wird THEN wird gespeichert und YNAB nicht gefragt.
- [ ] Kaputt: GIVEN unbekannte Kategorie, YNAB-Fehler oder fehlende Einstellungen WHEN eine Regel angelegt wird THEN stand "Unknown" in der Datenbank. Intakt: Fehler mit Grund, keine Zeile angelegt.
- [ ] Kaputt: GIVEN Regeln mit Priorität bis 7 WHEN eine Regel über den Zuweisungs-Dialog angelegt wird THEN bekam sie Priorität 1. Intakt: 8, sie steht in `getAllRules` vorn. Beide Anlege-Wege schicken keine Priorität mehr.
- [ ] Kaputt: GIVEN zwei Regeln gleicher Priorität, die auf dieselbe Buchung passen WHEN `getAllRules` liest THEN war die Reihenfolge offen. Intakt: die zuerst gespeicherte steht vorn und gewinnt in `RulesEngine`, unabhängig von der Id.
- [ ] Kaputt: GIVEN eine Importdatei, deren dritte von vier Regeln einen leeren Namen hat oder deren Einfügen scheitert WHEN importiert wird THEN entstand ein halber Import oder die Regel kam ungeprüft durch. Intakt: keine neue Zeile, die Meldung nennt alle fehlerhaften Regeln. Eine gültige Datei legt alle an, Reihenfolge bei gleicher Priorität wie in der Datei.
- [ ] GIVEN `pattern_type` oder `target_field` mit unbekanntem Wert in der Datenbank WHEN gelesen wird THEN scheitert das Lesen mit Nennung des Werts. Die bekannten Werte laufen unverändert durch.
- [ ] Kaputt: GIVEN ein ungültiger Regex WHEN Regel testen läuft THEN kam `false`. Intakt: `InvalidPattern`. Treffer und Nicht-Treffer sind je mit einem Test belegt.
- [ ] Jeder Fall von `RulesError` hat eine deutsche Meldung (Wertetest); `Rules/State.fs`, `SyncFlow/State.fs` und `Api.fs` enthalten kein eigenes `rulesErrorToString` mehr.
- [ ] Die Texte der Regel- und Split-Validierung in `Validation.fs` sind deutsch, alle Fehler werden gesammelt gemeldet.
- [ ] `RulesError.DuplicateRule`, `RuleCreateRequest` und `RuleUpdateRequest` kommen im Quelltext nicht mehr vor.
- [ ] README categorization nachgezogen: Priority mit Gleichstand-Regel und Vergabe durch den Server.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Captured via `quick-capture` on 2026-10-30. Refine am 2026-10-30: nicht geschnitten. Die niedrigen Befunde ändern dieselben Zeilen und Verträge wie die hohen, getrennt liefe alles zweimal durch dieselben Stellen.

Geänderte Remoting-Verträge (`RuleChange`, `NewRule`, `testRule`, `RulesError`): Client und Server gehen zusammen raus, Playwright-Fixtures haben laut Suche keine Regel-Antworten, trotzdem prüfen.

`scripts/import-rules.fsx` schreibt eigene Prioritäten direkt in die Datenbank und bleibt unberührt.

Regeln, die heute schon "Unknown" als Kategorienamen tragen, werden nicht repariert. Das ist nur der angezeigte Name; ein Wechsel auf eine andere Kategorie korrigiert ihn.

Reihenfolge: läuft vor categorization-cgq35 (Fehler mit Datenverlust zuerst), beide ändern `Rules/State.fs`.
