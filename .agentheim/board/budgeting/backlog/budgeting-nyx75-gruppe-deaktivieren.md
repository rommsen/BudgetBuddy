---
id: budgeting-nyx75
title: "Kategorie-Gruppe in BB deaktivieren: die Gruppe verschwindet samt ihren Kategorien aus Budget-Liste und Pickern, in YNAB bleibt sie"
status: backlog
type: feature
context: budgeting
created: 2026-11-20
completed:
depends_on: [design-system-001]
blocks: []
tags: [kategorien, gruppe, deaktivieren, frontend]
related_adrs: [0088]
related_research: []
prior_art: [budgeting-sn2sn, budgeting-pscgb]
---

## Why
Roman (2026-11-20): „Ich kann keine Budget-Gruppen löschen.“ Auf der Seite „Kategorien“ gibt es für Gruppen nur
Anlegen und Umbenennen (budgeting-pscgb). Beim Erfassen hat Roman „In BB ausblenden“ gewählt: wie „Kategorie
deaktivieren“ (budgeting-sn2sn), nur für eine ganze Gruppe.

## What
Eine Gruppe lässt sich auf der Seite „Kategorien“ deaktivieren. Sie fehlt danach mit allen ihren Kategorien in der
Budget-Liste und in jedem Kategorie-Picker, steht im Abschnitt „Deaktiviert“ und lässt sich wieder aktivieren. In YNAB
bleibt sie bestehen.

## Acceptance criteria
- To be defined during refinement.

## Notes
Offen für die Verfeinerung:
- Regeln aus budgeting-sn2sn auf die Gruppe übertragen: Deaktivieren nur, wenn jede Kategorie der Gruppe 0 verfügbar
  und keine künftige Zuweisung hat? Sonst Meldung mit den Kategorien, die Geld halten.
- Was passiert mit den Kategorien beim Aktivieren der Gruppe: kommen alle zurück, oder nur die, die vorher einzeln
  aktiv waren?
- Kann die YNAB-API Gruppen ausblenden oder löschen? Annahme: Löschen nein. Prüfen, ob `PATCH` auf `category_groups`
  ein `hidden` kennt; sonst wie bei Kategorien der Hinweis „In YNAB noch sichtbar, dort ausblenden“.
- Regeln (`rules.yml`) und Buchungen, die auf Kategorien der Gruppe zeigen: wie bei einer einzeln deaktivierten
  Kategorie.
- Skizze zur Freigabe nötig (Ort des Knopfs am Gruppenkopf neben „Umbenennen“, Abschnitt „Deaktiviert“ mit Gruppen).
