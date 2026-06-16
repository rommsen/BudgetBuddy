---
schema_version: 1
project: BudgetBuddy
---

# Nächste Schritte: cat-001 + design-system-004 geshippt (Deploy läuft)

Beide todo-Tasks gebaut, verifiziert und committet — und auf Romans Anweisung nach origin
gepusht + deployt. **cat-001** (9112013): Regeln per ▲/▼ umsortierbar, oben gewinnt zuerst
(Backend `reorderRules` war schon fertig). **design-system-004** (18cf474): Toasts blenden
sanft aus (Zwei-Phasen-Exit, ADR 0007), Platzierung + Motion im Styleguide festgehalten.
cat-001 brauchte eine zweite Verifier-Runde (Token-/Touch-Target-Defekte). Was als Nächstes?

<options>
  <option title="Am Gerät gegenchecken">Nach dem Deploy: Regeln umsortieren (kippt eine zu-eager Regel hinter eine spezifischere?) und einen Toast auslösen — blendet er sanft aus, sitzt er richtig (mobil oben über der Bottom-Nav)? Ich kann auch headless screenshotten.</option>
  <option title="design-system-002 refinen" cmd='/agentheim:modeling design-system-002'>Der offene Backlog-Task: Drift-Audit der View-Schichten gegen den Styleguide. Noch unrefined — jetzt mit zwei frischen DS-Touchpoints (Toast-Motion, Reorder-Buttons) als Referenz.</option>
  <option title="Flaky SQLite-Test angehen" cmd='/agentheim:capture'>Der Verifier sah einen sporadischen `PatternType Conversions`-Fehler bei Connection-Disposal (Microsoft.Data.Sqlite 9.0.11/9.0.13-Versionskonflikt). Kein Blocker, aber als infrastructure-Bug erfassbar.</option>
  <option title="Nichts — im Alltag nutzen">Erstmal benutzen; nächste Auffälligkeit/Idee per `capture`/`modeling` einkippen.</option>
</options>
