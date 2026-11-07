---
id: design-system-bcdqy
title: "Muster im Styleguide: Orte, Auswahl, Liste mit Panel, Tastatur, Dialoge und Layout-Regeln für Handy und PC"
status: done
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

## Outcome

Der Styleguide `standards/frontend/styleguide.md` enthält die Muster aus ADR 0087 (Punkte 9 bis 24) und die Layout-Regeln der Recherche `seitenlayout-liste-detail-2026-11-07` (Abschnitt 6). Nur reine Doku, kein Code.

- **§7:** Neue Einleitung (die Regeln gelten für neue und umgebaute Ansichten, Abweichungen stehen im Fahrplan Etappe 3) und zwölf Abschnitte, je mit „Regel“, „Wann“, „Wann nicht“ und „Quelle“: 7.4 Drei Orte und Navigation, 7.5 Auswahl, 7.6 Liste mit Panel (PC), 7.7 Mehrfachauswahl, 7.8 Tastatur, 7.9 Auswahl-Dialog, 7.10 Bewegung, 7.11 Historie, 7.12 Leerer Zustand, 7.13 Rückmeldung, 7.14 Seitenaufbau, 7.15 Layout-Regeln Handy und PC. 7.1 Punkt 4 (Stacking) sagt jetzt, dass Sheets ohne Stapel das Ziel sind und `sheetLayered` bis Etappe 3 abweicht. 7.3 nennt die ChipBar der Budget-Historie als heutigen Stand (Ziel §7.11).
- **§5:** `Segmented` mit „nur feste Menge bis vier“ im Wann und „fünf oder mehr und Angelegtes → Auswahlfeld“ im Wann nicht; `Modal` mit „nur kurze Frage oder Auswahl“ im Wann und „Anzeigen, Bearbeiten, Neuanlegen → Panel“ im Wann nicht; `BottomSheet` mit der Tastenregel (Sheet bekommt die Tasten zuerst, Esc schließt nur es, Auswahl nach §7.9) und „kein zweites Sheet darüber“; neue Zeile `CategoryPicker` mit dem Tastaturverhalten aus §7.9. Überschrift jetzt „23 DS-Komponenten“, die Zeile in „Quellen“ ebenso.
- **§4:** Verweis, dass Zeilen am PC dichter sein dürfen (§7.15), und höchstens drei Größen je Ansicht (§7.14).
- **Farben (Kriterium „kein Widerspruch zu §2“):** `design-system-dak16` ist noch nicht gemergt, geprüft wurde gegen ADR 0087. In §5 sind „Orange-CTA“, „Orange-Knopf“, „Teal“ und die Seitenausnahmen „`notice` für die Budget-Seite“ (Badge, ErrorDisplay) durch die Rollen aus ADR 0087 ersetzt (Akzent, Hauptaktion, Gelb = braucht dich). Code-Bezeichner wie `heroTeal` und `Icons.sync MD Teal` bleiben, sie benennen heutigen Code; dak16 räumt sie ab.
- **Tastenregel:** gegen `pageKeyAction`, `pageKeyActionFor`, `typesIntoSearch` in `src/Client/Components/Budget/Inspector.fs` und `keyFocus` in `src/Client/Components/Budget/View.fs` abgeglichen (Suchfeld: alle vier Richtungen, ←→ nur mit Auswahl; andere Felder: Cursor; offener Picker oder Dialog: Seite lässt die Taste). Esc-Reihenfolge, Mehrfachauswahl und Auswahl-Dialog aus dem Prototyp Version 14 (`keydown`-Handler, `selectWithMods`, `selSummary`, `pickerOpts`).
- **Abweichung vom Prototyp:** Der Prototyp schaltet erst ab 1024 px auf Liste mit Panel. Der Styleguide nennt 840 px, wie Task und Recherche (PC 1). Roman sollte das beim Styleguide-Gate bestätigen.
- **Quellen ohne ADR-Punkt:** 7.11 Historie und 7.12 Leerer Zustand haben keinen ADR-0087-Punkt und keinen Recherche-Abschnitt; Quelle ist der Prototyp Version 14, auf den ADR 0087 im Abschnitt Context verweist (bei 7.12 zusätzlich §8).
- **Gate:** `./verify.sh` zeigt `VERIFY: PASS` (dotnet 4286 grün, node 317 grün). Das Kriterium „Roman hat freigegeben [human-eye]“ ist offen.
- **Kann-Sammlung design-system (Vorschlag, kein Ticket):** Billige mechanische Prüfung „höchstens eine horizontal scrollende Leiste je Ansicht“ im Sweep aus `infrastructure-nx14s`.

