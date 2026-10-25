---
id: ADR-0061
title: Rückfrage zurücknehmen nur bei Rückfrage Lena, Einstellung über SettingsApi
scope: ledger
status: accepted
date: 2026-10-25
related_tasks: [ledger-xfjcp]
related_adrs: [0032, 0050, 0055, 0056]
---

# ADR-0061: Rückfrage zurücknehmen nur bei Rückfrage Lena, Einstellung über SettingsApi

## Context

ADR 0056 beschreibt "Zurücknehmen" als Schreiben von "keine Farbe, kein Name" und nennt als No-Op
nur den Fall "schon kein Flag". Offen blieb, was `WithdrawLena` auf einer Buchung mit einem anderen
Flag (*Markiert*) tut. Außerdem legt ADR 0056 fest, dass die Farbe in den Settings steht, aber nicht,
über welche API.

## Decision

1. `setLenaFlag ... WithdrawLena` schreibt "kein Flag" nur, wenn der Ist-Stand *Rückfrage Lena* ist
   (Flag-Name "Lena", getrimmt, ohne Groß-/Kleinschreibung). Bei jedem anderen Stand antwortet der
   Befehl `FlagUnchanged` ohne Ereignis und Push. "Rückfrage zurücknehmen" meint die Lena-Rückfrage
   und löscht kein fremdes Flag. Das Detail bietet die Aktion ohnehin nur bei *Rückfrage Lena* an.
2. Die Deutung "Name ist Lena" liegt an einer Stelle (`Ledger.Domain.isLenaFlagName`), die der Lesepfad
   (`ReadModel.flagOf`) und die Befehle gemeinsam nutzen. Die SQL-Klausel des Filters
   (`bb_lower(trim(flag_name, Whitespace)) = 'lena'`) bildet sie nach.
3. Die Einstellung `lena_flag_color` läuft über zwei neue Member der `SettingsApi`
   (`getLenaFlagSettings` mit gespeicherter Farbe und Vorschlag, `saveLenaFlagColor`), nicht über
   `AppSettings`. Das lässt `AppSettings` und seine Aufrufer unverändert. Die Logik liegt in
   `Ledger.QueryHandlers.getLenaFlagSettings` und `Ledger.CommandHandlers.saveLenaFlagColor`, damit sie
   ohne `Server.Api` testbar ist. Eine gespeicherte Farbe, die keine der sechs YNAB-Farben ist, zählt als
   nicht gesetzt.

## Consequences

- Eine Marke-Buchung bleibt bei "Zurücknehmen" unberührt; wer sie löschen will, braucht eine eigene Geste.
- Die Settings-Seite lädt die Farbe mit einem zweiten Aufruf neben `getSettings`.
