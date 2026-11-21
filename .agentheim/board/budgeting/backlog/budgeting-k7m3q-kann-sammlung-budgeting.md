---
id: budgeting-k7m3q
title: "Kann-Sammlung budgeting"
status: backlog
type: chore
context: budgeting
created: 2026-11-20
completed:
depends_on: []
blocks: []
tags: [review, ui]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Kann-Sammlung des BC nach der Regel Backlog-Disziplin in CLAUDE.md. Funde ohne Geld-, Daten-, Sicherheits- oder Alltagsfehler kommen als Zeile hierher. Sie wird nie automatisch gebaut.

## What
- Work-Lauf 2026-11-20 (budgeting-gtrgk): bei offener Verschiebung nimmt deren Hinweis im festen Bereich des Sheets bei 320 px fast die ganze Höhe, die Tab-Leiste rückt unter den Rand (vorher schon so).
- Work-Lauf 2026-11-20 (budgeting-gtrgk): nach einem Fehler stehen „Erneut senden“ am Hinweis der offenen Verschiebung und in der roten Zeile doppelt da.
- Work-Lauf 2026-11-20 (budgeting-y1kkr): nach erfolgreichem „Erneut senden“ (alle Zeilen geschrieben) gibt es kein „Rückgängig“; die Task nennt nur die letzte Verschiebung aus dem Tab.
- Work-Lauf 2026-11-20 (budgeting-y1kkr): ein Doppeltipp auf „Rückgängig“ zeigt beim zweiten Tipp gelb „Eine Verschiebung läuft noch“; der Auftrag geht nur einmal hinaus.
- Work-Lauf 2026-11-20 (budgeting-p3sk0): ist der Monat beim ersten Betreten der Übersicht noch nicht geladen und dauert das Laden länger als 0,9 s, ist das Leuchten der neu angepinnten Kategorie vorbei, bevor die Zeile erscheint.
- Work-Lauf 2026-11-20 (budgeting-p3sk0): auf einem anderen Gerät angepinnte Kategorien leuchten beim nächsten Betreten nach einem Neu-Lesen ebenfalls.
- Work-Lauf 2026-11-20 (budgeting-sx9v5): verlässt man „Targets prüfen“ vor der Antwort auf „übernehmen“, geht die Rückmeldung verloren (auch eine Ablehnung).
- Work-Lauf 2026-11-20 (budgeting-sx9v5): der Satz „wird gleich neu gelesen“ steht auch nach gescheitertem Neu-Lesen noch da.
- Work-Lauf 2026-11-20 (budgeting-sx9v5): eine Ablehnung zu einem Target kann im Sheet einer anderen Kategorie landen, wenn inzwischen gewechselt wurde.
- Review-Lauf 2026-11-20: in Overview stehen `pinSaveNotice` und `pinStep` mit derselben Bedingung (`PinsSaved (Error _)` bei `Pins = Success sent`); die Wurzel könnte `overview'.PinError` nach dem Schritt lesen.
- Work-Lauf 2026-11-21 (budgeting-m5pqn): viele Handler-Tests lesen feste Monate in der geteilten Test-DB; seit es den YNAB-Stand gibt, hängt ein reines Lesen davon ab, welcher Test den Monat zuletzt beobachtet hat (etwa `TargetChangeTests` „Ein in YNAB geändertes Goal …“). Eigene Monate oder Beobachten vor dem Lesen würde es festigen.
- Work-Lauf 2026-11-21 (budgeting-m5pqn): Befehle planen auf YNABs Verfügbar, die Seite zeigt BBs abgeleitetes; weichen Kassenbuch und YNAB ab, kann eine Prüfung auf Verfügbar im Befehl andere Zahlen sehen als die Seite.
- Work-Lauf 2026-11-21 (budgeting-nyx75): Kassenbuch-Filter (`CategoryMultiSelect`) und Bob (`BotApi.getCategories`) bieten Kategorien einer deaktivierten Gruppe weiter an, wie bei einzeln deaktivierten.
- Work-Lauf 2026-11-21 (budgeting-nyx75): ein YNAB-Stand von vor der Spalte `group_id` kennt die Gruppe nicht; bis der Monat neu beobachtet wird, zeigt die Budget-Seite Kategorien einer frisch deaktivierten Gruppe ohne Geld noch (bei alten Monaten womöglich dauerhaft). Kein Geld wird unsichtbar.
- Work-Lauf 2026-11-21 (budgeting-nyx75): der Styleguide zeigt `Button.quietTextFor` weiter mit „Umbenennen“ als Beispieltext; der Satz unter „Was hier nicht passiert“ in der README (Kategorien anlegen oder umbenennen bleibt in YNAB) ist seit budgeting-pscgb überholt.
- Work-Lauf 2026-11-21 (budgeting-yx51n): das erste Festhalten des Rahmens läuft nicht unter `MonthGuard`; liest der Rahmen ohne Stand YNAB, während eine Rückschreibung im letzten Monat das Fenster erweitert, kann das Festhalten die Erweiterung überschreiben (nur direkt nach dem Deploy, das Fenster wird nur kleiner, kein 404).
- Review-Lauf 2026-11-21: eine in BB angelegte oder verschobene Kategorie fehlt bis zum Nachziehen (budgeting-ffjk9) im gehaltenen Katalog des Rahmens; `checkedWrite` ruft nach dem Schreiben ohnehin `RefreshMirror` (`GET /categories`) auf, aus dieser Antwort ließe sich der Katalog ohne weiteren YNAB-Aufruf mit festhalten. Beim Verfeinern von budgeting-ffjk9 mitdenken.
- Review-Lauf 2026-11-21: Targets prüfen, nach einem gescheiterten „übernehmen“ weiter unten in der Liste rollt die Zeile mit dem roten Fehler ins Bild, der Hinweis „Prüfung nicht neu gelesen … Neu laden“ steht oben außer Sicht. Ob er ins Bild gehört, entscheidet Roman (sichtbares Verhalten).
