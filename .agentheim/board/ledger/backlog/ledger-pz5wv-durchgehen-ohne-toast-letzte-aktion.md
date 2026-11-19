---
id: ledger-pz5wv
title: "Durchgehen ohne Toast (B3): letzte Aktion unter „Passt, weiter“ mit Rückgängig für jede Aktion, Kategorie am Feld bestätigt, Fehler rot an derselben Stelle"
status: backlog
type: feature
context: ledger
created: 2026-11-19
completed:
depends_on: [design-system-2k7uw]
blocks: []
tags: [design-exploration, durchgehen, toast, frontend]
related_adrs: [0087]
related_research: []
prior_art: [ledger-vf5h6]
---

## Why
Roman am 2026-11-19: der Toast der vorigen Aktion liegt im Durchgehen auf „Passt, weiter“, man muss warten.
Entscheidung B3 und Regel R3 (design-system-2k7uw).

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, von Roman am 2026-11-19 freigegeben). Worker können sie
nicht öffnen: beim Verfeinern liest der Conductor sie (`Artifact` read), legt Datei und Screenshots je Ansicht in den
Scratchpad und nennt die Pfade in der Task (CLAUDE.md „Skizzen“).

- Unter „Passt, weiter“ steht nach der ersten Aktion die letzte: „✓ **Passt** REWE Markt …“, rechts „Rückgängig“. Die
  Aktion steht vorn und wird nie gekürzt, nur der Payee. Bleibt bis zur nächsten Aktion.
- Rückgängig neu auch für „Passt“ und „Später“ (heute nur im Toast nach Kategorie, Memo, Lena, Verwerfen): holt die
  Karte zurück und nimmt Prüfvermerk bzw. Lena-Flag zurück; bei „Später“ holt es die Karte nach vorn. Am PC Taste Z.
- Kategorie auf der Karte: das Feld leuchtet kurz, darunter „<Name> · <Betrag> verfügbar“ und „Rückgängig“; Übertrag mit „Gegenbuchung angelegt“.
- Fehler eines Befehls: die Karte federt zurück und bleibt unverändert, der Fehler steht rot in derselben Zeile mit
  „Erneut“ (nicht auf der Karte, sonst muss man scrollen). YNAB-Warnungen gelb in derselben Zeile.
- Alle Toasts des Durchgehens entfallen; Liste und Vorschlag je Toast im Anhang der Skizze.

## Acceptance criteria
- [ ] Kein Toast im Durchgehen am Handy; jede Funktion aus dem Abgleich der Skizze vorhanden (Anhang „Abgleich“).
- [ ] Rückgängig für Passt, Später, Lena, Verwerfen, Ist neu, Kategorie; Test je Weg.
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome), Abweichungen nur die in dieser Task genannten.

## Notes
Captured 2026-11-19 aus ledger-vf5h6.
