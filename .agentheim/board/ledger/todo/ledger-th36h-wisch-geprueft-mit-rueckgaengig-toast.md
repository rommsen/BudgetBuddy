---
id: ledger-th36h
title: "Wisch nach rechts markiert eine Buchung als geprüft, Toast mit Rückgängig (Handy)"
status: todo
type: feature
context: ledger
created: 2026-10-19
completed:
depends_on: [ledger-r777c, design-system-001]
blocks: []
tags: [frontend, kassenbuch, handy, design-system]
related_adrs: [0028, 0032, 0050]
related_research: []
prior_art: []
---

## Why
Am Handy ist der Wisch die schnellste Geste für Romans Hauptablauf (neue Buchungen
durchsehen, jede als geprüft markieren). `ledger-r777c` liefert den schreibenden Weg und
eine Schaltfläche. Diese Task legt die Geste darauf und gibt mit "Rückgängig" einen
Ausweg, weil ein Wisch leicht danebengeht.

## What
1. **Geste:** Wisch nach rechts an einer Kassenbuch-Karte (Handy) ruft `setReviewMark
   Reviewed` aus `ledger-r777c` auf. Eine Geste, keine zweite Stufe. Wisch nach links
   bleibt frei. Basis ist `Swipe.SwipeableRow` (`src/Client/DesignSystem/Swipe.fs`, heute
   nur im SyncFlow).
2. **Toast mit Aktion:** Nach der Geste erscheint ein Toast "<Payee> geprüft" (Beispiel
   "REWE Mitte geprüft", Handoff Ablauf 1, Screen 1.3) mit der Aktion "Rückgängig" in Teal.
   Er sitzt über der Tab-Bar, nicht über der Karte. Der Toast braucht dafür einen Aktions-Slot im Design System
   (`src/Client/Types.fs:184`, `src/Client/DesignSystem/Toast.fs:29` kennen heute keinen).
   Die Erweiterung gehört in die Toast-Komponente, die Galerie `/styleguide`
   (`src/Client/Components/Styleguide/View.fs`) bekommt ein Beispiel dazu.
3. **Rückgängig:** ruft `restoreReviewMark` mit dem Vorzustand aus der Antwort des
   Befehls auf (ADR 0050, Punkt 1 und 10). Die Zeile kehrt in die Liste zurück, wenn sie
   der Chip "Zu prüfen" ausgeblendet hatte, der Zähler steigt wieder.
4. **Wisch auf geprüfter Zeile:** erzeugt `NoChange`, kein Toast mit Rückgängig.
5. **Fehlschlag des Rückschreibens:** der Toast meldet "Prüfvermerk nicht in YNAB" mit dem
   Grund, die Zeile trägt den Hinweis aus `ledger-r777c`.

## Acceptance criteria
- [ ] Toast-Modell: ein Toast kann eine optionale Aktion (Beschriftung und Nachricht)
      tragen, ohne Aktion verhält er sich wie bisher (Reducer-Test, bestehende
      Toast-Tests bleiben grün).
- [ ] `Toast.renderList` rendert die Aktion als Schaltfläche, ein Klick löst die
      Nachricht aus und schließt den Toast (Ansichts-Test).
- [ ] Reducer-Test: Wisch auf eine zu prüfende Zeile löst `setReviewMark Reviewed` aus,
      speichert den Vorzustand aus der Antwort und zeigt den Toast "<Payee> geprüft" mit
      "Rückgängig".
- [ ] Reducer-Test: Rückgängig löst `restoreReviewMark` mit genau diesem Vorzustand aus,
      auch wenn der Vorzustand `approved = true`, `Uncleared` war.
- [ ] Reducer-Test: Rückgängig sendet nie eine Reconciled-Angabe (Vorzustand Reconciled
      wird als "nicht anfassen" weitergegeben).
- [ ] Reducer-Test: Wisch auf eine bereits geprüfte Zeile erzeugt keinen Toast mit
      Rückgängig.
- [ ] Reducer-Test: mit aktivem Chip "Zu prüfen" verschwindet die Zeile nach dem Wisch,
      nach Rückgängig kommt sie zurück und der Zähler stimmt.
- [ ] Wisch nach links löst nichts aus (Ansichts-Test auf die Konfiguration von
      `SwipeableRow`).
- [ ] Die Galerie `/styleguide` zeigt den Toast mit Aktion (Test, dass die Galerie ihn
      rendert, analog zu den bestehenden Galerie-Tests).
- [ ] `dotnet build` und `dotnet test` grün.
- [ ] Die Wisch-Geste fühlt sich am Handy flüssig an, der Toast verdeckt weder die Karte
      noch die Bottom-Navigation. [human-eye]

## Notes
Abgespalten am 2026-10-19 beim Verfeinern von `ledger-r777c` (Entscheidung von Roman). Die
Toast-Erweiterung ist Design-System-Arbeit und kommt nach CLAUDE.md samt Galerie-Beispiel.

Verfeinert am 2026-10-19: Toast-Text und Lage aus dem Handoff Ablauf 1 (Screen 1.3)
übernommen. `ledger-nhwrt` nutzt denselben Aktions-Slot für den Sammel-Toast
"n Buchungen geprüft · Rückgängig"; wer zuerst umgesetzt wird, baut den Slot.
