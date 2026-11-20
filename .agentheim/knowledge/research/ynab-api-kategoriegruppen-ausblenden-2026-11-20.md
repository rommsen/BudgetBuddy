---
topic: Kann die YNAB-API Kategoriegruppen oder Kategorien ausblenden oder löschen?
date: 2026-11-20
requested_by: conductor
related_tasks: [ADR 0088]
---

# Research: YNAB-API, Kategoriegruppen und Kategorien ausblenden oder löschen

## Frage
Gibt es in der offiziellen YNAB-API v1 einen Weg, eine Kategoriegruppe (oder Kategorie) auszublenden oder zu löschen? Welche Felder sind in POST und PATCH schreibbar?

## Summary
- Nein. Weder Kategoriegruppen noch Kategorien lassen sich über die API ausblenden oder löschen. Die Annahme aus ADR 0088 gilt weiter, Stand Spec-Version `1.91.0` [1].
- Kategoriegruppen: nur `POST` und `PATCH`, schreibbar ist allein `name` (höchstens 50 Zeichen) [1].
- Kategorien: `POST`, `PATCH` und Monats-`PATCH`. Schreibbar sind `name`, `note`, `category_group_id` und die `goal_*`-Felder. `hidden` und `deleted` fehlen im Request-Schema [1].
- Kein `DELETE` für Kategorien oder Kategoriegruppen. `DELETE` gibt es nur für Transaktionen und geplante Transaktionen [1], [2].
- Die Umbenennung von `/budgets/{budget_id}` auf `/plans/{plan_id}` kam 2026 (v1.78.0 am 2026-02-25 für Parameternamen, v1.79.0 am 2026-03-05 für Pfade), nicht 2025 [2].

## Findings

### Endpunkte (Spec `info.version: 1.91.0`)
Kategoriegruppen [1]:
- `POST /plans/{plan_id}/category_groups` (`createCategoryGroup`)
- `PATCH /plans/{plan_id}/category_groups/{category_group_id}` (`updateCategoryGroup`)
- Kein `GET` auf eine einzelne Gruppe, kein `DELETE`.

Kategorien [1]:
- `GET`, `POST /plans/{plan_id}/categories` (`getCategories`, `createCategory`)
- `GET`, `PATCH /plans/{plan_id}/categories/{category_id}` (`getCategoryById`, `updateCategory`)
- `GET`, `PATCH /plans/{plan_id}/months/{month}/categories/{category_id}` (`getMonthCategoryById`, `updateMonthCategory`)
- Kein `DELETE`.

Laut Changelog kamen Anlegen von Kategorien und Gruppen sowie Ändern von Gruppen mit v1.78.0 (2026-02-25) dazu [2].

### Request-Schemas
- `PostCategoryGroupWrapper` und `PatchCategoryGroupWrapper`: Pflichtfeld `category_group`, Verweis auf `SaveCategoryGroup` [1].
- `SaveCategoryGroup`: einzige Eigenschaft `name` (string, maxLength 50, Pflicht) [1].
- `PostCategoryWrapper`: `category` als `NewCategory` (`SaveCategory` plus Pflicht `name`, `category_group_id`). `PatchCategoryWrapper`: `category` als `ExistingCategory` (`SaveCategory` ohne Zusatzfelder) [1].
- `SaveCategory`: `name`, `note`, `category_group_id`, `goal_target`, `goal_target_date`, `goal_needs_whole_amount`, `goal_frequency` (`monthly`, `weekly`, `yearly`) [1].
- `SaveMonthCategory`: nur `budgeted` (Milliunits, Pflicht) [1].

`hidden` und `deleted` stehen nur in den Antwort-Schemas (`CategoryGroup`, `CategoryBase`), in keinem Request-Schema [1]. Der Changelog nennt keinen Eintrag zu Ausblenden oder Löschen von Kategorien [2].

### Einordnung
Die Spec definiert keine `additionalProperties`-Freigabe für `hidden`. Ob die API ein mitgeschicktes `hidden` still ignoriert oder ablehnt, sagt die Spec nicht. Dokumentiert ist es nicht, darauf bauen sollte man nicht.

## Sources
1. [YNAB API OpenAPI Spec](https://api.ynab.com/papi/open_api_spec.yaml), `info.version: 1.91.0`, abgerufen 2026-11-20. Primärquelle für Pfade und Schemas.
2. [YNAB API, Changelog auf api.ynab.com](https://api.ynab.com/), abgerufen 2026-11-20. Einträge v1.74.0 bis v1.91.0 (2026-11-10).

## Open questions
- Verhalten bei einem undokumentierten `hidden` im PATCH-Rumpf (ignoriert oder Fehler) ist ungeprüft. Klären ginge nur mit einem Integrationstest gegen ein Test-Budget hinter `RUN_INTEGRATION_TESTS`.
