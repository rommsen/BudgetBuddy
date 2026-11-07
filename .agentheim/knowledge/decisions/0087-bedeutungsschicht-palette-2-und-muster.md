---
id: 0087
title: Bedeutungsschicht: Palette 2 ohne Seitenausnahmen, Lena als Flag, drei Orte, Panel statt Dialog, Tastenregeln
scope: global
status: accepted
date: 2026-11-07
supersedes: []
superseded_by: []
related_tasks: [design-system-dak16, design-system-bcdqy, infrastructure-nx14s, infrastructure-jc1y1]
related_adrs: [0049, 0075, 0077, 0082, 0086]
related_research: [farbschema-neon-dark-2026-11-07, seitenlayout-liste-detail-2026-11-07]
---

# ADR 0087: Bedeutungsschicht für BudgetBuddy

## Context
Etappe 1 des Fahrplans (ADR 0086) legt fest, was Farben und Muster in BB bedeuten. Grundlage
sind zwei geprüfte Recherchen (`farbschema-neon-dark-2026-11-07`,
`seitenlayout-liste-detail-2026-11-07`), die Skizze
(https://claude.ai/artifact/HPpe24Y1owgpSZLWvtdGWd, Version 2) und der klickbare Prototyp
(https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg, Version 14), den Roman am Handy und am PC
benutzt hat.

Befunde am Ist-Stand: Weißer Text auf `neon-orange` hat 2,84:1, `neon-purple` und `neon-pink`
fallen als Text auf hellen Flächen unter 4,5:1, `border` hat 1,35:1. Orange heißt mal Hauptaktion,
mal Warnung; der Styleguide hat für Targets prüfen vier Seitenausnahmen, Pink gilt nur im
Kassenbuch, Gelb nur im Budget. Am Handy liegen im Kassenbuch zwei seitlich wischende Leisten
übereinander, die Navigation hat sechs Punkte, einer davon eine Aktion.

## Decision

### Farben (Palette 2)
1. **Eine Farbe, eine Bedeutung, auf jeder Seite.** Seitenausnahmen gibt es nicht. Braucht eine
   Seite eine neue Bedeutung, bekommt sie einen neuen Token oder Neutral. Die Ausnahmen aus ADR 0075
   (Nachtrag Targets prüfen) und ADR 0049 Punkt 7 (Pink) entfallen.
2. **Akzent Cyan `#28d6df`** für Hauptaktion und Auswahl/Navigation zugleich (Modell Linear).
   Hauptaktion = gefüllter Knopf, Auswahl = Kante, Strich, Fläche. Text auf dem Akzent ist dunkel
   (`bg-app`). Orange entfällt als UI-Farbe.
3. **Gelb `#cdb27a`: braucht dich, kein Fehler** (zu prüfen, fehlt, ohne Kategorie, unsicheres
   Duplikat, offene Aufgabe). Immer mit Wort daneben. Pink entfällt.
4. **Rot `#fd4f75`: Fehler und Minus.** **Grün `#5be479`: gelungen** (Rückmeldung, alles erledigt).
   **Blau `#7ab8ff`: Hinweis.** Lila (`neon-purple`) und `calm-violet` als UI-Farbe entfallen.
5. **Neuer Token `border-strong` `#6a6c86`** für Ränder, die ein Bedienelement erst erkennbar
   machen (Eingabefeld, Auswahlfeld, Knopf ohne Füllung) und für Fokusringe (3:1).
6. **Kontrast:** Text 4,5:1 und erkennbar machende Ränder 3:1 gegen `bg-card`, `bg-elevated` und
   `bg-hover`. Glow nur an der Hauptaktion und an Rückmeldungen.
7. Flächen und Textstufen bleiben unverändert (sie bestehen).

### Lena und Flags
8. **Lena wird über das Flag gekennzeichnet**, nicht über eine reservierte UI-Farbe. Die Farbe
   kommt aus `lena_flag_color` (ADR 0056) und erscheint nur im Flag-Symbol, immer mit dem Namen.

### Orte und Abläufe
9. **Drei Orte**: Übersicht, Kassenbuch, Budget. Unten am Handy und links am PC stehen nur Orte,
   keine Aktionen. Einstellungen hinter einem Symbol. Regeln sind ein Tab im Kassenbuch. Quick Add
   ist kein Ort mehr; sein Versprechen gilt für „+ Buchung“ (Kassenbuch und Übersicht).
10. **Übersicht (Variante A)**: zuerst „Zu tun“ als geordnete Liste mit genau einem Hauptknopf,
    dann der Monat (feste Höhe), dann Angepinnt (wachsend), am Handy danach die Konten.
11. **Durchgehen** neuer Buchungen startet in der Übersicht, Karte für Karte.
12. **Import direkt**: Umsätze gehen ins Kassenbuch und nach YNAB als „zu prüfen“. Nur unsichere
    Duplikate warten und erscheinen beim Durchgehen als eigene Karte. Abrufen geht jederzeit von Hand.
13. **Kassenbuch am Handy**: Kontowahl als Auswahlfeld mit Saldo, Suche, ein Filter-Knopf mit
    Sheet; gesetzte Filter stehen sichtbar als Chips. Keine seitlich wischenden Leisten.
14. **Angepinnte Kategorien** auf der Übersicht, auswählbar dort und im Kategorie-Detail. Das
    Anpinnen ist Zustand, kein Ereignis (ADR 0083).

### Muster
15. **Umschalter nur für eine feste Menge von höchstens vier Optionen.** Alles, was der Nutzer
    anlegt (Konten, Kategorien, Gruppen, Flags), ist ein Auswahlfeld mit Liste.
16. **Panel statt Dialog am PC**: Was ein Ding der Liste zeigt, bearbeitet oder neu anlegt, steht
    rechts im Panel (rund 30 % der Breite). Ein Dialog ist nur eine kurze Frage oder Auswahl.
17. **Mehrfachauswahl** macht das Panel zur Zusammenfassung mit Summe, Ausgaben, Einnahmen, Aufteilung
    nach Kategorien und den Sammelaktionen. Umschalt-Klick, Strg/Cmd-Klick, Umschalt+Pfeil.
18. **Pfeile gehören der Liste** (wie `Inspector.fs` `pageKeyAction`): ↑↓ Zeile und ←→ Tab auch aus dem
    Suchfeld; in Betragsfeldern gehören sie dem Cursor; ohne Auswahl ↓ erste, ↑ letzte Zeile.
19. **Oberster Dialog zuerst**: Ein offener Dialog bekommt Tasten zuerst und verbraucht sie. Esc
    schließt nur ihn und führt dorthin zurück, wo man herkam.
20. **Auswahl-Dialog**: am PC Fokus sofort in der Suche, ↑↓ bewegen die Markierung ohne Neuaufbau,
    Enter wählt.
21. **Animation hängt am Öffnen**, nie am Rendern; Scrollposition eines Dialogs bleibt beim Neuzeichnen.
22. **Rückmeldungen verdecken nie Bedienelemente.**
23. **Feste Höhe vor variabler Höhe**, damit feste Blöcke immer an derselben Stelle stehen.
24. **Eine Hauptaktion je Ansicht.**

## Consequences
### Positive
- Jede Farbe ist ohne Nachschlagen lesbar, Aktion und Warnung sind nie verwechselbar.
- Die Muster sind prüfbar formuliert; neue Seiten haben eine Antwort, bevor sie gebaut werden.

### Negative
- Der Tokenwechsel ändert das Aussehen aller Seiten auf einmal. Die Designwert-Tests müssen vorher weg
  (`infrastructure-nx14s`, `infrastructure-jc1y1`).
- Orange als Charakterfarbe der Hero-Knöpfe geht verloren.

### Neutral
- Die Umsetzung der Orte und Abläufe (Punkte 9 bis 14) und der Muster in bestehenden Seiten gehört zu
  Etappe 3; Etappe 1 legt sie in Tokens und Styleguide fest.
