---
id: design-system-bcdqy
title: "Muster im Styleguide: Orte, Auswahl, Liste mit Panel, Tastatur, Dialoge und Layout-Regeln für Handy und PC"
status: doing
type: feature
context: design-system
created: 2026-11-07
completed:
depends_on: [design-system-001]
blocks: []
tags: [etappe-1, styleguide, muster, tastatur, layout, doku]
related_adrs: [0087, 0086, 0005, 0004]
related_research: [seitenlayout-liste-detail-2026-11-07]
prior_art: [design-system-zahv9]
---

## Why
Etappe 1 des Fahrplans (ADR 0086). Jeder Bereich von BB hat seine Muster selbst erfunden: am Handy
mal Pillenleiste, mal Selectbox; Historie im Kassenbuch anders als im Budget; Regeln im Dialog,
Buchungen im Panel; Tasten je Seite anders. Im Prototyp hat Roman die gemeinsamen Muster
festgelegt (ADR 0087 Punkte 9 bis 24). Damit Etappe 2 und 3 dagegen bauen, müssen sie im
Styleguide stehen, prüfbar formuliert.

## What
Der Styleguide `standards/frontend/styleguide.md` bekommt die Muster aus ADR 0087 und die
Layout-Regeln aus der Recherche `seitenlayout-liste-detail-2026-11-07` (Abschnitt 6). Nur §4, §5
und §7; die Farben schreibt `design-system-dak16` (§2, §3).

Neue oder überarbeitete Abschnitte in §7, je mit „Regel“, „Wann“, „Wann nicht“ und Quelle (ADR 0087
Punkt n oder Recherche):
1. **Drei Orte und Navigation**: Übersicht, Kassenbuch, Budget; unten bzw. links nur Orte, keine
   Aktionen; Einstellungen hinter einem Symbol; Regeln als Tab im Kassenbuch.
2. **Auswahl**: Umschalter nur für eine feste Menge bis vier Optionen; alles vom Nutzer Angelegte als
   Auswahlfeld mit Liste; höchstens eine seitlich wischende Leiste je Ansicht, nur für Zweitrangiges,
   Anfang nie abgeschnitten; gesetzte Filter sichtbar als Chips, eine Zahl am Knopf allein reicht nicht.
3. **Liste mit Panel (PC)**: ab 840 px Liste und Panel nebeneinander, Panel rund 30 % der Breite;
   Panel statt Dialog für Anzeigen, Bearbeiten und Neuanlegen; Dialog nur für kurze Frage oder Auswahl.
4. **Mehrfachauswahl**: Umschalt-Klick, Strg/Cmd-Klick, Umschalt+Pfeil, Leertaste; ab zwei
   Einträgen wird das Panel zur Zusammenfassung (Summe groß, Ausgaben, Einnahmen, nach Kategorie,
   Sammelaktionen); eine Hauptaktion bleibt eine.
5. **Tastatur**: Pfeile gehören der Liste, auch aus dem Suchfeld (Regel aus `Inspector.fs`
   `pageKeyAction` für alle Listen mit Panel); in Betragsfeldern dem Cursor; ohne Auswahl ↓ erste,
   ↑ letzte Zeile; ←→ wechseln Tabs; oberster Dialog bekommt Tasten zuerst und verbraucht sie; Esc
   schließt nur ihn und führt zurück.
6. **Auswahl-Dialog**: am PC Fokus sofort in der Suche, ↑↓ bewegen die Markierung ohne Neuaufbau,
   Enter wählt; am Handy kein Autofokus.
7. **Bewegung**: Animation hängt am Öffnen, nie am Rendern; Scrollposition eines Dialogs bleibt beim
   Neuzeichnen.
8. **Historie**: eine Form für Buchung und Kategorie (Zeitpunkt, Herkunft als neutrales Kürzel, Text,
   Betrag rechts).
9. **Leerer Zustand**: ein Satz, was erscheinen wird, höchstens eine Aktion.
10. **Rückmeldung**: verdeckt nie ein Bedienelement (bei offenem Sheet oben, am PC unten rechts über
    einer Leiste).
11. **Seitenaufbau**: eine Hauptaktion je Ansicht; höchstens drei Schriftgrößen und zwei große
    Elemente; feste Höhe vor variabler Höhe; Gleiches nicht doppelt (eine Kontowahl).
12. **Layout-Regeln Handy und PC** aus Recherche Abschnitt 6 (Trefferflächen 48 px, Seitenrand 16 px,
    Sheets mit sichtbarem Schließen, Zurück schließt, nie gestapelt; am PC freie Breite an Inhalt,
    Konten in der Seitenleiste, Filter als sichtbare Zeile, Zeilen dürfen dichter als 44 px sein).

§5 (Komponenten): `Segmented` mit „nur feste Menge bis vier“; `Modal` mit „nur kurze Frage oder
Auswahl, sonst Panel“; `BottomSheet` und Picker mit dem Tastaturverhalten.

## Acceptance criteria
- [ ] §7 enthält die zwölf Abschnitte oben, jeder mit „Regel“, „Wann“, „Wann nicht“ und einer Quelle (ADR-Punkt oder Recherche-Abschnitt).
- [ ] Jede Regel aus ADR 0087 Punkte 9 bis 24 ist in §7 oder §5 genau einmal zu finden; das Outcome listet die Zuordnung Punkt zu Abschnitt.
- [ ] §5 nennt bei `Segmented`, `Modal`, `BottomSheet` und `CategoryPicker` die neue Regel im „Wann“ oder „Wann nicht“.
- [ ] Kein Satz in §4, §5 oder §7 widerspricht §2 (Farben nach ADR 0087); geprüft gegen den Stand von `design-system-dak16`, falls er schon gemergt ist, sonst gegen ADR 0087.
- [ ] Wo eine Regel heute verletzt ist (etwa zwei wischende Leisten im Kassenbuch, sechs Punkte in der Navigation, Regeln im Dialog), steht das als Zeile im Fahrplan Etappe 3, nicht als neues Ticket.
- [ ] Roman hat die Abschnitte gelesen und freigegeben (Styleguide-Gate). [human-eye]
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Vorlage: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 14), der jedes Muster
  am Handy und am PC zeigt; Recherche `.agentheim/knowledge/research/seitenlayout-liste-detail-2026-11-07.md`.
  Der Conductor legt den Prototyp in den Scratchpad (CLAUDE.md, Ablauf).
- Reine Doku, kein Code. Die Muster werden in Etappe 3 gebaut; dieser Schritt macht sie zur Messlatte.
- Prose-only, unenforced: Die Regeln sind Konventionen für Client-Code; eine mechanische Prüfung je
  Regel ist nicht Teil dieser Task. Wo später billig prüfbar (etwa „keine zwei horizontalen Scroller
  je Ansicht“ im Sweep aus `infrastructure-nx14s`), kommt das als Zeile in die Kann-Sammlung.
