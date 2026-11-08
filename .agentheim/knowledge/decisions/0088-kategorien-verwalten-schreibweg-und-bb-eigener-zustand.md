---
id: 0088
title: Kategorien verwalten: Anlegen und Umbenennen über YNAB, Deaktivieren und Reihenfolge nur in BB
scope: budgeting
status: accepted
date: 2026-11-08
supersedes: []
superseded_by: []
related_tasks: [budgeting-pscgb, budgeting-sn2sn]
related_adrs: [0083, 0084, 0086]
related_research: []
---

# ADR 0088: Kategorien verwalten

## Context
Etappe 2 des Fahrplans (ADR 0086): Kategorien sollen in BB angelegt, umbenannt, in eine andere Gruppe
verschoben und deaktiviert werden, nie gelöscht. Die Kategorienliste kommt heute aus YNAB (Kategorie-Spiegel,
`upsertCategories`). Die YNAB-API (OpenAPI-Spezifikation, `https://api.ynab.com/papi/open_api_spec.yaml`, gelesen
2026-11-08) kann Kategorien anlegen (`POST /plans/{id}/categories`, `name` und `category_group_id`), ändern
(`PATCH .../categories/{id}`: `name`, `note`, `category_group_id`, Goal-Felder) und Gruppen anlegen und umbenennen
(`POST`/`PATCH .../category_groups`, `name` bis 50 Zeichen). Sie kann eine Kategorie nicht ausblenden (kein
schreibbares `hidden`), nicht löschen und keine Reihenfolge setzen.

## Decision
Entschieden von Roman am 2026-11-08.

1. **Anlegen, Umbenennen, Gruppe wechseln, Gruppen anlegen und umbenennen** gehen über die YNAB-API. BB schreibt
   nach YNAB und zieht danach die Kategorienliste über den Kategorie-Spiegel nach. YNAB bleibt die Quelle der Liste
   und der Namen; BB hält dafür kein eigenes Ereignis (kein Geld, ADR 0083).
2. **Deaktivieren nur in BB.** Eine deaktivierte Kategorie verschwindet in BB aus Auswahl und Budget-Liste; ihre
   Buchungen und Historie bleiben. Deaktivieren geht nur, wenn sie im laufenden Monat kein verfügbares Geld hat und
   in keinem künftigen Monat Geld zugewiesen ist. Hat sie in einem Monat dennoch Aktivität oder Verfügbares (etwa
   durch eine spätere Buchung), zeigt BB sie in diesem Monat wieder, damit kein Geld unsichtbar wird. In YNAB bleibt
   sie sichtbar; BB zeigt den Hinweis, sie dort von Hand auszublenden. Blendet Roman sie in YNAB aus, übernimmt BB das
   wie bisher (`Hidden`). Zustand, kein Ereignis (ADR 0083).
3. **Eigene Reihenfolge in BB.** BB merkt sich je Gruppe eine eigene Reihenfolge der Kategorien und zeigt sie in
   Budget und Auswahl. YNAB behält seine eigene; beide laufen auseinander, das ist gewollt. Neue Kategorien aus YNAB
   stehen am Ende ihrer Gruppe. Zustand, kein Ereignis.
4. **Kein Löschen.**

## Consequences
- Deaktivieren und Reihenfolge sind die ersten Stellen, an denen BB bewusst anders aussieht als YNAB. Wer in YNAB
  nachsieht, findet dort eine andere Reihenfolge und Kategorien, die BB nicht zeigt.
- Ein Fehler beim Schreiben nach YNAB lässt die Liste unverändert; BB meldet ihn konkret und ändert nichts lokal.

## Alternatives considered
- **Deaktivieren weglassen**, nur in YNAB ausblenden: abgelehnt, Roman will es in BB.
- **Reihenfolge aus YNAB übernehmen**, Pfeile weglassen: abgelehnt, Roman will die eigene Reihenfolge in BB.
- **Kategorien als BB-eigene Ereignisse führen:** abgelehnt, YNAB bleibt Quelle der Liste, solange die API Anlegen
  und Umbenennen kann.
