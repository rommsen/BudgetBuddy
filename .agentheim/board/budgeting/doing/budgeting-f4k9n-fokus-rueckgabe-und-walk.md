---
id: budgeting-f4k9n
title: "Muss-Befunde zu budgeting-g2ty9: ReturnFocus nimmt beim Tippen den Fokus, Walk der Rückfragen ohne getippten Betrag"
status: doing
type: bug
context: budgeting
created: 2026-11-04
completed:
depends_on: []
blocks: []
tags: [budget, target, fokus, test, review]
related_adrs: [0080]
related_research: []
prior_art: [budgeting-g2ty9, budgeting-xp46a]
---

## Why
Die Reviews zu budgeting-g2ty9 (Commit bd78ffc) melden zwei Muss-Befunde. Sollte und Kann gehen gesammelt in einen eigenen Backlog-Eintrag.

## What

### M1 (Frontend-Review, Bug)
`src/Client/DesignSystem/BottomSheet.fs` (Effekt mit `ReturnFocus`, etwa Zeilen 606 bis 618), dazu `src/Client/Components/Budget/TargetEdit.fs` (`typeInForm`, `TargetAmountChanged`, `typeTargetDate`) und `TargetEditView.fs`.

Fehlerfall am Handy: Rhythmus "Woche" wählen, "Target speichern" tippen, die Warnung steht an der Stelle des Knopfs. Betrags- und Datumsfeld darüber bleiben bedienbar. Ein Zeichen ins Betragsfeld setzt `ConfirmingChange = false`, `BottomSheet.confirm` wird ausgehängt, die Aufräumfunktion setzt per `setTimeout 0` den Fokus auf `[data-testid='target-save'] button`. Das Feld verliert nach dem ersten Zeichen den Fokus, die Tastatur schließt, weitere Zeichen gehen verloren. Mit Hardware-Tastatur landet ein folgendes Enter auf "Target speichern". Dasselbe beim Datumsfeld und beim Schließen durch stilles Neulesen oder Kategoriewechsel.

Gewünscht: Der Fokus geht nur zurück, wenn er beim Aushängen noch in der Rückfrage lag oder auf `body` gefallen ist (alternativ: Rückgabe nur an Abbrechen, Escape und Bestätigen gebunden). Die Regel in `/styleguide` Abschnitt 18 und Styleguide §5 entsprechend nachziehen.

### M2 (QA-Review, fehlender Test)
`src/Tests/TargetChangeClientTests.fs`, askWalkTests (`events` etwa Zeile 623, Prüfung je Schritt etwa Zeile 662).

Fehlerfall: Der Walk hat kein Ereignis "Betrag" oder "Datum". Mutante A (`startTargetSave` prüft wieder nur `form.ConfirmingChange` statt `confirmOpen form`) sendet bei "Entfernen, Betrag 60, Speichern" ein Speichern bei offener Entfernen-Bestätigung, der Walk bleibt grün. Mutante B (`canSaveTarget` mit `not form.ConfirmingChange`) bleibt ebenso unentdeckt.

Gewünscht: Ereignis "Betrag" (`TargetAmountChanged "60"`) im Walk, Orakel setzt `Typed = true` und schließt die Warnung, `RemoveOpen` bleibt; "Speichern" sendet laut Orakel nur bei getipptem Betrag ohne offene Rückfrage und ohne laufenden Aufruf. Je Schritt zusätzlich `canSaveTarget` gegen das Orakel prüfen. Beide Mutanten müssen den Walk rot machen (im Outcome nennen, Mutantenliste genau diese zwei).

## Acceptance criteria
- [ ] M1: Bei offener Warnung im Tab Target am Handy lässt sich ein mehrstelliger Betrag ins Betragsfeld tippen, der Fokus bleibt im Feld. `[fokus]`-Test in `scripts/target-change.test.mjs`, vor dem Fix rot.
- [ ] M1: Abbrechen, Escape und Bestätigen geben den Fokus weiter wie bisher zurück (bestehender `[fokus]`-Test bleibt grün).
- [ ] M1: `/styleguide` Abschnitt 18 und `standards/frontend/styleguide.md` §5 nennen die Bedingung der Rückgabe.
- [ ] M2: Der Walk kennt "Betrag", prüft je Schritt `canSaveTarget` und wird mit Mutante A und mit Mutante B rot.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Nicht im Umfang: die Sollte-Befunde (Pause-Sperre nur im Tab Target, Entfernen-Bestätigung beim Neulesen, Meldungstexte, Fokus nach erfolgreichem Entfernen). Die stehen im Sammel-Eintrag.
