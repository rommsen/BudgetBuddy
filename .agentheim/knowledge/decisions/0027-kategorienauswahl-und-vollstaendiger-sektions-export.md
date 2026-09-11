---
id: 0027
title: Kategorienauswahl als eine Karte mit beiden Zuständen; Sektions-Export-Umfang und -Ausnahmen; Portal-Karten schließen sich nacheinander
scope: design-system
status: accepted
date: 2026-09-11
supersedes: []
superseded_by: []
related_tasks: [design-system-y6gkd]
related_adrs: [0024, 0005]
related_research: []
---

# ADR 0027: Kategorienauswahl als eine Karte, Sektions-Export-Umfang, sequentielles Öffnen/Schließen der Portal-Karten

## Context
`design-system-y6gkd` erweitert den Export (`design-system-t82sg`, ADR 0024) um zwei
Dinge: die im Erst-Sync fehlende Kategorienauswahl (`BottomSheet.categoryPicker`) und
den Export aller übrigen Styleguide-Sektionen. Drei Fragen ohne bestehende Antwort:

1. **Wie viele Karten für die Kategorienauswahl?** Der Picker zeigt beim Öffnen
   gleichzeitig die Vorschlags-/Zuletzt-verwendet-Chips UND die Gruppenliste mit
   Suchfeld darunter (`CategoryPickerInternal`, `src/Client/DesignSystem/BottomSheet.fs`):
   es gibt keinen zweiten, getrennten UI-Zustand, den man separat öffnen müsste.
2. **Welche Sektionen bekommen eine eigene, aus dem DOM gerenderte Karte, welche nicht?**
   Die Route hat 22 Sektionen; einige sind bereits anders abgedeckt (Farben/Typografie
   über generierte Token-Karten), eine ist reiner Fließtext (Voice).
3. **Wie erfasst man mehrere Portal-Komponenten (BottomSheet, Kategorienauswahl, Modal)
   in derselben Playwright-Session, ohne dass eine noch offene Karte die nächste
   verfälscht?** Alle drei rendern per `createPortal` nach `document.body`, BottomSheet
   und Kategorienauswahl teilen sich sogar dieselben CSS-Klassen (`.overlay`,
   `.bottom-sheet`).

## Decision
1. **Eine Karte mit beiden Zuständen.** `components-categorypicker.html` zeigt das
   offene Sheet unverändert, wie `CategoryPickerInternal` es tatsächlich rendert: Chips
   oben, Gruppenliste mit farbcodiertem Available darunter, Suchfeld dazwischen. Keine
   zweite, künstlich in zwei Interaktionsschritte zerlegte Karte, denn diesen zweiten
   Zustand gibt es im echten Picker nicht.
2. **Sektions-Export-Umfang:** jede Sektion, die eigenständiges, visuell prüfbares
   Markup rendert, bekommt eine eigene `data-ds-card`-Karte (Button, Stats, Icons,
   PageHeader, Primitives, Swipe, Loading, ErrorDisplay, Input, Form, Toast, Modal,
   Kategorienauswahl, zusätzlich zu den bereits vorhandenen Card/Money/Badge/Table/
   Navigation/BottomSheet). Ausgenommen: Farbsemantik- und Typografie-Sektion (bereits
   über die generierten "Colors"/"Type"-Token-Karten abgedeckt, aus denselben Werten,
   kein zweites, potenziell driftendes Set) und die Voice-Sektion (reiner Fließtext,
   steht im Bundle-README/SKILL statt in einer Karte, wie im Task vorgeschlagen).
3. **Portal-Karten öffnen, einsammeln, schließen, erst dann die nächste öffnen.**
   Neue Helferfunktion `extractPortalCard` im Export-Skript: Trigger klicken, auf ein
   Bereitschafts-Signal warten, Markup in einen synthetischen `data-ds-card`-Wrapper
   klonen, schreiben, dann über einen kartenspezifischen Schließen-Selektor wieder
   schließen und auf das Verschwinden der `.active`-Klasse warten, bevor die nächste
   Portal-Karte an der Reihe ist. Reihenfolge: BottomSheet, Kategorienauswahl (beide
   teilen sich `.overlay`/`.bottom-sheet`), danach Modal (eigener Fixed-Wrapper
   `.fixed.inset-0.z-50.overflow-hidden`). Toast rendert nicht per Portal (kein Eintrag
   in `renderToBody`), braucht aber denselben Trigger-Klick vor der Erfassung, da sonst
   kein Toast im Container sichtbar ist.

## Consequences
### Positive
- Das Bundle zeigt Roman und Claude Design exakt den echten Interaktions-Zustand des
  Pickers, den Ablauf 4 "Kategorie ändern" (`ledger-xbrht`) tatsächlich nutzt: kein
  eigens für den Export gebautes Mockup.
- Fast alle Styleguide-Sektionen sind jetzt im Bundle vertreten (Liste im Diary der
  Task-Datei); die drei Ausnahmen sind begründet, nicht übersehen.
- Sequentielles Öffnen/Schließen verhindert, dass `document.querySelector(".bottom-sheet.active")`
  eine falsche, noch offene Karte einer vorherigen Portal-Komponente trifft: ohne diese
  Reihenfolge hätte die Kategorienauswahl-Karte das BottomSheet-Markup erfasst.
- Zwei Export-Läufe bleiben byte-identisch (verifiziert); jede `preview/*.html` beginnt
  weiterhin in Zeile 1 mit dem `@dsCard`-Marker.

### Negative
- Die Kategorienauswahl-Karte zeigt keinen Zustand mit eingegebenem Suchtext (nur die
  Chips + Gruppenliste ohne Filter): kleine, akzeptierte Dokumentationslücke, dieselbe
  Klasse wie die fehlende "enthüllte Aktion" bei der Swipe-Karte.
- `extractPortalCard` fügt dem Export-Skript eine zusätzliche Abstraktionsebene hinzu;
  gerechtfertigt, weil drei Portal-Karten (BottomSheet, Kategorienauswahl, Modal)
  dieselbe Öffnen/Einsammeln/Schließen-Logik brauchen und Kopieren des Blocks pro Karte
  mehr Drift-Risiko gehabt hätte.

## Alternatives considered
- **Zwei Karten für die Kategorienauswahl** (Einstieg separat von Gruppenliste): hätte
  einen künstlichen zweiten Zustand erfordert (z. B. Suchtext eintippen, um die Chips
  auszublenden), den Roman im echten Ablauf so nicht als eigenen Schritt erlebt. Der
  Task selbst stellte beide Varianten frei; die einfachere, wahrheitsgetreuere wurde
  gewählt.
- **Typografie- und Farbsemantik-Sektion zusätzlich als eigene DOM-Karten exportieren:**
  hätte ein zweites, aus dem DOM statt aus den Tokens generiertes Set derselben Werte
  erzeugt, mit Drift-Risiko zwischen beiden. Verworfen zugunsten der bestehenden,
  token-generierten Karten.
- **Jede Portal-Karte offen lassen und stattdessen über eindeutigere CSS-Selektoren
  (z. B. verschachtelte `:not()`-Ketten) unterscheiden:** wäre fragiler geworden, je
  mehr Portal-Komponenten dazukommen, und hätte die Reihenfolge im Skript an die
  Reihenfolge im DOM gekoppelt. Schließen vor dem nächsten Öffnen ist robuster und
  bleibt unabhängig von der Anzahl der Portal-Karten.
