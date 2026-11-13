---
id: 0094
title: Gelöschte Regel über einen eigenen Befehl an ihrer Stelle wiederherstellen
scope: categorization
status: accepted
date: 2026-11-13
supersedes: []
superseded_by: []
related_tasks: [categorization-n3sxw]
related_adrs: [0083, 0087]
related_research: []
---

# ADR 0094: Gelöschte Regel über einen eigenen Befehl an ihrer Stelle wiederherstellen

## Context

„Regel löschen“ im Tab „Regeln“ löscht sofort und bietet „Rückgängig“ im Toast (categorization-n3sxw, wie bei der
Buchung). Die Regel soll mit allen Feldern (auch Payee-Override und aktiv/inaktiv) an ihrer alten Stelle stehen,
ohne Zwischenstand an falscher Stelle. `createRule` vergibt die Priorität selbst (ganz vorn), fragt YNAB nach dem
Kategorienamen und kennt weder Id noch Payee-Override aus der Oberfläche.

## Decision

Ein eigener Befehl `restoreRule: RuleRestore -> Async<RulesResult<Rule list>>` mit `RuleRestore = { Rule; Position }`.
Er prüft wie beim Anlegen (Name, Muster, Payee-Override, Stelle nicht negativ; `Validation.validateRuleRestore`, alle
Fehler gesammelt), übersetzt das Muster und legt die Regel mit ihrer alten Id und allen Feldern in einer Transaktion
an der Stelle `Position` ein (`Persistence.Rules.restoreRuleAt`), wobei alle Regeln ihre Priorität neu bekommen. Eine
Stelle hinter dem Ende hängt sie unten an. YNAB wird nicht gefragt, Kategorie-Id und -Name kommen aus der Regel. Die
Antwort ist die ganze Liste in Prüfreihenfolge; der Client übernimmt sie, statt selbst einzufügen. Regeln bleiben
Zustand (ADR 0083).

## Consequences

- Kein Zwischenstand: der Client zeigt die Regel erst mit der Antwort, an der Stelle, die der Server gespeichert hat.
- Eine Regel, deren Id noch existiert, scheitert an der Eindeutigkeit (`DatabaseError restore_rule`), die Liste bleibt.
- `createRule` bleibt schlicht (Priorität vorn, Kategorie aus YNAB); `exportRules` und `importRules` entfallen, der
  Befehl ersetzt den Import als einzigen Weg, eine vollständige Regel samt Priorität anzulegen.