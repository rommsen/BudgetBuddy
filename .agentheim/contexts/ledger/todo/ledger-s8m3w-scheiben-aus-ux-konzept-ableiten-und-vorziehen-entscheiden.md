---
id: ledger-s8m3w
title: Bau-Scheiben aus dem Kassenbuch-UX-Konzept ableiten und über das Vorziehen schreibender Scheiben vor v2 entscheiden
status: todo
type: decision
context: ledger
created: 2026-09-11
completed:
depends_on: [ledger-xbrht]
blocks: []
tags: [frontend, ux, kassenbuch, scheiben, vision]
related_adrs: [0012, 0013, 0021]
related_research: []
prior_art: [ledger-q5w9c, ledger-kt0wq]
---

## Why
`ledger-xbrht` entwirft das Kassenbuch als Arbeitsfläche für das ganze Ziel und liefert
einen klickbaren Prototyp. Gebaut wird in Scheiben, zuerst die lesenden. Welche Scheiben
es gibt, in welcher Reihenfolge, und ob schreibende Abläufe (geprüft, Kategorie, Split,
Flags) schon vor v2 kommen, lässt sich erst sinnvoll festlegen, wenn Roman den Prototyp
durchgespielt hat. Deshalb ein eigener Task (Entscheidung Roman, 2026-09-11, bei der
Verfeinerung von `ledger-xbrht`).

## What
1. **Scheiben ableiten** aus Konzept und Prototyp von `ledger-xbrht`, als Backlog-Tasks im
   `ledger` (bzw. `ynab-sync`, wo der YNAB-Verkehr betroffen ist). Voraussichtliche
   Reihenfolge, beim Ableiten zu prüfen:
   - lesend: Gesamtsicht mit Kontoständen, Filter, Mehrfachauswahl mit Summe,
     Herkunft/Historie und "noch nicht in YNAB" in der neuen Liste;
   - Spiegel-Erweiterung: `approved` und Flags in Snapshot und Beobachtungs-Ereignissen,
     damit "noch nicht geprüft" und das Rückfrage-Flag überhaupt lesend angezeigt werden
     können;
   - schreibend: geprüft markieren, Kategorie ändern, Split ändern, Flags setzen, jeweils
     mit Rückschreibe-Weg nach YNAB und Abweichungs-Sicht.
2. **`ledger-p3n8k`** (gelöschte Buchungen ausblenden) in eine Scheibe aufgehen lassen oder
   ausdrücklich als eigenständig bestätigen.
3. **Entscheidung mit Roman:** werden schreibende Scheiben vor v2 gebaut? Wenn ja, Vision
   (`.agentheim/vision.md`, Stufen v1/v2) anpassen und die Entscheidung als ADR festhalten.

## Acceptance criteria
- [ ] Jede Scheibe ist als Backlog-Task erfasst, mit Styleguide-Gate
      (`design-system-001` in `depends_on`) und Verweis auf ihren Prototyp-Screen bzw. den
      Screenshot unter `docs/ux/kassenbuch/`.
- [ ] Die Scheiben stehen in einer Reihenfolge mit `depends_on`, lesende vor schreibenden;
      die Spiegel-Erweiterung für `approved` und Flags ist eine eigene Scheibe vor jeder
      Anzeige, die sie braucht.
- [ ] `ledger-p3n8k` ist in einer Scheibe aufgegangen (und per DISMISS oder REFINE
      bereinigt) oder ausdrücklich als eigenständig bestätigt.
- [ ] Ob schreibende Scheiben vor v2 gebaut werden, ist mit Roman entschieden und als ADR
      festgehalten; wenn ja, ist die Vision angepasst.

## Notes
- **"Geprüft" ist fachlich Romans Prüfvermerk.** Technisch ist es vermutlich YNABs
  `approved` (Direktimport-Buchungen wie ING kommen unbestätigt an); `cleared` trägt für
  Roman keine Information, weil importierte Buchungen immer schon bei der Bank sind. Beim
  Modellieren der schreibenden Scheibe festzurren.
- **Der Spiegel kennt heute weder `approved` noch Flags.** Der Ereignis-Katalog
  (`ledger/README.md`, "Ereignisse") beobachtet Details, Kategorisierung, Cleared und
  Löschung. Schon eine lesende Anzeige "noch nicht geprüft" oder "Rückfrage-Flag" braucht
  eine Erweiterung von Snapshot und Beobachtungs-Ereignissen (Katalog-Änderung, mit Roman).
- Schreibende Scheiben heißen "BB entscheidet, YNAB wird nachgezogen" (ADR 0012): BB
  braucht dafür den Rückschreibe-Weg nach YNAB (Update bestehender Buchungen) und die
  Abweichungs-Sicht. Das ist Vision-Stufe v2 und größer als eine UI-Scheibe.
- Läuft wie `ledger-xbrht` in der Hauptsession mit Roman (Modeling-Arbeit, kein Code).
  Bleibt im Backlog, bis der Prototyp aus `ledger-xbrht` durchgespielt ist; die genaue
  Form hängt von dessen Ergebnis ab.