### Zuordnung ADR 0087 Punkt zu Abschnitt

Jeder Punkt hat genau eine Heimat, an der die Regel steht. An anderen Stellen steht höchstens ein Verweis mit Abschnittsnummer.

| Punkt | Heimat | Verweise |
|---|---|---|
| 9 Drei Orte | §7.4 | §7.15 (unten nur Orte) |
| 10 Übersicht Variante A | §7.4 | |
| 11 Durchgehen | §7.4 | |
| 12 Import direkt | §7.4 | |
| 13 Kassenbuch am Handy | §7.5 | |
| 14 Angepinnte Kategorien | §7.4 | |
| 15 Umschalter bis vier, Angelegtes als Auswahlfeld | §7.5 | §5 Segmented |
| 16 Panel statt Dialog | §7.6 | §5 Modal |
| 17 Mehrfachauswahl | §7.7 | |
| 18 Pfeile gehören der Liste | §7.8 | |
| 19 Oberster Dialog zuerst | §7.8 | §5 Modal, BottomSheet |
| 20 Auswahl-Dialog | §7.9 | §5 BottomSheet, CategoryPicker |
| 21 Animation am Öffnen | §7.10 | |
| 22 Rückmeldung verdeckt nichts | §7.13 | |
| 23 Feste Höhe vor variabler | §7.14 | |
| 24 Eine Hauptaktion je Ansicht | §7.14 | §7.7 (Sammelaktion) |

### Zeilen für Fahrplan Etappe 3

Bereits vorhandene Zeilen decken ab: Regeln im Dialog (Zeile „PC: Seitenleiste mit Konten, Liste plus Panel überall (auch Regeln …)“), Quick Add als Ort, Regeln ins Kassenbuch, Tastenregel für alle Listen, Historie im Kassenbuch wie im Budget, Kontowahl vereinheitlichen. Neu:

- Navigation nur mit den drei Orten, Einstellungen hinter einem Symbol (heute sechs Punkte: Sync, Rules, Quick Add, Kassenbuch, Budget, Settings; Styleguide §7.4).
- Kassenbuch am Handy: die zwei seitlich wischenden Leisten entfallen, dafür Filter-Knopf mit Sheet und gesetzte Filter als Chips (Styleguide §7.5).
- Wochentag eines Targets: heute sieben Knöpfe im `Segmented`, wird ein Auswahlfeld (Styleguide §7.5).
- Panel rund 30 % der Breite und ab 840 px (heute fest 340 px im Budget, 360 px in Targets prüfen erst ab 1024 px, 420 px im Buchungs-Detail; Styleguide §7.6).
- Kategorie-Picker nicht mehr über einem Sheet stapeln (`CategoryPicker.sheetLayered` in Kassenbuch, Split, Regeln, Budget); die Auswahl ersetzt den Inhalt des Sheets und führt zurück (Styleguide §7.15).
- Rückmeldung am PC über der Auswahl-Leiste des Kassenbuchs statt auf ihr (heute Toast `md:bottom-4` auf der Leiste `fixed bottom-0`; Styleguide §7.13).
- Budget-Historie: Filter nach Art als Auswahlfeld „Zeigen“ statt `ChipBar` (Styleguide §7.11).
