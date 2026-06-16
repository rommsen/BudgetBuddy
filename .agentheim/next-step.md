---
schema_version: 1
project: BudgetBuddy
---

# Nächste Schritte: Split-Vorzeichen-Bug gefixt (ynab-003, 14e1f61)

Romans Live-Feedback zum Cashback-Split ist behoben: Beträge werden jetzt als **positive
Magnituden** getippt, das Vorzeichen der (negativen) Buchung wird intern angewandt — `200`
gegen −222,15 ergibt korrekt 22,15 statt 422,15. Die read-only „Rest"-Zeile ist weg, **alle
Beträge editierbar**, und es gibt einen **Rest-Button pro Zeile** (dein Wunsch: Rest nicht
selbst rechnen). 572 Tests grün, fresh-eyes-verifier PASS. Wird gerade deployt. Wie weiter?

<options>
  <option title="ynab-003 am Gerät gegenchecken">Nach dem Deploy denselben Cashback-Fall am Handy nachstellen (222,15-Buchung, 200 bar, Rest-Button auf der Kategorie) und einen generischen 3-Zeilen-Split — die Sheet-/Picker-Mechanik ist laut ADR 0005 nur am echten Gerät voll verifizierbar. Ich kann die App auch headless starten + screenshotten.</option>
  <option title="design-system-002 refinen" cmd='/agentheim:modeling design-system-002'>Einziger offener Backlog-Task (Drift-Audit): View-Code gegen den Styleguide prüfen (hartkodierte Farben, inline-Feliz statt DS-Komponente) und in Refactor-Tasks splitten. Noch unrefined — braucht eine Modeling-Runde.</option>
  <option title="Nichts — abwarten">Erstmal im Alltag nutzen und schauen, ob noch etwas am Split (oder anderswo) auffällt; nächste Idee per `capture`/`modeling` einkippen, wenn sie kommt.</option>
</options>
